from __future__ import annotations

import copy
import json
from pathlib import Path

import pytest

from cli_anything.slaythespare2_111_beta.core.errors import ErrorCode, ProtocolFailure
from cli_anything.slaythespare2_111_beta.core.protocol import (
    CapabilityState,
    CommandDescriptor,
    CompletionStrategy,
    ConcurrencyClass,
    CriticalEventBuffer,
    HandshakeTranscript,
    IdempotencyCache,
    JsonLineCodec,
    MonotonicBudget,
    MutationLane,
    NonceRegistry,
    ProtocolLimits,
    RequestTracker,
    ack_body_sha256,
    client_proof,
    constant_time_hex_equal,
    lp,
    request_payload_sha256,
    server_proof,
)
from cli_anything.slaythespare2_111_beta.core.schema import SchemaRegistry


def _load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


@pytest.mark.parametrize(
    ("schema_name", "relative"),
    [
        ("protocol-v1", "protocol/challenge.json"),
        ("protocol-v1", "protocol/hello.json"),
        ("protocol-v1", "protocol/request-play-card.json"),
        ("protocol-v1", "protocol/choice-required.json"),
        ("state-v1", "state/minimal-state.json"),
        ("scenario-v1", "scenario/poc-1b.json"),
        ("evidence-v1", "evidence/minimal-manifest.json"),
    ],
)
def test_golden_document_validates(
    schemas_root: Path, golden_root: Path, schema_name: str, relative: str
) -> None:
    SchemaRegistry(schemas_root).validate(schema_name, _load(golden_root / relative))


def test_schema_rejects_protocol_major(schemas_root: Path, golden_root: Path) -> None:
    request = _load(golden_root / "protocol/request-play-card.json")
    request["protocol"] = "sts2-test/v2"
    with pytest.raises(ProtocolFailure, match="protocol-v1"):
        SchemaRegistry(schemas_root).validate("protocol-v1", request)


def test_schema_rejects_unknown_lifecycle_type(schemas_root: Path) -> None:
    with pytest.raises(ProtocolFailure):
        SchemaRegistry(schemas_root).validate(
            "protocol-v1", {"protocol": "sts2-test/v1", "schema_version": 1, "type": "done"}
        )


def test_jsonl_round_trip() -> None:
    codec = JsonLineCodec()
    payload = {"z": 2, "a": "中文"}
    assert codec.decode(codec.encode(payload)) == payload


def test_jsonl_encoder_is_stable_and_lf_terminated() -> None:
    encoded = JsonLineCodec().encode({"z": 2, "a": 1})
    assert encoded == b'{"a":1,"z":2}\n'


@pytest.mark.parametrize(
    ("wire", "code"),
    [
        (b"\xef\xbb\xbf{}\n", ErrorCode.INVALID_ARGUMENT),
        (b'{"x":"a\x00b"}\n', ErrorCode.INVALID_ARGUMENT),
        (b"{}{}\n", ErrorCode.INVALID_ARGUMENT),
        (b"{}", ErrorCode.INVALID_ARGUMENT),
        (b"\xff\n", ErrorCode.INVALID_ARGUMENT),
    ],
)
def test_jsonl_rejects_invalid_framing(wire: bytes, code: ErrorCode) -> None:
    with pytest.raises(ProtocolFailure) as failure:
        JsonLineCodec().decode(wire)
    assert failure.value.code == code


def test_jsonl_rejects_line_limit() -> None:
    codec = JsonLineCodec(ProtocolLimits(max_line_bytes=8))
    with pytest.raises(ProtocolFailure):
        codec.decode(b'{"long":1}\n')


def test_jsonl_rejects_depth_limit() -> None:
    codec = JsonLineCodec(ProtocolLimits(max_depth=2))
    with pytest.raises(ProtocolFailure):
        codec.decode(b'{"a":{"b":{"c":1}}}\n')


