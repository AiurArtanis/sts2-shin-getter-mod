from __future__ import annotations

import hashlib
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
PROTECTION_POLICY_SCHEMA = "sts2-protection-policy/v1"
REQUIRED_PROTECTION_CATEGORIES = (
    "production_source",
    "steam_game",
    "workshop",
    "production_mod",
)
OPTIONAL_PROTECTION_CATEGORIES = ("production_deployment",)
PROTECTION_CATEGORIES = REQUIRED_PROTECTION_CATEGORIES + OPTIONAL_PROTECTION_CATEGORIES


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


def default_state_root(environment: Mapping[str, str] | None = None) -> Path:
    """Return the writable legacy CLI state root outside all source trees."""

    env = dict(os.environ if environment is None else environment)
    base = env.get("LOCALAPPDATA") or env.get("TEMP") or tempfile.gettempdir()
    return Path(base).expanduser().resolve() / "cli-anything" / "slaythespare2-111-beta" / "state"


def is_reparse_point(path: Path) -> bool:
    try:
        stat_result = path.lstat()
    except OSError:
        return False
    attributes = getattr(stat_result, "st_file_attributes", 0)
    return path.is_symlink() or bool(attributes & 0x400)


def _same_or_descendant(path: Path, root: Path) -> bool:
    try:
        path.relative_to(root)
        return True
    except ValueError:
        return False


def normalize_protection_policy(
    policy: Mapping[str, str | Path],
    *,
    implicit_roots: Sequence[Path] = (),
) -> dict[str, Path]:
    provided = {key for key in policy if isinstance(key, str)}
    required = set(REQUIRED_PROTECTION_CATEGORIES)
    supported = set(PROTECTION_CATEGORIES)
    missing = sorted(required - provided)
    unknown = sorted(str(key) for key in policy if not isinstance(key, str) or key not in supported)
    if missing or unknown:
        raise ProtocolFailure(
            ErrorCode.ISOLATION_BREACH,
            "live protection policy must name every required category exactly once",
            details={"missing_categories": missing, "unknown_categories": unknown},
        )

    normalized: dict[str, Path] = {}
    invalid: dict[str, str] = {}
    for category in PROTECTION_CATEGORIES:
        if category not in policy:
            continue
        raw_root = policy[category]
        if not isinstance(raw_root, (str, os.PathLike)):
            invalid[category] = repr(raw_root)
            continue
        root = Path(raw_root).expanduser()
        if not root.is_absolute() or not root.is_dir():
            invalid[category] = str(root)
            continue
        normalized[category] = root.resolve(strict=True)
    if invalid:
        raise ProtocolFailure(
            ErrorCode.ISOLATION_BREACH,
            "every named protection root must be an existing absolute directory",
            details={"invalid_roots": invalid},
        )

    identities: dict[str, str] = {}
    duplicates: dict[str, list[str]] = {}
    for category, root in normalized.items():
        identity = os.path.normcase(os.path.realpath(root))
        if identity in identities:
            duplicates.setdefault(identity, [identities[identity]]).append(category)
        else:
            identities[identity] = category
    if duplicates:
        raise ProtocolFailure(
            ErrorCode.ISOLATION_BREACH,
            "named protection categories must not resolve to the same directory",
            details={"duplicate_categories": list(duplicates.values())},
        )

    resolved_implicit = tuple(root.expanduser().resolve(strict=True) for root in implicit_roots)
    redundant: dict[str, str] = {}
    for category, root in normalized.items():
        if any(_same_or_descendant(root, implicit) or _same_or_descendant(implicit, root) for implicit in resolved_implicit):
            redundant[category] = str(root)
    if redundant:
        raise ProtocolFailure(
            ErrorCode.ISOLATION_BREACH,
            "named protection roots must add distinct coverage beyond implicit repository and project roots",
            details={"redundant_roots": redundant},
        )
    return normalized


def protection_policy_document(policy: Mapping[str, Path]) -> dict[str, Any]:
    return {
        "schema": PROTECTION_POLICY_SCHEMA,
        "roots": {
            category: str(policy[category])
            for category in PROTECTION_CATEGORIES
            if category in policy
        },
    }


def protection_policy_sha256(policy: Mapping[str, Path]) -> str:
    return hashlib.sha256(canonical_json_bytes(protection_policy_document(policy))).hexdigest()


