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


@pytest.fixture
def constrained_component_host(component_host_dll: Path, tmp_path: Path):
    token = secrets.token_bytes(32)
    pipe_name = f"sts2-test-component-constrained-{secrets.token_hex(8)}"
    environment = os.environ.copy()
    environment.update(
        {
            "STS2_TEST_TOKEN": base64.urlsafe_b64encode(token).decode("ascii").rstrip("="),
            "STS2_TEST_SESSION_ID": "component-session",
            "STS2_TEST_INSTANCE_ID": "solo",
            "STS2_TEST_PIPE": pipe_name,
            "STS2_TEST_OUTPUT_ROOT": str(tmp_path),
            "STS2_TEST_COMPONENT_MAX_LINE_BYTES": "4096",
            "STS2_TEST_COMPONENT_REPLAY_CAPACITY": "8",
        }
    )
    process = subprocess.Popen(
        ["dotnet", str(component_host_dll)],
        cwd=tmp_path,
        env=environment,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
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


@pytest.fixture
def blocked_writer_component_host(component_host_dll: Path, tmp_path: Path):
    token = secrets.token_bytes(32)
    pipe_name = f"sts2-test-component-overflow-{secrets.token_hex(8)}"
    release_file = tmp_path / "release-writer"
    environment = os.environ.copy()
    environment.update(
        {
            "STS2_TEST_TOKEN": base64.urlsafe_b64encode(token).decode("ascii").rstrip("="),
            "STS2_TEST_SESSION_ID": "component-session",
            "STS2_TEST_INSTANCE_ID": "solo",
            "STS2_TEST_PIPE": pipe_name,
            "STS2_TEST_OUTPUT_ROOT": str(tmp_path),
            "STS2_TEST_COMPONENT_REPLAY_CAPACITY": "64",
            "STS2_TEST_COMPONENT_OUTBOUND_CAPACITY": "1",
            "STS2_TEST_COMPONENT_WRITER_RELEASE_FILE": str(release_file),
        }
    )
    process = subprocess.Popen(
        ["dotnet", str(component_host_dll)],
        cwd=tmp_path,
        env=environment,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
    )
    yield {
        "process": process,
        "token": token,
        "pipe_name": pipe_name,
        "root": tmp_path,
        "release_file": release_file,
    }
    release_file.touch(exist_ok=True)
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


def _client(
    component_host: dict,
    *,
    token: bytes | None = None,
    resume_from_seq: int | None = None,
) -> CompanionClient:
    client = CompanionClient(
        pipe_name=component_host["pipe_name"],
        session_id="component-session",
        instance_id="solo",
        token=token or component_host["token"],
        expected_adapter_id="component-test-host",
    )
    client.connect(timeout_seconds=10, resume_from_seq=resume_from_seq)
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


def test_component_host_action_parent_uses_exact_reference_and_full_queue_settle(component_host: dict) -> None:
    with _client(component_host) as client:
        parent = client.dispatch("test.action_parent", {}, wait_for="queue_settled")
        enqueued = client.wait_event(parent, "action_enqueued")
        assert enqueued["data"]["correlation"] == "exact_reference"
        assert enqueued["data"]["action_id"] == 7

        busy = client.request("test.mutation", {})
        assert busy["type"] == "failed"
        assert busy["error"]["code"] == ErrorCode.MUTATION_BUSY.value

        continuation = client.request(
            "test.action_complete",
            {
                "blocked_request_id": parent,
                "action_handle": enqueued["data"]["action_handle"],
            },
        )
        finished = client.wait_event(parent, "action_finished")
        parent_terminal = client.wait_terminal(parent)

    assert continuation["type"] == "completed"
    assert continuation["result"]["released"] is True
    assert enqueued["seq"] < finished["seq"] < parent_terminal["seq"]
    assert finished["data"]["action_handle"] == enqueued["data"]["action_handle"]
    assert parent_terminal["result"]["completion"] == "queue_settled"
    assert parent_terminal["result"]["queue_empty"] is True
    assert parent_terminal["result"]["executor_running"] is False


def test_component_host_wrong_action_reference_cannot_release_mutation_lane(component_host: dict) -> None:
    with _client(component_host) as client:
        parent = client.dispatch("test.action_parent", {}, wait_for="queue_settled")
        enqueued = client.wait_event(parent, "action_enqueued")
        stale = client.request(
            "test.action_complete",
            {"blocked_request_id": parent, "action_handle": "action:component:wrong"},
        )
        assert stale["type"] == "failed"
        assert stale["error"]["code"] == ErrorCode.STALE_HANDLE.value
        assert client.request_status(parent)["terminal"] is None

        client.request(
            "test.action_complete",
            {
                "blocked_request_id": parent,
                "action_handle": enqueued["data"]["action_handle"],
            },
        )
        parent_terminal = client.wait_terminal(parent)

    assert parent_terminal["type"] == "completed"


def test_game_bridge_action_completion_has_no_sleep_or_nearest_action_fallback(harness_root: Path) -> None:
    dispatch_root = harness_root / "bridge" / "Sts2HeadlessTestBridge" / "src" / "Dispatch"
    observer = (dispatch_root / "ActionObserver.cs").read_text(encoding="utf-8")
    execution = (dispatch_root / "RequestExecution.cs").read_text(encoding="utf-8")
    registry = (dispatch_root / "CommandRegistry.cs").read_text(encoding="utf-8")
    combined = "\n".join((observer, execution, registry))

    assert "CompletionTask" in combined
    assert "ActionQueueSet.IsEmpty" in combined
    assert "ActionExecutor.IsRunning" in combined
    assert "CurrentlyRunningAction" in combined
    assert "RequestEnqueue(action)" in combined
    assert "ReadyPredicate" in combined
    assert "PlayerTurnPhase.Play" in combined
    assert "Task.Delay(" not in combined
    assert "Thread.Sleep(" not in combined
    assert "nearest action" not in combined.lower()
    assert "recent action" not in combined.lower()


def test_game_bridge_and_component_host_share_production_idempotency_gate(harness_root: Path) -> None:
    bridge = harness_root / "bridge" / "Sts2HeadlessTestBridge"
    gate = bridge / "src" / "Dispatch" / "RequestIdempotencyGate.cs"
    execution = (bridge / "src" / "Dispatch" / "RequestExecution.cs").read_text(encoding="utf-8")
    component = (bridge / "tests" / "ComponentHost" / "Program.cs").read_text(encoding="utf-8")
    project = (bridge / "tests" / "ComponentHost" / "ComponentHost.csproj").read_text(encoding="utf-8")
    assert gate.is_file()
    assert "RequestIdempotencyGate" in execution
    assert "RequestIdempotencyGate" in component
    assert "src/Dispatch/RequestIdempotencyGate.cs" in project.replace("\\", "/")


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


@pytest.mark.parametrize(
    ("field", "replacement"),
    [
        ("owner_id", 999),
        ("choice_generation", 999),
        ("choice_handle", "choice:stale"),
        ("candidates", ["choice-item:stale:0"]),
    ],
)
def test_component_host_rejects_each_stale_choice_identity_field(
    component_host: dict,
    field: str,
    replacement: object,
) -> None:
    with _client(component_host) as client:
        parent = client.dispatch("test.choice_parent", {}, wait_for="queue_settled")
        event = client.wait_event(parent, "choice_required")
        selection = {
            "blocked_request_id": parent,
            "owner_id": event["data"]["owner_id"],
            "choice_handle": event["data"]["choice_handle"],
            "choice_generation": event["data"]["choice_generation"],
            "candidates": [event["data"]["candidates"][0]["handle"]],
        }
        selection[field] = replacement
        terminal = client.request("choice.select", selection)
    assert terminal["type"] == "failed"
    assert terminal["error"]["code"] == ErrorCode.STALE_HANDLE.value


def test_game_bridge_choice_broker_uses_local_selector_and_server_handles(harness_root: Path) -> None:
    source = (
        harness_root
        / "bridge"
        / "Sts2HeadlessTestBridge"
        / "src"
        / "Dispatch"
        / "ChoiceBroker.cs"
    ).read_text(encoding="utf-8")
    assert "ICardSelector" in source
    assert "CardSelectCmd.UseSelector" in source
    assert "localOnly: true" in source
    assert "TaskCompletionSource<IEnumerable<CardModel>>" in source
    assert "blocked_request_id" in source
    assert "choice_generation" in source
    assert "InvalidateParent" in source
    assert "Task.Delay(" not in source
    assert "Thread.Sleep(" not in source


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


def test_component_host_reconnect_routes_future_inflight_terminal_to_new_pipe(component_host: dict) -> None:
    client = _client(component_host)
    request_id = client.dispatch("test.delayed", {"delay_ms": 500}, wait_for="immediate")
    resume_from = client.last_seq
    client.connect(timeout_seconds=10, resume_from_seq=resume_from)
    terminal = client.wait_terminal(request_id, timeout_seconds=5)
    client.close()
    assert terminal["type"] == "completed"
    assert terminal["request_id"] == request_id
    assert terminal["result"]["delayed"] is True


def test_component_host_same_payload_duplicate_stays_single_inflight_execution(component_host: dict) -> None:
    with _client(component_host) as client:
        request_id = client.dispatch("test.delayed", {"delay_ms": 400}, request_id="duplicate-inflight")
        duplicate = client.dispatch(
            "test.delayed",
            {"delay_ms": 400},
            request_id=request_id,
            timeout_ms=10_000,
        )
        terminal = client.wait_terminal(request_id, timeout_seconds=5)
    assert duplicate == request_id
    assert terminal["type"] == "completed"
    assert terminal["result"]["execution_count"] == 1


def test_component_host_replayed_request_is_idempotent(component_host: dict) -> None:
    with _client(component_host) as client:
        request_id = client.new_request_id()
        first = client.request("runtime.ping", {}, request_id=request_id)
        replay = client.request("runtime.ping", {}, request_id=request_id)
    assert first["type"] == "completed"
    assert replay["type"] == "completed"
    assert replay["replayed"] is True


def test_component_host_same_id_different_payload_conflicts(component_host: dict) -> None:
    request_id = "server-side-payload-conflict"
    with _client(component_host) as client:
        client.request("runtime.ping", {"value": 1}, request_id=request_id)
        resume_from_seq = client.last_seq
    # A fresh client has no local request ledger, so this assertion exercises
    # the production C# process-epoch gate rather than the Python preflight.
    # Resume after the original terminal so handshake replay cannot be mistaken
    # for the response to the deliberately conflicting request.
    with _client(component_host, resume_from_seq=resume_from_seq) as fresh_client:
        conflict = fresh_client.request("runtime.ping", {"value": 2}, request_id=request_id)
    assert conflict["type"] == "failed"
    assert conflict["error"]["code"] == ErrorCode.IDEMPOTENCY_CONFLICT.value


def test_component_client_rejects_conflicting_retry_before_delayed_second_write(
    component_host: dict,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    with _client(component_host) as client:
        request_id = "client-preflight-conflict-race"
        original_write = client._write_to_stream
        request_write_count = 0

        def delay_second_request_write(stream: object, message: dict) -> None:
            nonlocal request_write_count
            if message.get("type") == "request" and message.get("request_id") == request_id:
                request_write_count += 1
                if request_write_count == 2:
                    time.sleep(0.45)
            original_write(stream, message)

        monkeypatch.setattr(client, "_write_to_stream", delay_second_request_write)
        client.dispatch(
            "test.delayed",
            {"delay_ms": 250},
            request_id=request_id,
            wait_for="immediate",
            timeout_ms=10_000,
        )
        with pytest.raises(ProtocolFailure) as failure:
            client.request(
                "test.delayed",
                {"delay_ms": 450},
                request_id=request_id,
                wait_for="immediate",
                timeout_ms=10_000,
            )
        first_terminal = client.wait_terminal(request_id, timeout_seconds=5)

    assert failure.value.code == ErrorCode.IDEMPOTENCY_CONFLICT
    assert request_write_count == 1
    assert first_terminal["type"] == "completed"
    assert first_terminal["result"]["execution_count"] == 1


def test_component_host_completed_id_never_reexecutes_after_terminal_cache_capacity(
    component_host: dict,
) -> None:
    with _client(component_host) as client:
        request_id = "completed-before-idempotency-window"
        first = client.request(
            "test.delayed",
            {"delay_ms": 1},
            request_id=request_id,
            timeout_ms=10_000,
        )
        for index in range(256):
            terminal = client.request(
                "runtime.ping",
                {"fill": index},
                request_id=f"idempotency-fill-{index}",
            )
            assert terminal["type"] == "completed"
        repeated = client.request(
            "test.delayed",
            {"delay_ms": 1},
            request_id=request_id,
            timeout_ms=10_000,
        )
        resume_from_seq = client.last_seq

    assert first["result"]["execution_count"] == 1
    assert repeated["type"] == "failed"
    assert repeated["error"]["code"] == "E_IDEMPOTENCY_WINDOW_EXPIRED"
    with _client(component_host, resume_from_seq=resume_from_seq) as fresh_client:
        conflict = fresh_client.request(
            "test.delayed",
            {"delay_ms": 2},
            request_id=request_id,
            timeout_ms=10_000,
        )
    assert conflict["type"] == "failed"
    assert conflict["error"]["code"] == ErrorCode.IDEMPOTENCY_CONFLICT.value


def test_wait_event_fails_when_request_is_already_terminal(component_host: dict) -> None:
    with _client(component_host) as client:
        request_id = client.new_request_id()
        client.request("runtime.ping", {}, request_id=request_id)
        with pytest.raises(ProtocolFailure) as failure:
            client.wait_event(request_id, "event_that_cannot_arrive", timeout_seconds=0.5)
    assert failure.value.code == ErrorCode.INVALID_PHASE


@pytest.mark.parametrize(
    "payload",
    [
        b'{not-json}\n',
        b'{"oversized":"' + (b"x" * 8192) + b'"}\n',
    ],
)
def test_component_host_bad_frame_isolated_and_next_authenticated_ping_succeeds(
    constrained_component_host: dict,
    payload: bytes,
) -> None:
    client = _client(constrained_component_host)
    stream = client._stream
    assert stream is not None
    stream.write(payload)
    stream.flush()
    time.sleep(0.15)
    client.close()

    with _client(constrained_component_host) as recovered:
        terminal = recovered.request("runtime.ping", {}, wait_for="immediate")
    assert terminal["type"] == "completed"


def test_component_host_rejects_expired_resume_window(constrained_component_host: dict) -> None:
    client = _client(constrained_component_host)
    for _ in range(4):
        terminal = client.request("runtime.ping", {})
        assert terminal["type"] == "completed"
    client.close()

    with pytest.raises(ProtocolFailure) as failure:
        client.connect(timeout_seconds=10, resume_from_seq=0)
    assert failure.value.code == ErrorCode.RESUME_WINDOW_EXPIRED


def test_component_host_blocked_live_writer_latches_critical_overflow_and_freezes_mutation(
    blocked_writer_component_host: dict,
) -> None:
    with _client(blocked_writer_component_host) as client:
        request_id = client.send_only("runtime.ping", {})
        time.sleep(0.2)
        blocked_writer_component_host["release_file"].touch()
        terminal = client.wait_terminal(request_id, timeout_seconds=5)
        frozen = client.request("test.mutation", {}, timeout_ms=2_000)
    assert terminal["type"] == "failed"
    assert terminal["error"]["code"] == ErrorCode.OBSERVER_OVERFLOW.value
    assert frozen["type"] == "failed"
    assert frozen["error"]["code"] == ErrorCode.OBSERVER_OVERFLOW.value


def test_component_host_shutdown_is_explicit(component_host: dict) -> None:
    client = _client(component_host)
    terminal = client.request("runtime.shutdown", {}, wait_for="immediate")
    client.close()
    component_host["process"].wait(timeout=5)
    assert terminal["type"] == "completed"
    assert component_host["process"].returncode == 0
