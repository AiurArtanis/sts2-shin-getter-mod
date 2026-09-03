from __future__ import annotations

import json
import os
import shutil
import signal
import subprocess
import time
from pathlib import Path

import pytest

from cli_anything.slaythespare2_111_beta.core.broker_client import BrokerClient
from cli_anything.slaythespare2_111_beta.core.errors import ErrorCode, ProtocolFailure
from cli_anything.slaythespare2_111_beta.core.evidence import evidence_metadata_template
from cli_anything.slaythespare2_111_beta.core.legacy import FileLock, HarnessError
from cli_anything.slaythespare2_111_beta.core.process_manager import (
    ExactProcessManager,
    ProcessRecord,
    capture_process_identity,
)
from cli_anything.slaythespare2_111_beta.core.runtime_session import ControlSession


pytestmark = pytest.mark.component


@pytest.fixture(scope="session")
def broker_component_host_dll(harness_root: Path) -> Path:
    project = (
        harness_root
        / "bridge"
        / "Sts2HeadlessTestBridge"
        / "tests"
        / "ComponentHost"
        / "ComponentHost.csproj"
    )
    completed = subprocess.run(
        ["dotnet", "build", str(project), "--configuration", "Release", "--nologo", "--verbosity", "minimal"],
        cwd=harness_root,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        timeout=180,
        check=False,
    )
    assert completed.returncode == 0, completed.stdout
    dll = project.parent / "bin" / "Release" / "net9.0" / "ComponentHost.dll"
    assert dll.is_file()
    return dll


@pytest.fixture
def broker_fixture(tmp_path: Path, broker_component_host_dll: Path):
    repository = tmp_path / "repository"
    repository.mkdir()
    session = ControlSession.create(tmp_path / "runtime", "broker-test", repository_root=repository)
    client = BrokerClient.bootstrap(session, repository_root=repository, timeout_seconds=15)
    fixture = {
        "client": client,
        "session": session,
        "repository": repository,
        "component_dll": broker_component_host_dll,
    }
    yield fixture
    try:
        client.request("session.close", {})
    except ProtocolFailure:
        pass


def _start_component(fixture: dict) -> dict:
    return fixture["client"].request(
        "process.start",
        {
            "instance_id": "solo",
            "role": "single",
            "argv": [shutil.which("dotnet") or "dotnet", str(fixture["component_dll"])],
            "cwd": str(fixture["session"].paths.root),
            "adapter_expected": "component-test-host",
            "timeout_seconds": 15,
        },
    )


def test_broker_publishes_verifiable_public_identity(broker_fixture: dict) -> None:
    session = broker_fixture["session"]
    identity = json.loads(session.paths.broker_json.read_text(encoding="utf-8"))
    assert identity["pid"] == broker_fixture["client"].broker_identity.pid
    assert identity["control_pipe"].startswith("sts2-broker-")
    assert not any(part in key.lower() for key in identity for part in ("token", "proof", "secret"))


def test_broker_holds_exclusive_session_lease(broker_fixture: dict) -> None:
    with pytest.raises(HarnessError):
        with FileLock(broker_fixture["session"].paths.session_lock, timeout=0.1):
            pass


def test_broker_component_start_authenticates_and_records_runtime(broker_fixture: dict) -> None:
    result = _start_component(broker_fixture)
    session = broker_fixture["session"]
    assert result["handshake"]["adapter"]["id"] == "component-test-host"
    assert result["process"]["state"] == "ready"
    runtime = json.loads((session.paths.instances / "solo" / "runtime.json").read_text(encoding="utf-8"))
    assert runtime["adapter"]["id"] == "component-test-host"


def test_broker_never_persists_companion_secret(broker_fixture: dict) -> None:
    _start_component(broker_fixture)
    values = []
    for path in broker_fixture["session"].paths.root.rglob("*"):
        if path.is_file() and path.suffix in {".json", ".jsonl", ".log"}:
            values.append(path.read_text(encoding="utf-8", errors="replace"))
    persisted = "\n".join(values).lower()
    assert "sts2_test_token" not in persisted
    assert '"client_proof"' not in persisted
    assert '"server_proof"' not in persisted