def test_jsonl_rejects_string_limit() -> None:
    codec = JsonLineCodec(ProtocolLimits(max_string_bytes=3))
    with pytest.raises(ProtocolFailure):
        codec.decode(b'{"a":"four"}\n')


def test_jsonl_rejects_array_limit() -> None:
    codec = JsonLineCodec(ProtocolLimits(max_array_items=2))
    with pytest.raises(ProtocolFailure):
        codec.decode(b'{"a":[1,2,3]}\n')


def test_length_prefix_is_big_endian_utf8() -> None:
    assert lp("é") == b"\x00\x00\x00\x02\xc3\xa9"


def test_handshake_transcript_is_unambiguous() -> None:
    first = HandshakeTranscript("a|b", "c", "p", "x", "sts2-test/v1", 0, "s", "n")
    second = HandshakeTranscript("a", "b|c", "p", "x", "sts2-test/v1", 0, "s", "n")
    assert first.to_bytes() != second.to_bytes()


def test_client_proof_is_deterministic() -> None:
    token = bytes(range(32))
    transcript = HandshakeTranscript(
        "session-golden", "solo", "process-golden", "connection-golden",
        "sts2-test/v1", 0,
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        "AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE",
    )
    assert client_proof(token, transcript) == client_proof(token, transcript)
    assert len(client_proof(token, transcript)) == 64


def test_client_proof_changes_when_resume_seq_changes() -> None:
    token = bytes(range(32))
    base = HandshakeTranscript("s", "i", "p", "c", "sts2-test/v1", 0, "a", "b")
    changed = HandshakeTranscript("s", "i", "p", "c", "sts2-test/v1", 1, "a", "b")
    assert client_proof(token, base) != client_proof(token, changed)


def test_server_proof_binds_ack_body(golden_root: Path) -> None:
    token = bytes(range(32))
    transcript = HandshakeTranscript("s", "i", "p", "c", "sts2-test/v1", 0, "a", "b")
    body = _load(golden_root / "protocol/hello-ack-body.json")
    tampered = copy.deepcopy(body)
    tampered["runtime"]["main_thread_probe"] = False
    assert server_proof(token, transcript, body) != server_proof(token, transcript, tampered)


def test_ack_hash_uses_canonical_key_order() -> None:
    assert ack_body_sha256({"b": 2, "a": 1}) == ack_body_sha256({"a": 1, "b": 2})


def test_constant_time_hex_equal_handles_invalid_hex() -> None:
    assert constant_time_hex_equal("00", "00") is True
    assert constant_time_hex_equal("not-hex", "00") is False


def test_nonce_registry_rejects_replay() -> None:
    registry = NonceRegistry(capacity=4)
    registry.remember("transcript-1")
    with pytest.raises(ProtocolFailure) as failure:
        registry.remember("transcript-1")
    assert failure.value.code == ErrorCode.AUTH_FAILED


def test_nonce_registry_evicts_oldest() -> None:
    registry = NonceRegistry(capacity=2)
    registry.remember("one")
    registry.remember("two")
    registry.remember("three")
    registry.remember("one")


def test_request_digest_ignores_transport_seq(golden_root: Path) -> None:
    request = _load(golden_root / "protocol/request-play-card.json")
    changed = copy.deepcopy(request)
    changed["seq"] = 999
    assert request_payload_sha256(request) == request_payload_sha256(changed)


def test_request_digest_binds_args(golden_root: Path) -> None:
    request = _load(golden_root / "protocol/request-play-card.json")
    changed = copy.deepcopy(request)
    changed["args"]["card"] += "-other"
    assert request_payload_sha256(request) != request_payload_sha256(changed)


def test_idempotency_first_request_is_new(golden_root: Path) -> None:
    decision = IdempotencyCache().accept(_load(golden_root / "protocol/request-play-card.json"))
    assert decision.status == "new"


def test_idempotency_same_inflight_request_is_inflight(golden_root: Path) -> None:
    cache = IdempotencyCache()
    request = _load(golden_root / "protocol/request-play-card.json")
    cache.accept(request)
    assert cache.accept(request).status == "in_flight"


