from __future__ import annotations

import base64
import ctypes
import hashlib
import os
import signal
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Callable, Mapping, Sequence

from .errors import ErrorCode, ProtocolFailure


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


@dataclass(frozen=True)
class ProcessIdentity:
    pid: int
    process_start_time_utc: str
    executable_path: str
    executable_sha256: str


BrokerIdentity = ProcessIdentity


def _windows_process_details(pid: int) -> tuple[Path, str]:
    from ctypes import wintypes

    process_query_limited_information = 0x1000
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    handle = kernel32.OpenProcess(process_query_limited_information, False, pid)
    if not handle:
        raise ProcessLookupError(pid)
    try:
        size = wintypes.DWORD(32768)
        buffer = ctypes.create_unicode_buffer(size.value)
        if not kernel32.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(size)):
            raise OSError(ctypes.get_last_error(), "QueryFullProcessImageNameW failed")
        creation = wintypes.FILETIME()
        exit_time = wintypes.FILETIME()
        kernel = wintypes.FILETIME()
        user = wintypes.FILETIME()
        if not kernel32.GetProcessTimes(
            handle, ctypes.byref(creation), ctypes.byref(exit_time), ctypes.byref(kernel), ctypes.byref(user)
        ):
            raise OSError(ctypes.get_last_error(), "GetProcessTimes failed")
        ticks = (creation.dwHighDateTime << 32) | creation.dwLowDateTime
        unix_100ns = ticks - 116444736000000000
        seconds, remainder = divmod(unix_100ns, 10_000_000)
        timestamp = datetime.fromtimestamp(seconds, timezone.utc).replace(microsecond=remainder // 10)
        return Path(buffer.value).resolve(strict=True), timestamp.isoformat(timespec="microseconds").replace("+00:00", "Z")
    finally:
        kernel32.CloseHandle(handle)


def _posix_process_details(pid: int) -> tuple[Path, str]:
    proc = Path("/proc") / str(pid)
    executable = (proc / "exe").resolve(strict=True)
    started = datetime.fromtimestamp(proc.stat().st_ctime, timezone.utc)
    return executable, started.isoformat(timespec="microseconds").replace("+00:00", "Z")


def capture_process_identity(pid: int) -> ProcessIdentity:
    try:
        executable, started = _windows_process_details(pid) if os.name == "nt" else _posix_process_details(pid)
    except (OSError, ValueError) as exc:
        raise ProcessLookupError(pid) from exc
    return ProcessIdentity(pid, started, str(executable), sha256_file(executable))


def _normalized_path(value: str) -> str:
    return os.path.normcase(os.path.realpath(value))


def identity_matches(expected: ProcessIdentity, actual: ProcessIdentity) -> bool:
    return (
        expected.pid == actual.pid
        and expected.process_start_time_utc == actual.process_start_time_utc
        and _normalized_path(expected.executable_path) == _normalized_path(actual.executable_path)
        and expected.executable_sha256.lower() == actual.executable_sha256.lower()
    )


def require_process_identity(expected: ProcessIdentity) -> ProcessIdentity:
    try:
        actual = capture_process_identity(expected.pid)
    except ProcessLookupError as exc:
        raise ProtocolFailure(
            ErrorCode.PROCESS_IDENTITY_MISMATCH,
            "recorded process no longer exists",
            details={"pid": expected.pid},
        ) from exc
    if not identity_matches(expected, actual):
        raise ProtocolFailure(
            ErrorCode.PROCESS_IDENTITY_MISMATCH,
            "PID, start time, executable path, or executable hash no longer matches",
            details={"pid": expected.pid},
        )
    return actual


def assert_broker_alive(identity: BrokerIdentity) -> ProcessIdentity:
    try:
        return require_process_identity(identity)
    except (ProtocolFailure, ProcessLookupError) as exc:
        raise ProtocolFailure(
            ErrorCode.BROKER_EXIT,
            "session broker is not the recorded live process; old game processes cannot be adopted",
            details={"pid": identity.pid},
        ) from exc


def build_game_environment(
    *,
    base: Mapping[str, str],
    session_id: str,
    instance_id: str,
    pipe_name: str,
    token: bytes,
    output_root: Path,
    app_data: Path | None = None,
    local_app_data: Path | None = None,
) -> dict[str, str]:
    if len(token) != 32:
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "companion token must contain exactly 256 bits")
    environment = dict(base)
    environment.update(
        {
            "STS2_TEST_ENABLE": "1",
            "STS2_TEST_SESSION_ID": session_id,
            "STS2_TEST_INSTANCE_ID": instance_id,
            "STS2_TEST_PIPE": pipe_name,
            "STS2_TEST_TOKEN": base64.urlsafe_b64encode(token).decode("ascii").rstrip("="),
            "STS2_TEST_OUTPUT_ROOT": str(output_root.resolve()),
        }
    )
    if app_data is not None:
        environment["APPDATA"] = str(app_data.resolve())
    if local_app_data is not None:
        environment["LOCALAPPDATA"] = str(local_app_data.resolve())
    return environment


