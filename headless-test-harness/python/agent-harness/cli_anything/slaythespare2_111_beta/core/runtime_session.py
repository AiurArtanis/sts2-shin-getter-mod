from __future__ import annotations

import json
import os
import re
import tempfile
import uuid
from dataclasses import dataclass
from enum import StrEnum
from pathlib import Path
from typing import Any, Mapping, Sequence

from .errors import ErrorCode, ProtocolFailure
from .legacy import FileLock, utc_now
from .protocol import canonical_json_bytes


IDENTIFIER_PATTERN = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")
SECRET_NAME_PARTS = ("token", "proof", "secret", "credential")


def validate_identifier(value: str) -> str:
    if not IDENTIFIER_PATTERN.fullmatch(value):
        raise ProtocolFailure(
            ErrorCode.INVALID_ARGUMENT,
            "identifier must start with an alphanumeric character and contain at most 64 safe characters",
            details={"value": value},
        )
    return value


def default_runtime_root(environment: Mapping[str, str] | None = None) -> Path:
    env = dict(os.environ if environment is None else environment)
    base = env.get("LOCALAPPDATA") or env.get("TEMP") or tempfile.gettempdir()
    return Path(base).expanduser().resolve() / "cli-anything" / "slaythespare2-111-beta" / "sessions"


def is_reparse_point(path: Path) -> bool:
    if not path.exists() and not path.is_symlink():
        return False
    try:
        attributes = getattr(path.lstat(), "st_file_attributes", 0)
    except OSError:
        return False
    return path.is_symlink() or bool(attributes & 0x400)


def _same_or_descendant(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


class RuntimePathGuard:
    def __init__(self, repository_root: Path, protected_roots: Sequence[Path]) -> None:
        self.repository_root = repository_root.expanduser().resolve()
        self.protected_roots = tuple(
            dict.fromkeys([self.repository_root, *(root.expanduser().resolve() for root in protected_roots)])
        )

    def validate(self, candidate: Path, *, create: bool = False) -> Path:
        expanded = candidate.expanduser()
        if not expanded.is_absolute():
            raise ProtocolFailure(ErrorCode.ISOLATION_BREACH, "runtime root must be absolute")
        resolved = expanded.resolve()
        if resolved == Path(resolved.anchor):
            raise ProtocolFailure(ErrorCode.ISOLATION_BREACH, "filesystem root cannot be a runtime root")
        for protected in self.protected_roots:
            if _same_or_descendant(resolved, protected) or _same_or_descendant(protected, resolved):
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    "runtime root overlaps a protected tree",
                    details={"runtime_root": str(resolved), "protected_root": str(protected)},
                )
        probe = resolved
        while True:
            if is_reparse_point(probe):
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    "runtime path traverses a symlink or reparse point",
                    details={"path": str(probe)},
                )
            if probe.parent == probe:
                break
            probe = probe.parent
        if create:
            resolved.mkdir(parents=True, exist_ok=True)
        return resolved


def atomic_write_json(path: Path, value: Mapping[str, Any]) -> None:
    path = path.resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    lock_path = path.with_name(path.name + ".write.lock")
    payload = json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    with FileLock(lock_path):
        descriptor, temporary_name = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=path.parent)
        temporary = Path(temporary_name)
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(payload)
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temporary, path)
        finally:
            if temporary.exists():
                temporary.unlink()


def append_jsonl(path: Path, value: Mapping[str, Any]) -> None:
    path = path.resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    lock_path = path.with_name(path.name + ".append.lock")
    payload = canonical_json_bytes(value) + b"\n"
    with FileLock(lock_path):
        with path.open("ab") as handle:
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())


class InstanceState(StrEnum):
    DEFINED = "defined"
    STARTING = "starting"
    PIPE_WAITING = "pipe_waiting"
    AUTHENTICATING = "authenticating"
    READY = "ready"
    BUSY = "busy"
    STOPPING = "stopping"
    DISCONNECTED = "disconnected"
    FAILED = "failed"
    CRASHED = "crashed"
    EXITED = "exited"


TRANSITIONS: dict[InstanceState, set[InstanceState]] = {
    InstanceState.DEFINED: {InstanceState.STARTING},
    InstanceState.STARTING: {InstanceState.PIPE_WAITING, InstanceState.CRASHED, InstanceState.FAILED},
    InstanceState.PIPE_WAITING: {InstanceState.AUTHENTICATING, InstanceState.FAILED, InstanceState.CRASHED},
    InstanceState.AUTHENTICATING: {InstanceState.READY, InstanceState.FAILED, InstanceState.CRASHED},
    InstanceState.READY: {InstanceState.BUSY, InstanceState.STOPPING, InstanceState.DISCONNECTED, InstanceState.CRASHED},
    InstanceState.BUSY: {InstanceState.READY, InstanceState.STOPPING, InstanceState.DISCONNECTED, InstanceState.CRASHED},
    InstanceState.DISCONNECTED: {InstanceState.READY, InstanceState.STOPPING, InstanceState.CRASHED, InstanceState.FAILED},
    InstanceState.STOPPING: {InstanceState.EXITED, InstanceState.CRASHED},
    InstanceState.FAILED: {InstanceState.STOPPING, InstanceState.EXITED},
    InstanceState.CRASHED: {InstanceState.EXITED},
    InstanceState.EXITED: set(),
}


