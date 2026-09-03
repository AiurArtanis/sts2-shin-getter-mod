from __future__ import annotations

import hashlib
import json
import os
import platform
import re
import sys
import tempfile
from copy import deepcopy
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence

from .errors import ErrorCode, ProtocolFailure
from .legacy import utc_now
from .runtime_session import atomic_write_json, is_reparse_point
from .schema import SchemaRegistry


_FINALIZED_FILES = {"evidence/manifest.json", "evidence/FINALIZED"}
_ROOT_ARTIFACTS = {"scenario.json", "requests.jsonl", "broker-events.jsonl"}
_SECRET_PATTERNS = (
    re.compile(rb"STS2_TEST_TOKEN\s*=", re.IGNORECASE),
    re.compile(rb'"(?:token|client_proof|server_proof|secret|credential)"\s*:', re.IGNORECASE),
)


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _redact_account_paths(value: Any) -> tuple[Any, int]:
    """Return a JSON-compatible deep copy with account-root paths redacted."""

    home = str(Path.home())
    home_forward = home.replace("\\", "/")
    removed = 0

    def visit(item: Any) -> Any:
        nonlocal removed
        if isinstance(item, Mapping):
            result: dict[str, Any] = {}
            for key, child in item.items():
                lowered = str(key).lower()
                if any(part in lowered for part in ("token", "proof", "secret", "credential")):
                    removed += 1
                    continue
                result[str(key)] = visit(child)
            return result
        if isinstance(item, list):
            return [visit(child) for child in item]
        if isinstance(item, tuple):
            return [visit(child) for child in item]
        if isinstance(item, str):
            redacted = item.replace(home, "<redacted>")
            redacted = redacted.replace(home_forward, "<redacted>")
            if redacted != item:
                removed += 1
            return redacted
        return item

    return visit(deepcopy(value)), removed


def _kind_for(relative: str) -> str:
    if relative.endswith(".jsonl"):
        return "jsonl"
    if relative.endswith(".json"):
        return "json"
    if relative.endswith(".log"):
        return "log"
    if relative.endswith(".mcr"):
        return "replay"
    return "binary"


def _producer_for(relative: str) -> str:
    if relative.startswith("instances/"):
        return "broker-or-companion"
    if relative.startswith("evidence/"):
        return "harness"
    return "broker"


def _aggregate_artifacts(artifacts: Sequence[Mapping[str, Any]]) -> str:
    digest = hashlib.sha256()
    for artifact in sorted(artifacts, key=lambda item: str(item["path"]).encode("utf-8")):
        record = f'{artifact["path"]}\0{artifact["sha256"]}\0{artifact["bytes"]}\n'
        digest.update(record.encode("utf-8"))
    return digest.hexdigest()


def evidence_metadata_template(
    *,
    case_id: str,
    result: str,
    scenario_sha256: str,
    harness_commit: str,
    game_version: str,
    game_executable_sha256: str,
    game_assembly_sha256: str,
    adapter_id: str,
    adapter_sha256: str,
    instances: Sequence[Mapping[str, Any]],
    seed: int,
    mods: Sequence[Mapping[str, Any]] = (),
    capabilities: Mapping[str, Any] | None = None,
    assertions: Sequence[Mapping[str, Any]] = (),
    started_at: str | None = None,
    ended_at: str | None = None,
    harness_version: str = "0.2.0",
    game_commit: str | None = None,
    rng_fingerprints: Sequence[Mapping[str, Any] | str] = (),
    fault_injection: bool = False,
) -> dict[str, Any]:
    """Build the non-artifact portion of an ``sts2-evidence/v1`` manifest."""

    now = utc_now()
    return {
        "schema": "sts2-evidence/v1",
        "case": {
            "id": case_id,
            "scenario_sha256": scenario_sha256.lower(),
            "started_at": started_at or now,
            "ended_at": ended_at or now,
            "result": result,
            "invalid": result == "invalid",
        },
        "software": {
            "harness_version": harness_version,
            "harness_commit": harness_commit,
            "python": platform.python_version(),
            "os": platform.system() or sys.platform,
        },
        "game": {
            "version": game_version,
            "commit": game_commit,
            "executable_sha256": game_executable_sha256.lower(),
            "assembly_sha256": game_assembly_sha256.lower(),
        },
        "mods": [dict(item) for item in mods],
        "adapter": {
            "id": adapter_id,
            "assembly_sha256": adapter_sha256.lower(),
            "capabilities": dict(capabilities or {}),
        },
        "instances": [dict(item) for item in instances],
        "determinism": {
            "seed": seed,
            "rng_fingerprints": list(rng_fingerprints),
            "fault_injection": bool(fault_injection),
        },
        "assertions": [dict(item) for item in assertions],
        "redaction": {"rules_version": 1, "removed_fields": 0},
    }