def _persisted_protection_policy(index: Mapping[str, Any]) -> dict[str, Path] | None:
    document = index.get("protection_policy")
    if document is None:
        return None
    if not isinstance(document, Mapping) or document.get("schema") != PROTECTION_POLICY_SCHEMA:
        raise ProtocolFailure(
            ErrorCode.ISOLATION_BREACH,
            "control session has an invalid named protection-policy schema",
        )
    roots = document.get("roots")
    if not isinstance(roots, Mapping):
        raise ProtocolFailure(
            ErrorCode.ISOLATION_BREACH,
            "control session named protection policy has no roots mapping",
        )
    return normalize_protection_policy(roots)


class RuntimePathGuard:
    def __init__(self, repository_root: Path, protected_roots: Sequence[Path]) -> None:
        self.repository_root = repository_root.expanduser().resolve()
        self.protected_roots = tuple(
            dict.fromkeys([self.repository_root, *(root.expanduser().resolve() for root in protected_roots)])
        )

    def validate(self, candidate: Path, *, create: bool = False, purpose: str = "runtime root") -> Path:
        expanded = candidate.expanduser()
        if not expanded.is_absolute():
            raise ProtocolFailure(ErrorCode.ISOLATION_BREACH, f"{purpose} must be absolute")
        # Check the lexical path before resolve(). On Windows, resolve() follows
        # a junction and erases the reparse node that must make this path RED.
        self._reject_reparse_ancestors(expanded, purpose=purpose)
        resolved = expanded.resolve()
        if resolved == Path(resolved.anchor):
            raise ProtocolFailure(ErrorCode.ISOLATION_BREACH, f"filesystem root cannot be a {purpose}")
        for protected in self.protected_roots:
            if _same_or_descendant(resolved, protected) or _same_or_descendant(protected, resolved):
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    f"{purpose} overlaps a protected tree",
                    details={"candidate": str(resolved), "protected_root": str(protected), "purpose": purpose},
                )
        self._reject_reparse_ancestors(resolved, purpose=purpose)
        if create:
            resolved.mkdir(parents=True, exist_ok=True)
            self._reject_reparse_ancestors(expanded, purpose=purpose)
            if expanded.resolve() != resolved:
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    f"{purpose} changed target while being created",
                )
        return resolved

    @staticmethod
    def _reject_reparse_ancestors(candidate: Path, *, purpose: str) -> None:
        probe = candidate
        while True:
            if is_reparse_point(probe):
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    f"{purpose} traverses a symlink or reparse point",
                    details={"path": str(probe)},
                )
            if probe.parent == probe:
                break
            probe = probe.parent

    def validate_existing_directory(self, candidate: Path, *, purpose: str) -> Path:
        resolved = self.validate(candidate, purpose=purpose)
        if not resolved.is_dir():
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"{purpose} must be an existing directory")
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
    def __init__(
        self,
        paths: SessionPaths,
        repository_root: Path,
        protected_roots: Sequence[Path],
        *,
        protection_policy: Mapping[str, Path] | None = None,
    ) -> None:
        self.paths = paths
        self.repository_root = repository_root.resolve()
        self.protection_policy = dict(protection_policy or {})
        self.guard = RuntimePathGuard(
            self.repository_root,
            [*protected_roots, *self.protection_policy.values()],
        )

    @property
    def protected_roots(self) -> tuple[Path, ...]:
        return self.guard.protected_roots

    @property
    def protection_policy_sha256(self) -> str | None:
        if not self.protection_policy:
            return None
        return protection_policy_sha256(self.protection_policy)

    def revalidate(self) -> Path:
        return self.guard.validate(self.paths.root, purpose="control session root")

    def validate_process_cwd(self, cwd: Path) -> Path:
        self.revalidate()
        return self.guard.validate_existing_directory(cwd, purpose="process cwd")

    @classmethod
    def create(
        cls,
        runtime_root: Path,
        session_id: str | None = None,
        *,
        repository_root: Path,
        protected_roots: Sequence[Path] = (),
        protection_policy: Mapping[str, str | Path] | None = None,
    ) -> "ControlSession":
        identifier = validate_identifier(session_id or f"session-{uuid.uuid4().hex[:16]}")
        normalized_policy = (
            normalize_protection_policy(protection_policy)
            if protection_policy is not None
            else {}
        )
        guarded_root = RuntimePathGuard(
            repository_root,
            [*protected_roots, *normalized_policy.values()],
        ).validate(runtime_root, create=True)
        paths = SessionPaths(guarded_root / identifier)
        if paths.root.exists() and any(paths.root.iterdir()):
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "control session already exists")
        paths.instances.mkdir(parents=True, exist_ok=True)
        paths.evidence.mkdir(parents=True, exist_ok=True)
        for stream in (paths.requests_jsonl, paths.broker_events_jsonl):
            stream.touch(exist_ok=True)
        session = cls(
            paths,
            repository_root,
            protected_roots,
            protection_policy=normalized_policy,
        )
        session._save_index(
            {
                "schema": "sts2-control-session/v1",
                "session_id": identifier,
                "created_at": utc_now(),
                "updated_at": utc_now(),
                "state": "defined",
                "case_invalid": False,
                "repository_root": str(session.repository_root),
                "protected_roots": [str(root) for root in session.protected_roots],
                "protection_policy": (
                    protection_policy_document(session.protection_policy)
                    if session.protection_policy
                    else None
                ),
                "instances": {},
            }
        )
        return session

    @classmethod
    def open(
        cls,
        session_root: Path,
        *,
        repository_root: Path,
        protected_roots: Sequence[Path] = (),
        protection_policy: Mapping[str, str | Path] | None = None,
        expected_protection_policy_sha256: str | None = None,
    ) -> "ControlSession":
        resolved_repository = repository_root.expanduser().resolve(strict=True)
        requested_policy = (
            normalize_protection_policy(protection_policy)
            if protection_policy is not None
            else {}
        )
        requested_guard = RuntimePathGuard(
            resolved_repository,
            [*protected_roots, *requested_policy.values()],
        )
        resolved_session = requested_guard.validate(session_root, purpose="control session root").resolve(strict=True)
        paths = SessionPaths(resolved_session)
        if not paths.session_json.is_file():
            raise ProtocolFailure(ErrorCode.NOT_FOUND, "session.json does not exist")
        index = cls._read_index_file(paths.session_json)
        persisted_repository = index.get("repository_root")
        persisted_roots = index.get("protected_roots")
        if not isinstance(persisted_repository, str) or not isinstance(persisted_roots, list) or not all(
            isinstance(item, str) and Path(item).is_absolute() for item in persisted_roots
        ):
            raise ProtocolFailure(
                ErrorCode.ISOLATION_BREACH,
                "control session is missing persisted path-protection policy",
            )
        if os.path.normcase(os.path.realpath(persisted_repository)) != os.path.normcase(os.path.realpath(resolved_repository)):
            raise ProtocolFailure(
                ErrorCode.ISOLATION_BREACH,
                "control session repository identity does not match the caller",
            )
        persisted_policy = _persisted_protection_policy(index)
        if persisted_policy is None:
            if requested_policy or expected_protection_policy_sha256 is not None:
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    "control session is missing its named protection policy",
                )
        else:
            persisted_digest = protection_policy_sha256(persisted_policy)
            if not requested_policy and expected_protection_policy_sha256 is None:
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    "opening a named-policy session requires the same policy identity",
                )
            if requested_policy and protection_policy_sha256(requested_policy) != persisted_digest:
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    "control session named protection policy differs from the caller",
                    details={
                        "expected_policy_sha256": persisted_digest,
                        "actual_policy_sha256": protection_policy_sha256(requested_policy),
                    },
                )
            if (
                expected_protection_policy_sha256 is not None
                and expected_protection_policy_sha256 != persisted_digest
            ):
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    "control session named protection-policy digest differs from the broker bootstrap",
                    details={
                        "expected_policy_sha256": expected_protection_policy_sha256,
                        "actual_policy_sha256": persisted_digest,
                    },
                )
        combined = [
            *(Path(item) for item in persisted_roots),
            *protected_roots,
            *(persisted_policy or {}).values(),
        ]
        session = cls(
            paths,
            resolved_repository,
            combined,
            protection_policy=persisted_policy,
        )
        session.revalidate()
        return session

    @staticmethod
    def _read_index_file(path: Path) -> dict[str, Any]:
        try:
            value = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"invalid control session index: {exc}") from exc
        if not isinstance(value, dict):
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "control session index must be an object")
        return value

    def load_index(self) -> dict[str, Any]:
        self.revalidate()
        return self._read_index_file(self.paths.session_json)

    def _save_index(self, value: dict[str, Any]) -> None:
        value["updated_at"] = utc_now()
        atomic_write_json(self.paths.session_json, value)

    def mark_case_invalid(
        self,
        code: ErrorCode,
        message: str,
        *,
        details: Mapping[str, Any] | None = None,
    ) -> None:
        index = self.load_index()
        index["case_invalid"] = True
        index["case_invalid_reason"] = {
            "code": code.value,
            "message": message,
            "details": dict(details or {}),
        }
        self._save_index(index)

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