def test_idempotency_replays_terminal(golden_root: Path) -> None:
    cache = IdempotencyCache()
    request = _load(golden_root / "protocol/request-play-card.json")
    cache.accept(request)
    cache.complete(request["request_id"], {"type": "completed", "result": {"ok": True}})
    decision = cache.accept(request)
    assert decision.status == "replay"
    assert decision.response and decision.response["replayed"] is True


def test_idempotency_conflict_is_error(golden_root: Path) -> None:
    cache = IdempotencyCache()
    request = _load(golden_root / "protocol/request-play-card.json")
    cache.accept(request)
    changed = copy.deepcopy(request)
    changed["args"]["card"] += "-other"
    with pytest.raises(ProtocolFailure) as failure:
        cache.accept(changed)
    assert failure.value.code == ErrorCode.IDEMPOTENCY_CONFLICT


def test_idempotency_cache_is_bounded(golden_root: Path) -> None:
    cache = IdempotencyCache(capacity=2)
    request = _load(golden_root / "protocol/request-play-card.json")
    for index in range(3):
        current = copy.deepcopy(request)
        current["request_id"] = f"request-{index}"
        cache.accept(current)
        cache.complete(current["request_id"], {"type": "completed"})
    assert len(cache) == 2


def test_mutation_lane_accepts_parent() -> None:
    lane = MutationLane()
    lane.acquire_parent("parent")
    assert lane.owner == "parent"


@pytest.mark.parametrize("concurrency", [ConcurrencyClass.SNAPSHOT_SAFE_QUERY, ConcurrencyClass.CONTROL])
def test_mutation_lane_allows_safe_passthrough(concurrency: ConcurrencyClass) -> None:
    lane = MutationLane()
    lane.acquire_parent("parent")
    lane.authorize("query", concurrency)


def test_mutation_lane_allows_matching_choice_continuation() -> None:
    lane = MutationLane()
    lane.acquire_parent("parent")
    lane.authorize(
        "choice", ConcurrencyClass.CHOICE_CONTINUATION,
        blocked_request_id="parent", choice_valid=True,
    )


@pytest.mark.parametrize(
    ("blocked_request_id", "choice_valid"),
    [("other", True), ("parent", False)],
)
def test_mutation_lane_rejects_invalid_choice_continuation(
    blocked_request_id: str, choice_valid: bool
) -> None:
    lane = MutationLane()
    lane.acquire_parent("parent")
    with pytest.raises(ProtocolFailure) as failure:
        lane.authorize(
            "choice", ConcurrencyClass.CHOICE_CONTINUATION,
            blocked_request_id=blocked_request_id, choice_valid=choice_valid,
        )
    assert failure.value.code == ErrorCode.STALE_HANDLE


def test_mutation_lane_rejects_unrelated_mutation() -> None:
    lane = MutationLane()
    lane.acquire_parent("parent")
    with pytest.raises(ProtocolFailure) as failure:
        lane.authorize("other", ConcurrencyClass.GAMEPLAY_MUTATION)
    assert failure.value.code == ErrorCode.MUTATION_BUSY


def test_mutation_lane_release_requires_owner() -> None:
    lane = MutationLane()
    lane.acquire_parent("parent")
    with pytest.raises(ProtocolFailure):
        lane.release_parent("other")
    lane.release_parent("parent")
    assert lane.owner is None


def test_mutation_lane_freeze_is_irreversible() -> None:
    lane = MutationLane()
    lane.freeze(ErrorCode.OBSERVER_OVERFLOW)
    with pytest.raises(ProtocolFailure) as failure:
        lane.acquire_parent("parent")
    assert failure.value.code == ErrorCode.OBSERVER_OVERFLOW


def test_critical_buffer_accepts_without_blocking() -> None:
    buffer = CriticalEventBuffer(capacity=2)
    assert buffer.try_write({"type": "event", "seq": 1}) is True
    assert buffer.pop()["seq"] == 1


