from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from multiprocessing.connection import Client
from pathlib import Path
from typing import Any, Mapping, Sequence

from .errors import ErrorCode, ProtocolFailure
from .legacy import FileLock
from .process_manager import BrokerIdentity, assert_broker_alive
from .runtime_session import ControlSession


_CONTROL_LIMIT = 1024 * 1024


def control_address(pipe_name: str, session_root: Path) -> str:
    if os.name == "nt":
        return rf"\\.\pipe\{pipe_name}"
    return str(session_root / f".{pipe_name}.sock")


class BrokerClient:
    """Short-lived current-user client for a session's long-lived broker."""

    def __init__(self, session: ControlSession, broker_record: Mapping[str, Any]) -> None:
        self.session = session
        self.broker_record = dict(broker_record)
        self.broker_identity = BrokerIdentity(
            int(broker_record["pid"]),
            str(broker_record["process_start_time_utc"]),
            str(broker_record["executable_path"]),
            str(broker_record["executable_sha256"]),
        )
        self.pipe_name = str(broker_record["control_pipe"])

    @classmethod
    def from_session(cls, session: ControlSession) -> "BrokerClient":
        try:
            record = json.loads(session.paths.broker_json.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError, KeyError, TypeError, ValueError) as exc:
            raise ProtocolFailure(ErrorCode.BROKER_EXIT, "session has no valid broker identity") from exc
        client = cls(session, record)
        assert_broker_alive(client.broker_identity)
        return client

    @classmethod
    def bootstrap(
        cls,
        session: ControlSession,
        *,
        repository_root: Path,
        protected_roots: Sequence[Path] = (),
        timeout_seconds: float = 15.0,
    ) -> "BrokerClient":
        session.revalidate()
        for root in protected_roots:
            if root.expanduser().resolve() not in session.protected_roots:
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    "broker bootstrap protection differs from the persisted session policy",
                    details={"missing_protected_root": str(root.expanduser().resolve())},
                )
        bootstrap_lock = session.paths.root / "broker.bootstrap.lock"
        with FileLock(bootstrap_lock, timeout=timeout_seconds):
            if session.paths.broker_json.exists():
                # A stale record is a terminal ownership loss, never permission
                # to mint a new token and adopt an old game process.
                return cls.from_session(session)

            logs = session.paths.evidence / "logs"
            logs.mkdir(parents=True, exist_ok=True)
            stdout = (logs / "broker-stdout.log").open("ab", buffering=0)
            stderr = (logs / "broker-stderr.log").open("ab", buffering=0)
            command = [
                sys.executable,
                "-m",
                "cli_anything.slaythespare2_111_beta.core.broker",
                "--session-root",
                str(session.paths.root),
                "--repository-root",
                str(repository_root.resolve()),
            ]
            creationflags = 0
            if os.name == "nt":
                creationflags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.CREATE_NO_WINDOW
            try:
                process = subprocess.Popen(
                    command,
                    cwd=str(session.paths.root),
                    stdin=subprocess.DEVNULL,
                    stdout=stdout,
                    stderr=stderr,
                    shell=False,
                    creationflags=creationflags,
                )
            finally:
                stdout.close()
                stderr.close()

            deadline = time.monotonic() + timeout_seconds
            last_error: Exception | None = None
            while time.monotonic() < deadline:
                if process.poll() is not None:
                    raise ProtocolFailure(
                        ErrorCode.BROKER_EXIT,
                        "session broker exited during bootstrap",
                        details={"exit_code": process.returncode},
                    )
                if session.paths.broker_json.is_file():
                    try:
                        client = cls.from_session(session)
                        client.request("broker.status", {}, timeout_seconds=1.0)
                        return client
                    except (ProtocolFailure, OSError) as exc:
                        last_error = exc
                time.sleep(0.025)
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
            raise ProtocolFailure(
                ErrorCode.BROKER_EXIT,
                "timed out waiting for session broker bootstrap",
                details={"last_error": str(last_error) if last_error else None},
            )

    def request(
        self,
        operation: str,
        arguments: Mapping[str, Any],
        *,
        timeout_seconds: float = 30.0,
    ) -> dict[str, Any]:
        if timeout_seconds <= 0:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "broker timeout must be positive")
        assert_broker_alive(self.broker_identity)
        address = control_address(self.pipe_name, self.session.paths.root)
        deadline = time.monotonic() + timeout_seconds
        last_error: Exception | None = None
        while time.monotonic() < deadline:
            try:
                connection = Client(address, family="AF_PIPE" if os.name == "nt" else "AF_UNIX", authkey=None)
                break
            except (OSError, EOFError) as exc:
                last_error = exc
                try:
                    assert_broker_alive(self.broker_identity)
                except ProtocolFailure:
                    raise
                time.sleep(0.025)
        else:
            raise ProtocolFailure(
                ErrorCode.BROKER_EXIT,
                "cannot connect to recorded session broker",
                details={"last_error": str(last_error) if last_error else None},
            )
        with connection:
            payload = json.dumps(
                {"operation": operation, "arguments": dict(arguments)},
                ensure_ascii=False,
                separators=(",", ":"),
            ).encode("utf-8")
            if len(payload) > _CONTROL_LIMIT:
                raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "broker control request exceeds 1 MiB")
            try:
                connection.send_bytes(payload)
                remaining = deadline - time.monotonic()
                if remaining <= 0 or not connection.poll(remaining):
                    raise ProtocolFailure(
                        ErrorCode.TIMEOUT_ACTION,
                        "broker response exceeded the monotonic local deadline",
                        details={"operation": operation, "timeout_seconds": timeout_seconds},
                    )
                raw = connection.recv_bytes(_CONTROL_LIMIT)
            except ProtocolFailure:
                raise
            except (EOFError, OSError) as exc:
                raise ProtocolFailure(ErrorCode.BROKER_EXIT, f"broker control pipe failed: {exc}") from exc
        try:
            response = json.loads(raw)
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ProtocolFailure(ErrorCode.BROKER_EXIT, "broker returned invalid JSON") from exc
        if not isinstance(response, dict) or not isinstance(response.get("ok"), bool):
            raise ProtocolFailure(ErrorCode.BROKER_EXIT, "broker returned an invalid response envelope")
        if response["ok"]:
            result = response.get("result", {})
            if not isinstance(result, dict):
                raise ProtocolFailure(ErrorCode.BROKER_EXIT, "broker result must be an object")
            if operation == "session.close":
                self._wait_for_broker_exit(deadline)
            return result
        error = response.get("error") or {}
        try:
            code = ErrorCode(str(error.get("code")))
        except ValueError:
            code = ErrorCode.BROKER_EXIT
        raise ProtocolFailure(
            code,
            str(error.get("message", "broker request failed")),
            retryable=bool(error.get("retryable", False)),
            details=dict(error.get("details") or {}),
        )

    def _wait_for_broker_exit(self, deadline: float) -> None:
        while time.monotonic() < deadline:
            try:
                assert_broker_alive(self.broker_identity)
            except ProtocolFailure as exc:
                if exc.code == ErrorCode.BROKER_EXIT:
                    return
                raise
            time.sleep(min(0.025, max(0.0, deadline - time.monotonic())))
        raise ProtocolFailure(
            ErrorCode.TIMEOUT_ACTION,
            "session.close response arrived but the broker did not exit before the monotonic deadline",
            details={"pid": self.broker_identity.pid},
        )