class EvidenceBundle:
    """Finalize and verify immutable evidence rooted in one control session."""

    def __init__(self, session_root: Path, *, schemas: SchemaRegistry | None = None) -> None:
        self.session_root = session_root.expanduser().resolve(strict=True)
        self.evidence_root = self.session_root / "evidence"
        self.manifest_path = self.evidence_root / "manifest.json"
        self.marker_path = self.evidence_root / "FINALIZED"
        self.schemas = schemas or SchemaRegistry()

    def _is_allowlisted(self, relative: str) -> bool:
        if relative in _FINALIZED_FILES:
            return False
        if relative in _ROOT_ARTIFACTS:
            return True
        if relative.startswith("evidence/"):
            return True
        parts = relative.split("/")
        if len(parts) < 3 or parts[0] != "instances":
            return False
        # Runtime-owned user data can contain saves, platform identifiers, and
        # Sentry installation data. Evidence includes only explicit per-instance
        # control artifacts and blob descriptors; a future save/replay command
        # must copy an intentional, redacted artifact into evidence/ first.
        return len(parts) == 3 or parts[2] == "blobs"

    def _iter_artifact_paths(self) -> Iterable[Path]:
        for path in sorted(self.session_root.rglob("*"), key=lambda item: item.as_posix().encode("utf-8")):
            relative = path.relative_to(self.session_root).as_posix()
            if is_reparse_point(path):
                if self._is_allowlisted(relative) or path.is_dir():
                    raise ProtocolFailure(
                        ErrorCode.ISOLATION_BREACH,
                        "evidence artifact traverses a symlink or reparse point",
                        details={"path": relative},
                    )
                continue
            if not path.is_file() or not self._is_allowlisted(relative):
                continue
            if path.name.endswith(".lock") or path.name.endswith(".tmp"):
                continue
            yield path

    @staticmethod
    def _assert_no_secret(path: Path) -> None:
        try:
            payload = path.read_bytes()
        except OSError as exc:
            raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, f"cannot read evidence artifact: {exc}") from exc
        for pattern in _SECRET_PATTERNS:
            if pattern.search(payload):
                raise ProtocolFailure(
                    ErrorCode.ISOLATION_BREACH,
                    "secret material is forbidden in evidence artifacts",
                    details={"path": path.name},
                )

    def _catalog(self, *, scan_secrets: bool) -> list[dict[str, Any]]:
        artifacts: list[dict[str, Any]] = []
        for path in self._iter_artifact_paths():
            if scan_secrets:
                self._assert_no_secret(path)
            relative = path.relative_to(self.session_root).as_posix()
            artifacts.append(
                {
                    "path": relative,
                    "kind": _kind_for(relative),
                    "bytes": path.stat().st_size,
                    "sha256": _sha256_file(path),
                    "producer": _producer_for(relative),
                }
            )
        return artifacts

    def finalize(self, metadata: Mapping[str, Any]) -> dict[str, Any]:
        if self.manifest_path.exists() or self.marker_path.exists():
            raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, "evidence bundle is already finalized")

        manifest, removed = _redact_account_paths(metadata)
        if not isinstance(manifest, dict):
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "evidence metadata must be an object")
        case = manifest.get("case")
        if isinstance(case, Mapping) and case.get("invalid") is True and case.get("result") == "passed":
            raise ProtocolFailure(
                ErrorCode.OBSERVER_OVERFLOW,
                "an invalid case cannot be finalized as passed",
            )
        manifest["schema"] = "sts2-evidence/v1"
        manifest["artifacts"] = self._catalog(scan_secrets=True)
        redaction = manifest.setdefault("redaction", {"rules_version": 1, "removed_fields": 0})
        if isinstance(redaction, dict):
            redaction["removed_fields"] = int(redaction.get("removed_fields", 0)) + removed
        manifest["aggregate_sha256"] = _aggregate_artifacts(manifest["artifacts"])
        self.schemas.validate("evidence-v1", manifest)

        self.evidence_root.mkdir(parents=True, exist_ok=True)
        atomic_write_json(self.manifest_path, manifest)
        manifest_sha256 = _sha256_file(self.manifest_path)
        descriptor, temporary_name = tempfile.mkstemp(
            prefix="FINALIZED.", suffix=".tmp", dir=self.evidence_root
        )
        temporary = Path(temporary_name)
        try:
            with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(manifest_sha256 + "\n")
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(temporary, self.marker_path)
        finally:
            if temporary.exists():
                temporary.unlink()
        return manifest

    def verify(self) -> dict[str, Any]:
        if not self.manifest_path.is_file() or not self.marker_path.is_file():
            raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, "evidence finalization marker is incomplete")
        try:
            expected_manifest_hash = self.marker_path.read_text(encoding="utf-8").strip().lower()
            manifest_bytes = self.manifest_path.read_bytes()
            manifest = json.loads(manifest_bytes)
        except (OSError, UnicodeError, json.JSONDecodeError) as exc:
            raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, f"invalid evidence manifest: {exc}") from exc
        actual_manifest_hash = hashlib.sha256(manifest_bytes).hexdigest()
        if expected_manifest_hash != actual_manifest_hash:
            raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, "evidence manifest hash does not match FINALIZED")
        try:
            self.schemas.validate("evidence-v1", manifest)
        except ProtocolFailure as exc:
            raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, str(exc), details=exc.details) from exc

        actual_artifacts = self._catalog(scan_secrets=True)
        expected_artifacts = manifest.get("artifacts")
        if expected_artifacts != actual_artifacts:
            raise ProtocolFailure(
                ErrorCode.EVIDENCE_TAMPERED,
                "evidence artifact catalog changed after finalization",
                details={"expected_count": len(expected_artifacts or []), "actual_count": len(actual_artifacts)},
            )
        actual_aggregate = _aggregate_artifacts(actual_artifacts)
        if manifest.get("aggregate_sha256") != actual_aggregate:
            raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, "evidence aggregate hash mismatch")
        return {
            "ok": True,
            "artifact_count": len(actual_artifacts),
            "aggregate_sha256": actual_aggregate,
            "manifest_sha256": actual_manifest_hash,
        }