def test_broker_runtime_ping_uses_owned_long_connection(broker_fixture: dict) -> None:
    _start_component(broker_fixture)
    result = broker_fixture["client"].request(
        "runtime.exec",
        {"instance_id": "solo", "command": "runtime.ping", "args": {}, "wait_for": "immediate"},
    )
    assert result["type"] == "completed"
    assert result["result"]["backend"] == "component_test_host"


def test_broker_process_status_and_stop_are_exact(broker_fixture: dict) -> None:
    started = _start_component(broker_fixture)
    status = broker_fixture["client"].request("process.status", {"instance_id": "solo"})
    assert status["alive"] is True
    assert status["pid"] == started["process"]["pid"]
    stopped = broker_fixture["client"].request(
        "process.stop", {"instance_id": "solo", "grace_seconds": 2.0, "force": True}
    )
    assert stopped["state"] == "exited"


def test_broker_lists_only_its_session_instances(broker_fixture: dict) -> None:
    _start_component(broker_fixture)
    result = broker_fixture["client"].request("process.list", {})
    assert [item["instance_id"] for item in result["instances"]] == ["solo"]


def test_broker_finalizes_and_verifies_evidence_without_post_write(broker_fixture: dict) -> None:
    _start_component(broker_fixture)
    broker_fixture["client"].request(
        "process.stop", {"instance_id": "solo", "grace_seconds": 2.0, "force": True}
    )
    session = broker_fixture["session"]
    session.paths.scenario_json.write_text('{"schema":"sts2-scenario/v1"}\n', encoding="utf-8")
    metadata = evidence_metadata_template(
        case_id="poc-0-component",
        result="passed",
        scenario_sha256="0" * 64,
        harness_commit="1" * 40,
        game_version="0.111.0-component",
        game_executable_sha256="2" * 64,
        game_assembly_sha256="3" * 64,
        adapter_id="component-test-host",
        adapter_sha256="4" * 64,
        instances=[
            {
                "id": "solo",
                "role": "single",
                "pid": 42,
                "process_start_time_utc": "2026-09-03T00:00:00Z",
                "driver": "component",
                "user_data_root": "<redacted>/solo",
            }
        ],
        seed=424242,
    )
    finalized = broker_fixture["client"].request("evidence.finalize", {"metadata": metadata})
    verified = broker_fixture["client"].request("evidence.verify", {})
    assert finalized["aggregate_sha256"] == verified["aggregate_sha256"]
    assert verified["ok"] is True


def test_dead_broker_is_not_replaced_or_allowed_to_adopt_child(broker_fixture: dict) -> None:
    started = _start_component(broker_fixture)
    child = ProcessRecord.from_dict(started["process"])
    broker = broker_fixture["client"].broker_identity
    os.kill(broker.pid, signal.SIGTERM)
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        try:
            capture_process_identity(broker.pid)
        except ProcessLookupError:
            break
        time.sleep(0.05)
    with pytest.raises(ProtocolFailure) as failure:
        BrokerClient.from_session(broker_fixture["session"]).request("process.status", {"instance_id": "solo"})
    assert failure.value.code == ErrorCode.BROKER_EXIT

    # Closing the broker's Job Object must make the game/component unable to
    # survive as an orphan. Retain an exact-identity cleanup as a test safety net.
    deadline = time.monotonic() + 10
    alive = True
    while time.monotonic() < deadline:
        try:
            capture_process_identity(child.identity.pid)
        except ProcessLookupError:
            alive = False
            break
        time.sleep(0.05)
    if alive:
        ExactProcessManager().stop(child.identity, grace_seconds=0.1, force=True)
    assert alive is False


def test_broker_rejects_second_process_for_same_instance(broker_fixture: dict) -> None:
    _start_component(broker_fixture)
    with pytest.raises(ProtocolFailure) as failure:
        _start_component(broker_fixture)
    assert failure.value.code == ErrorCode.INVALID_ARGUMENT