def test_critical_buffer_overflow_latches_failure() -> None:
    buffer = CriticalEventBuffer(capacity=1)
    buffer.try_write({"type": "event", "seq": 1})
    assert buffer.try_write({"type": "completed", "seq": 2}, ["request-1"]) is False
    assert buffer.invalid is True
    assert buffer.overflow and buffer.overflow["code"] == ErrorCode.OBSERVER_OVERFLOW.value


def test_critical_buffer_preserves_terminal_table() -> None:
    buffer = CriticalEventBuffer(capacity=1)
    buffer.try_write({"type": "event"})
    buffer.try_write({"type": "completed"}, ["request-1"])
    assert buffer.terminal_table["request-1"]["error"]["code"] == ErrorCode.OBSERVER_OVERFLOW.value


def test_critical_buffer_remains_invalid_after_drain() -> None:
    buffer = CriticalEventBuffer(capacity=1)
    buffer.try_write({"type": "event"})
    buffer.try_write({"type": "completed"})
    buffer.pop()
    assert buffer.invalid is True


def test_telemetry_overflow_is_counted_separately() -> None:
    buffer = CriticalEventBuffer(capacity=1, telemetry_capacity=1)
    assert buffer.try_write_telemetry({"name": "frame"}) is True
    assert buffer.try_write_telemetry({"name": "frame"}) is False
    assert buffer.telemetry_dropped == 1
    assert buffer.invalid is False


def test_request_tracker_enforces_one_terminal() -> None:
    tracker = RequestTracker()
    tracker.accept("request-1")
    tracker.start("request-1")
    tracker.complete("request-1", {"ok": True})
    with pytest.raises(ProtocolFailure):
        tracker.fail("request-1", ErrorCode.PROCESS_EXIT, "late")


def test_request_tracker_synthesizes_process_exit() -> None:
    tracker = RequestTracker()
    tracker.accept("one")
    tracker.accept("two")
    terminals = tracker.process_exited(7)
    assert {item["request_id"] for item in terminals} == {"one", "two"}
    assert all(item["error"]["code"] == ErrorCode.PROCESS_EXIT.value for item in terminals)


def test_monotonic_budget_counts_down() -> None:
    ticks = iter([1_000_000_000, 1_250_000_000])
    budget = MonotonicBudget(1000, clock=lambda: next(ticks))
    assert budget.remaining_ms() == 750


def test_monotonic_budget_resume_keeps_deadline() -> None:
    now = [1_000_000_000]
    budget = MonotonicBudget(1000, clock=lambda: now[0])
    deadline = budget.deadline_ns
    now[0] += 400_000_000
    resumed = budget.resume()
    assert resumed.deadline_ns == deadline
    assert resumed.remaining_ms() == 600


@pytest.mark.parametrize(
    ("state", "valid"),
    [("available", True), ("unavailable", True), ("unknown", True), ("partial", True), ("yes", False)],
)
def test_capability_state_values(state: str, valid: bool) -> None:
    if valid:
        assert CapabilityState(state).value == state
    else:
        with pytest.raises(ValueError):
            CapabilityState(state)


def test_command_descriptor_rejects_incompatible_wait_level() -> None:
    descriptor = CommandDescriptor(
        name="runtime.ping",
        kind="query",
        concurrency_class=ConcurrencyClass.SNAPSHOT_SAFE_QUERY,
        completion_strategy=CompletionStrategy.IMMEDIATE_QUERY,
        default_wait_for="immediate",
    )
    with pytest.raises(ProtocolFailure):
        descriptor.validate_wait_for("queue_settled")


def test_protocol_failure_serializes_without_traceback() -> None:
    failure = ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "bad", details={"field": "x"})
    assert failure.to_error() == {
        "code": "E_INVALID_ARGUMENT",
        "message": "bad",
        "retryable": False,
        "details": {"field": "x"},
    }
