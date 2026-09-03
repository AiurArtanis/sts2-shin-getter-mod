from __future__ import annotations

import copy
import json
import os
from pathlib import Path

import pytest

from cli_anything.slaythespare2_111_beta.core.errors import ErrorCode, ProtocolFailure
from cli_anything.slaythespare2_111_beta.core.evidence import EvidenceBundle, evidence_metadata_template
from cli_anything.slaythespare2_111_beta.core.runtime_session import ControlSession, append_jsonl


def _session(tmp_path: Path) -> ControlSession:
    repository = tmp_path / "repo"
    repository.mkdir(parents=True)
    session = ControlSession.create(tmp_path / "runtime", "session-1", repository_root=repository)
    session.paths.scenario_json.write_text('{"schema":"sts2-scenario/v1"}\n', encoding="utf-8")
    append_jsonl(session.paths.requests_jsonl, {"type": "request", "request_id": "one"})
    return session


def _metadata() -> dict:
    return evidence_metadata_template(
        case_id="poc-0",
        result="passed",
        scenario_sha256="0" * 64,
        harness_commit="1" * 40,
        game_version="0.111.0",
        game_executable_sha256="2" * 64,
        game_assembly_sha256="3" * 64,
        adapter_id="sts2-0.111",
        adapter_sha256="4" * 64,
        instances=[
            {
                "id": "solo",
                "role": "single",
                "pid": 42,
                "process_start_time_utc": "2026-09-03T00:00:00Z",
                "driver": "headless",
                "user_data_root": "<redacted>/solo",
            }
        ],
        seed=424242,
    )


def test_evidence_finalize_writes_manifest_and_marker(tmp_path: Path) -> None:
    session = _session(tmp_path)
    manifest = EvidenceBundle(session.paths.root).finalize(_metadata())
    assert manifest["schema"] == "sts2-evidence/v1"
    assert session.paths.evidence.joinpath("manifest.json").is_file()
    assert session.paths.evidence.joinpath("FINALIZED").is_file()


def test_evidence_manifest_uses_relative_paths(tmp_path: Path) -> None:
    session = _session(tmp_path)
    manifest = EvidenceBundle(session.paths.root).finalize(_metadata())
    assert all(not Path(item["path"]).is_absolute() for item in manifest["artifacts"])


def test_evidence_manifest_records_hash_and_size(tmp_path: Path) -> None:
    session = _session(tmp_path)
    manifest = EvidenceBundle(session.paths.root).finalize(_metadata())
    request = next(item for item in manifest["artifacts"] if item["path"] == "requests.jsonl")
    assert request["bytes"] == session.paths.requests_jsonl.stat().st_size
    assert len(request["sha256"]) == 64


def test_evidence_verify_accepts_unchanged_bundle(tmp_path: Path) -> None:
    session = _session(tmp_path)
    bundle = EvidenceBundle(session.paths.root)
    bundle.finalize(_metadata())
    result = bundle.verify()
    assert result["ok"] is True
    assert result["artifact_count"] >= 3


def test_evidence_verify_detects_single_byte_tamper(tmp_path: Path) -> None:
    session = _session(tmp_path)
    bundle = EvidenceBundle(session.paths.root)
    bundle.finalize(_metadata())
    with session.paths.requests_jsonl.open("ab") as handle:
        handle.write(b"x")
    with pytest.raises(ProtocolFailure) as failure:
        bundle.verify()
    assert failure.value.code == ErrorCode.EVIDENCE_TAMPERED


def test_evidence_verify_detects_new_allowlisted_artifact(tmp_path: Path) -> None:
    session = _session(tmp_path)
    bundle = EvidenceBundle(session.paths.root)
    bundle.finalize(_metadata())
    instance = session.paths.instances / "solo"
    instance.mkdir()
    (instance / "stdout.log").write_text("late", encoding="utf-8")
    with pytest.raises(ProtocolFailure) as failure:
        bundle.verify()
    assert failure.value.code == ErrorCode.EVIDENCE_TAMPERED