def redact_environment(environment: Mapping[str, str]) -> dict[str, Any]:
    public_names = sorted(
        name for name in environment if name != "STS2_TEST_TOKEN" and not any(part in name.upper() for part in ("SECRET", "PASSWORD", "CREDENTIAL"))
    )
    return {
        "names": public_names,
        "has_companion_token": "STS2_TEST_TOKEN" in environment,
        "values_recorded": False,
    }


def validate_isolated_user_data(actual: Path, expected_root: Path) -> Path:
    actual_resolved = actual.resolve(strict=True)
    expected_resolved = expected_root.resolve()
    try:
        actual_resolved.relative_to(expected_resolved)
    except ValueError as exc:
        raise ProtocolFailure(
            ErrorCode.ISOLATION_BREACH,
            "game reported a user-data path outside the isolated instance root",
            details={"reported": str(actual_resolved), "expected_root": str(expected_resolved)},
        ) from exc
    return actual_resolved


@dataclass(frozen=True)
class ProcessRecord:
    session_id: str
    instance_id: str
    role: str
    identity: ProcessIdentity
    command_argv_redacted: list[str]
    environment_allowlist: list[str]
    pipe_name: str
    adapter_expected: str
    state: str
    exit_code: int | None = None
    crash_reason: str | None = None

    def to_dict(self) -> dict[str, Any]:
        return {
            "session_id": self.session_id,
            "instance_id": self.instance_id,
            "role": self.role,
            "pid": self.identity.pid,
            "process_start_time_utc": self.identity.process_start_time_utc,
            "executable_path": self.identity.executable_path,
            "executable_sha256": self.identity.executable_sha256,
            "command_argv_redacted": list(self.command_argv_redacted),
            "environment_allowlist": list(self.environment_allowlist),
            "pipe_name": self.pipe_name,
            "adapter_expected": self.adapter_expected,
            "state": self.state,
            "exit_code": self.exit_code,
            "crash_reason": self.crash_reason,
        }

    @classmethod
    def from_dict(cls, value: Mapping[str, Any]) -> "ProcessRecord":
        identity = ProcessIdentity(
            int(value["pid"]),
            str(value["process_start_time_utc"]),
            str(value["executable_path"]),
            str(value["executable_sha256"]),
        )
        return cls(
            session_id=str(value["session_id"]),
            instance_id=str(value["instance_id"]),
            role=str(value["role"]),
            identity=identity,
            command_argv_redacted=[str(item) for item in value["command_argv_redacted"]],
            environment_allowlist=[str(item) for item in value["environment_allowlist"]],
            pipe_name=str(value["pipe_name"]),
            adapter_expected=str(value["adapter_expected"]),
            state=str(value["state"]),
            exit_code=int(value["exit_code"]) if value.get("exit_code") is not None else None,
            crash_reason=str(value["crash_reason"]) if value.get("crash_reason") is not None else None,
        )


@dataclass
class OwnedProcess:
    process: subprocess.Popen[bytes]
    identity: ProcessIdentity
    argv: list[str]
    stdout_handle: Any
    stderr_handle: Any


