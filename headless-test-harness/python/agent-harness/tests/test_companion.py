from __future__ import annotations

import base64
import json
import os
import secrets
import subprocess
import sys
import time
from pathlib import Path

import pytest

from cli_anything.slaythespare2_111_beta.core.companion_client import CompanionClient
from cli_anything.slaythespare2_111_beta.core.errors import ErrorCode, ProtocolFailure


pytestmark = pytest.mark.component


@pytest.fixture(scope="session")
def component_host_dll(harness_root: Path) -> Path:
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
def component_host(component_host_dll: Path, tmp_path: Path):
    token = secrets.token_bytes(32)
    pipe_name = f"sts2-test-component-{secrets.token_hex(8)}"
    environment = os.environ.copy()
    environment.update(
        {
            "STS2_TEST_TOKEN": base64.urlsafe_b64encode(token).decode("ascii").rstrip("="),
            "STS2_TEST_SESSION_ID": "component-session",
            "STS2_TEST_INSTANCE_ID": "solo",
            "STS2_TEST_PIPE": pipe_name,
            "STS2_TEST_OUTPUT_ROOT": str(tmp_path),
        }
    )
    creationflags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    process = subprocess.Popen(
        ["dotnet", str(component_host_dll)],
        cwd=tmp_path,
        env=environment,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        creationflags=creationflags,
    )
    yield {"process": process, "token": token, "pipe_name": pipe_name, "root": tmp_path}
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
    if process.stdout:
        process.stdout.close()
    if process.stderr:
        process.stderr.close()


def _client(component_host: dict, *, token: bytes | None = None) -> CompanionClient:
    client = CompanionClient(
        pipe_name=component_host["pipe_name"],
        session_id="component-session",
        instance_id="solo",
        token=token or component_host["token"],
        expected_adapter_id="component-test-host",
    )
    client.connect(timeout_seconds=10)
    return client


def test_component_host_authenticated_ping(component_host: dict) -> None:
    with _client(component_host) as client:
        terminal = client.request("runtime.ping", {}, wait_for="immediate")
    assert terminal["type"] == "completed"
    assert terminal["result"]["backend"] == "component_test_host"


def test_component_host_reports_tri_state_capabilities(component_host: dict) -> None:
    with _client(component_host) as client:
        capabilities = client.hello_ack_body["capabilities"]
    assert capabilities["typed_card_play"]["state"] == "partial"
    assert capabilities["pixel_output"]["state"] == "unavailable"


def test_component_host_rejects_wrong_client_token(component_host: dict) -> None:
    client = CompanionClient(
        pipe_name=component_host["pipe_name"],
        session_id="component-session",
        instance_id="solo",
        token=b"wrong" * 8,
    )
    with pytest.raises(ProtocolFailure) as failure:
        client.connect(timeout_seconds=10)
    assert failure.value.code == ErrorCode.AUTH_FAILED


def test_component_host_rejects_unknown_command(component_host: dict) -> None:
    with _client(component_host) as client:
        terminal = client.request("unknown.command", {})
    assert terminal["type"] == "failed"
    assert terminal["error"]["code"] == ErrorCode.INVALID_ARGUMENT.value


def test_component_host_choice_parent_remains_inflight(component_host: dict) -> None:
    with _client(component_host) as client:
        parent = client.dispatch("test.choice_parent", {}, wait_for="queue_settled")
        event = client.wait_event(parent, "choice_required")
        assert client.request_status(parent)["phase"] == "event"
        choice = client.request(
            "choice.select",
            {
                "blocked_request_id": parent,
                "owner_id": event["data"]["owner_id"],
                "choice_handle": event["data"]["choice_handle"],
                "choice_generation": event["data"]["choice_generation"],
                "candidates": [event["data"]["candidates"][0]["handle"]],
            },
        )
        parent_terminal = client.wait_terminal(parent)
    assert choice["type"] == "completed"
    assert parent_terminal["result"]["completion"] == "queue_settled"


def test_component_host_rejects_unrelated_mutation_while_choice_waits(component_host: dict) -> None:
    with _client(component_host) as client:
        parent = client.dispatch("test.choice_parent", {}, wait_for="queue_settled")
        client.wait_event(parent, "choice_required")
        terminal = client.request("test.mutation", {})
    assert terminal["type"] == "failed"
    assert terminal["error"]["code"] == ErrorCode.MUTATION_BUSY.value


def test_component_host_rejects_stale_choice(component_host: dict) -> None:
    with _client(component_host) as client:
        parent = client.dispatch("test.choice_parent", {}, wait_for="queue_settled")
        event = client.wait_event(parent, "choice_required")
        terminal = client.request(
            "choice.select",
            {
                "blocked_request_id": "wrong-parent",
                "owner_id": event["data"]["owner_id"],
                "choice_handle": event["data"]["choice_handle"],
                "choice_generation": event["data"]["choice_generation"],
                "candidates": [event["data"]["candidates"][0]["handle"]],
            },
        )
    assert terminal["type"] == "failed"
    assert terminal["error"]["code"] == ErrorCode.STALE_HANDLE.value


def test_component_host_snapshot_query_passes_mutation_lane(component_host: dict) -> None:
    with _client(component_host) as client:
        parent = client.dispatch("test.choice_parent", {}, wait_for="queue_settled")
        client.wait_event(parent, "choice_required")
        terminal = client.request("runtime.ping", {})
    assert terminal["type"] == "completed"


def test_component_host_reconnect_replays_unread_critical_events(component_host: dict) -> None:
    client = _client(component_host)
    request_id = client.send_only("runtime.ping", {})
    client.close()
    time.sleep(0.2)
    client.connect(timeout_seconds=10, resume_from_seq=0)
    terminal = client.wait_terminal(request_id)
    client.close()
    assert terminal["type"] == "completed"
    assert terminal["request_id"] == request_id


def test_component_host_replayed_request_is_idempotent(component_host: dict) -> None:
    with _client(component_host) as client:
        request_id = client.new_request_id()
        first = client.request("runtime.ping", {}, request_id=request_id)
        replay = client.request("runtime.ping", {}, request_id=request_id)
    assert first["type"] == "completed"
    assert replay["type"] == "completed"
    assert replay["replayed"] is True


def test_component_host_same_id_different_payload_conflicts(component_host: dict) -> None:
    with _client(component_host) as client:
        request_id = client.new_request_id()
        client.request("runtime.ping", {"value": 1}, request_id=request_id)
        conflict = client.request("runtime.ping", {"value": 2}, request_id=request_id)
    assert conflict["type"] == "failed"
    assert conflict["error"]["code"] == ErrorCode.IDEMPOTENCY_CONFLICT.value


def test_component_host_shutdown_is_explicit(component_host: dict) -> None:
    client = _client(component_host)
    terminal = client.request("runtime.shutdown", {}, wait_for="immediate")
    client.close()
    component_host["process"].wait(timeout=5)
    assert terminal["type"] == "completed"
    assert component_host["process"].returncode == 0