def test_evidence_finalize_rejects_second_publication(tmp_path: Path) -> None:
    session = _session(tmp_path)
    bundle = EvidenceBundle(session.paths.root)
    bundle.finalize(_metadata())
    with pytest.raises(ProtocolFailure) as failure:
        bundle.finalize(_metadata())
    assert failure.value.code == ErrorCode.EVIDENCE_TAMPERED


def test_evidence_finalize_rejects_pass_for_invalid_case(tmp_path: Path) -> None:
    session = _session(tmp_path)
    metadata = _metadata()
    metadata["case"]["result"] = "passed"
    metadata["case"]["invalid"] = True
    with pytest.raises(ProtocolFailure) as failure:
        EvidenceBundle(session.paths.root).finalize(metadata)
    assert failure.value.code == ErrorCode.OBSERVER_OVERFLOW


@pytest.mark.skipif(not hasattr(os, "symlink"), reason="platform has no symlink API")
def test_evidence_finalize_rejects_symlink_artifact(tmp_path: Path) -> None:
    session = _session(tmp_path)
    outside = tmp_path / "outside.txt"
    outside.write_text("outside", encoding="utf-8")
    instance = session.paths.instances / "solo"
    instance.mkdir()
    link = instance / "stdout.log"
    try:
        os.symlink(outside, link)
    except OSError as exc:
        pytest.skip(f"symlink creation unavailable: {exc}")
    with pytest.raises(ProtocolFailure) as failure:
        EvidenceBundle(session.paths.root).finalize(_metadata())
    assert failure.value.code == ErrorCode.ISOLATION_BREACH


@pytest.mark.parametrize(
    "secret",
    [
        b'STS2_TEST_TOKEN=secret\n',
        b'{"token":"secret"}\n',
        b'{"client_proof":"abc"}\n',
        b'{"server_proof":"abc"}\n',
    ],
)
def test_evidence_finalize_rejects_secret_material(tmp_path: Path, secret: bytes) -> None:
    session = _session(tmp_path)
    session.paths.requests_jsonl.write_bytes(secret)
    with pytest.raises(ProtocolFailure) as failure:
        EvidenceBundle(session.paths.root).finalize(_metadata())
    assert failure.value.code == ErrorCode.ISOLATION_BREACH


def test_evidence_metadata_redacts_account_path(tmp_path: Path) -> None:
    metadata = _metadata()
    metadata["instances"][0]["user_data_root"] = str(Path.home() / "secret-user" / "solo")
    session = _session(tmp_path)
    manifest = EvidenceBundle(session.paths.root).finalize(metadata)
    serialized = json.dumps(manifest)
    assert str(Path.home()) not in serialized
    assert "<redacted>" in serialized


def test_evidence_artifact_aggregate_is_order_stable(tmp_path: Path) -> None:
    first = _session(tmp_path / "a")
    second = _session(tmp_path / "b")
    first_manifest = EvidenceBundle(first.paths.root).finalize(_metadata())
    second_manifest = EvidenceBundle(second.paths.root).finalize(_metadata())
    assert first_manifest["aggregate_sha256"] == second_manifest["aggregate_sha256"]


def test_evidence_verify_rejects_manifest_edit(tmp_path: Path) -> None:
    session = _session(tmp_path)
    bundle = EvidenceBundle(session.paths.root)
    bundle.finalize(_metadata())
    manifest_path = session.paths.evidence / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["case"]["result"] = "failed"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    with pytest.raises(ProtocolFailure) as failure:
        bundle.verify()
    assert failure.value.code == ErrorCode.EVIDENCE_TAMPERED


def test_evidence_schema_rejects_incomplete_metadata(tmp_path: Path) -> None:
    session = _session(tmp_path)
    metadata = copy.deepcopy(_metadata())
    del metadata["game"]
    with pytest.raises(ProtocolFailure) as failure:
        EvidenceBundle(session.paths.root).finalize(metadata)
    assert failure.value.code == ErrorCode.INVALID_ARGUMENT