class ExactProcessManager:
    def __init__(self) -> None:
        self._owned: dict[int, OwnedProcess] = {}

    def spawn(
        self,
        argv: Sequence[str],
        *,
        cwd: Path,
        environment: Mapping[str, str],
        stdout_path: Path,
        stderr_path: Path,
    ) -> OwnedProcess:
        if not argv:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "process argv cannot be empty")
        stdout_path.parent.mkdir(parents=True, exist_ok=True)
        stderr_path.parent.mkdir(parents=True, exist_ok=True)
        stdout_handle = stdout_path.open("ab", buffering=0)
        stderr_handle = stderr_path.open("ab", buffering=0)
        child_environment = os.environ.copy()
        child_environment.update(environment)
        creationflags = 0
        if os.name == "nt":
            creationflags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.CREATE_NO_WINDOW
        try:
            process = subprocess.Popen(
                [str(item) for item in argv],
                cwd=str(cwd),
                env=child_environment,
                stdin=subprocess.DEVNULL,
                stdout=stdout_handle,
                stderr=stderr_handle,
                shell=False,
                creationflags=creationflags,
            )
            identity = capture_process_identity(process.pid)
        except Exception:
            stdout_handle.close()
            stderr_handle.close()
            raise
        owned = OwnedProcess(process, identity, [str(item) for item in argv], stdout_handle, stderr_handle)
        self._owned[process.pid] = owned
        return owned

    def status(self, identity: ProcessIdentity) -> dict[str, Any]:
        owned = self._owned.get(identity.pid)
        if owned is not None:
            exit_code = owned.process.poll()
            if exit_code is not None:
                self._close_streams(owned)
                return {"pid": identity.pid, "alive": False, "exit_code": exit_code}
        require_process_identity(identity)
        return {"pid": identity.pid, "alive": True, "exit_code": None}

    def stop(
        self,
        identity: ProcessIdentity,
        *,
        grace_seconds: float = 5.0,
        force: bool = False,
        request_shutdown: Callable[[], None] | None = None,
    ) -> dict[str, Any]:
        owned = self._owned.get(identity.pid)
        if owned is not None and owned.process.poll() is not None:
            self._close_streams(owned)
            return {"pid": identity.pid, "state": "exited", "exit_code": owned.process.returncode, "method": "already_exited"}
        require_process_identity(identity)
        if request_shutdown is not None:
            request_shutdown()
        if owned is not None:
            try:
                owned.process.wait(timeout=max(0.0, grace_seconds))
            except subprocess.TimeoutExpired:
                if force:
                    owned.process.kill()
                    method = "forced"
                else:
                    owned.process.terminate()
                    method = "terminated"
                try:
                    owned.process.wait(timeout=5)
                except subprocess.TimeoutExpired as exc:
                    raise ProtocolFailure(ErrorCode.TIMEOUT_ACTION, "process did not exit after exact termination") from exc
            else:
                method = "graceful"
            self._close_streams(owned)
            return {"pid": identity.pid, "state": "exited", "exit_code": owned.process.returncode, "method": method}
        if os.name == "nt":
            os.kill(identity.pid, signal.SIGTERM)
        else:
            os.kill(identity.pid, signal.SIGTERM)
        return {"pid": identity.pid, "state": "stopping", "exit_code": None, "method": "exact_pid_signal"}

    @staticmethod
    def _close_streams(owned: OwnedProcess) -> None:
        for handle in (owned.stdout_handle, owned.stderr_handle):
            if not handle.closed:
                handle.close()


class WriteSentinel:
    def __init__(self, protected_roots: Sequence[Path], *, allowed_roots: Sequence[Path] = ()) -> None:
        self.protected_roots = tuple(root.resolve() for root in protected_roots)
        self.allowed_roots = tuple(root.resolve() for root in allowed_roots)

    def capture(self) -> dict[str, dict[str, tuple[int, int]]]:
        snapshot: dict[str, dict[str, tuple[int, int]]] = {}
        for root in self.protected_roots:
            entries: dict[str, tuple[int, int]] = {}
            if root.exists():
                for path in sorted(root.rglob("*")):
                    if path.is_file() and not path.is_symlink():
                        stat = path.stat()
                        entries[path.relative_to(root).as_posix()] = (stat.st_size, stat.st_mtime_ns)
            snapshot[str(root)] = entries
        return snapshot

    def assert_unchanged(self, before: Mapping[str, Mapping[str, tuple[int, int]]]) -> None:
        after = self.capture()
        changes: list[dict[str, Any]] = []
        for root in sorted(set(before) | set(after)):
            old = dict(before.get(root, {}))
            new = dict(after.get(root, {}))
            for relative in sorted(set(old) | set(new)):
                if old.get(relative) != new.get(relative):
                    changes.append({"root": root, "path": relative, "before": old.get(relative), "after": new.get(relative)})
        if changes:
            raise ProtocolFailure(
                ErrorCode.ISOLATION_BREACH,
                "protected filesystem roots changed during the test session",
                details={"changes": changes[:100], "change_count": len(changes)},
            )