@dataclass(frozen=True)
class SessionPaths:
    root: Path

    @property
    def session_json(self) -> Path:
        return self.root / "session.json"

    @property
    def session_lock(self) -> Path:
        return self.root / "session.lock"

    @property
    def broker_json(self) -> Path:
        return self.root / "broker.json"

    @property
    def broker_events_jsonl(self) -> Path:
        return self.root / "broker-events.jsonl"

    @property
    def requests_jsonl(self) -> Path:
        return self.root / "requests.jsonl"

    @property
    def scenario_json(self) -> Path:
        return self.root / "scenario.json"

    @property
    def instances(self) -> Path:
        return self.root / "instances"

    @property
    def evidence(self) -> Path:
        return self.root / "evidence"


class ControlSession:
    def __init__(self, paths: SessionPaths, repository_root: Path) -> None:
        self.paths = paths
        self.repository_root = repository_root.resolve()

    @classmethod
    def create(
        cls,
        runtime_root: Path,
        session_id: str | None = None,
        *,
        repository_root: Path,
        protected_roots: Sequence[Path] = (),
    ) -> "ControlSession":
        identifier = validate_identifier(session_id or f"session-{uuid.uuid4().hex[:16]}")
        guarded_root = RuntimePathGuard(repository_root, protected_roots).validate(runtime_root, create=True)
        paths = SessionPaths(guarded_root / identifier)
        if paths.root.exists() and any(paths.root.iterdir()):
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "control session already exists")
        paths.instances.mkdir(parents=True, exist_ok=True)
        paths.evidence.mkdir(parents=True, exist_ok=True)
        for stream in (paths.requests_jsonl, paths.broker_events_jsonl):
            stream.touch(exist_ok=True)
        session = cls(paths, repository_root)
        session._save_index(
            {
                "schema": "sts2-control-session/v1",
                "session_id": identifier,
                "created_at": utc_now(),
                "updated_at": utc_now(),
                "state": "defined",
                "case_invalid": False,
                "instances": {},
            }
        )
        return session

    @classmethod
    def open(cls, session_root: Path, *, repository_root: Path) -> "ControlSession":
        paths = SessionPaths(session_root.resolve(strict=True))
        if not paths.session_json.is_file():
            raise ProtocolFailure(ErrorCode.NOT_FOUND, "session.json does not exist")
        return cls(paths, repository_root)

    def load_index(self) -> dict[str, Any]:
        try:
            value = json.loads(self.paths.session_json.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"invalid control session index: {exc}") from exc
        if not isinstance(value, dict):
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "control session index must be an object")
        return value

    def _save_index(self, value: dict[str, Any]) -> None:
        value["updated_at"] = utc_now()
        atomic_write_json(self.paths.session_json, value)

    def record_broker(self, public_identity: Mapping[str, Any]) -> None:
        for key in public_identity:
            if any(part in key.lower() for part in SECRET_NAME_PARTS):
                raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "broker.json cannot contain secret fields")
        allowed = {
            "pid", "process_start_time_utc", "executable_path", "executable_sha256",
            "control_pipe", "broker_epoch",
        }
        unexpected = set(public_identity) - allowed
        if unexpected:
            raise ProtocolFailure(
                ErrorCode.INVALID_ARGUMENT,
                "broker record contains unknown fields",
                details={"fields": sorted(unexpected)},
            )
        missing = allowed - set(public_identity)
        if missing:
            raise ProtocolFailure(
                ErrorCode.INVALID_ARGUMENT,
                "broker record is incomplete",
                details={"fields": sorted(missing)},
            )
        atomic_write_json(self.paths.broker_json, dict(public_identity))

    def define_instance(self, instance_id: str, *, role: str) -> dict[str, Any]:
        identifier = validate_identifier(instance_id)
        if role not in {"single", "host", "client"}:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "unknown instance role")
        index = self.load_index()
        if identifier in index["instances"]:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "instance already exists")
        root = self.paths.instances / identifier
        (root / "blobs").mkdir(parents=True, exist_ok=False)
        record = {
            "id": identifier,
            "role": role,
            "state": InstanceState.DEFINED.value,
            "root": str(root),
            "process_path": str(root / "process.json"),
            "runtime_path": str(root / "runtime.json"),
            "stdout_path": str(root / "stdout.log"),
            "stderr_path": str(root / "stderr.log"),
            "bridge_events_path": str(root / "bridge-events.jsonl"),
        }
        index["instances"][identifier] = record
        self._save_index(index)
        return record

    def instance(self, instance_id: str) -> dict[str, Any]:
        try:
            return dict(self.load_index()["instances"][instance_id])
        except KeyError as exc:
            raise ProtocolFailure(ErrorCode.NOT_FOUND, "unknown instance") from exc

    def transition_instance(self, instance_id: str, target: InstanceState) -> dict[str, Any]:
        index = self.load_index()
        try:
            record = index["instances"][instance_id]
        except KeyError as exc:
            raise ProtocolFailure(ErrorCode.NOT_FOUND, "unknown instance") from exc
        current = InstanceState(record["state"])
        if target not in TRANSITIONS[current]:
            raise ProtocolFailure(
                ErrorCode.INVALID_PHASE,
                f"illegal instance transition: {current.value} -> {target.value}",
            )
        record["state"] = target.value
        record["updated_at"] = utc_now()
        self._save_index(index)
        return dict(record)
