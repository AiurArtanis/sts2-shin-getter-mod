from __future__ import annotations

import base64
import copy
import hashlib
import hmac
import json
import threading
import time
from collections import OrderedDict, deque
from dataclasses import dataclass
from enum import StrEnum
from typing import Any, Callable, Iterable, Mapping

from .errors import ErrorCode, ProtocolFailure


PROTOCOL = "sts2-test/v1"
SCHEMA_VERSION = 1


def canonical_json_bytes(value: Any) -> bytes:
    try:
        return json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")
    except (TypeError, ValueError) as exc:
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"value is not canonical JSON: {exc}") from exc


def lp(value: str) -> bytes:
    encoded = value.encode("utf-8")
    if len(encoded) > 0xFFFFFFFF:
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "length-prefixed field is too large")
    return len(encoded).to_bytes(4, "big") + encoded


def base64url_no_padding(value: bytes) -> str:
    return base64.urlsafe_b64encode(value).decode("ascii").rstrip("=")


@dataclass(frozen=True)
class HandshakeTranscript:
    session_id: str
    instance_id: str
    process_epoch: str
    connection_id: str
    negotiated_protocol: str
    resume_from_seq: int
    server_nonce_b64url: str
    client_nonce_b64url: str

    def to_bytes(self) -> bytes:
        if self.resume_from_seq < 0:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "resume_from_seq must be non-negative")
        fields = (
            "sts2-test/handshake/v1",
            self.session_id,
            self.instance_id,
            self.process_epoch,
            self.connection_id,
            self.negotiated_protocol,
            str(self.resume_from_seq),
            self.server_nonce_b64url,
            self.client_nonce_b64url,
        )
        return b"".join(lp(field) for field in fields)


def _proof(token: bytes, label: str, transcript: HandshakeTranscript, suffix: bytes = b"") -> str:
    if len(token) < 32:
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "handshake token must contain at least 256 bits")
    message = lp(label) + transcript.to_bytes() + suffix
    return hmac.new(token, message, hashlib.sha256).hexdigest()


def client_proof(token: bytes, transcript: HandshakeTranscript) -> str:
    return _proof(token, "sts2-test/client-proof/v1", transcript)


def ack_body_sha256(body: Mapping[str, Any]) -> str:
    return hashlib.sha256(canonical_json_bytes(body)).hexdigest()


def server_proof(token: bytes, transcript: HandshakeTranscript, ack_body: Mapping[str, Any]) -> str:
    return _proof(
        token,
        "sts2-test/server-proof/v1",
        transcript,
        lp(ack_body_sha256(ack_body)),
    )


def constant_time_hex_equal(left: str, right: str) -> bool:
    try:
        left_bytes = bytes.fromhex(left)
        right_bytes = bytes.fromhex(right)
    except ValueError:
        return False
    return hmac.compare_digest(left_bytes, right_bytes)


class NonceRegistry:
    def __init__(self, capacity: int = 2048) -> None:
        if capacity < 1:
            raise ValueError("capacity must be positive")
        self.capacity = capacity
        self._digests: OrderedDict[str, None] = OrderedDict()

    def remember(self, transcript: str | bytes) -> None:
        encoded = transcript if isinstance(transcript, bytes) else transcript.encode("utf-8")
        digest = hashlib.sha256(encoded).hexdigest()
        if digest in self._digests:
            raise ProtocolFailure(ErrorCode.AUTH_FAILED, "handshake transcript replay rejected")
        self._digests[digest] = None
        self._digests.move_to_end(digest)
        while len(self._digests) > self.capacity:
            self._digests.popitem(last=False)


@dataclass(frozen=True)
class ProtocolLimits:
    max_line_bytes: int = 1024 * 1024
    max_depth: int = 32
    max_string_bytes: int = 256 * 1024
    max_array_items: int = 10_000


def _check_value_limits(value: Any, limits: ProtocolLimits, depth: int = 1) -> None:
    if depth > limits.max_depth:
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSON nesting depth exceeded")
    if isinstance(value, str):
        if len(value.encode("utf-8")) > limits.max_string_bytes:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSON string limit exceeded")
        return
    if isinstance(value, list):
        if len(value) > limits.max_array_items:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSON array limit exceeded")
        for item in value:
            _check_value_limits(item, limits, depth + 1)
        return
    if isinstance(value, dict):
        for key, item in value.items():
            _check_value_limits(str(key), limits, depth + 1)
            _check_value_limits(item, limits, depth + 1)


class JsonLineCodec:
    def __init__(self, limits: ProtocolLimits | None = None) -> None:
        self.limits = limits or ProtocolLimits()

    def encode(self, value: Mapping[str, Any]) -> bytes:
        _check_value_limits(value, self.limits)
        encoded = canonical_json_bytes(value) + b"\n"
        if len(encoded) > self.limits.max_line_bytes:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSONL line limit exceeded")
        return encoded

    def decode(self, line: bytes) -> dict[str, Any]:
        if not line.endswith(b"\n") or b"\n" in line[:-1] or b"\r" in line:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSONL requires exactly one LF-terminated object")
        if len(line) > self.limits.max_line_bytes:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSONL line limit exceeded")
        body = line[:-1]
        if body.startswith(b"\xef\xbb\xbf") or b"\x00" in body:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSONL BOM and NUL are forbidden")
        try:
            text = body.decode("utf-8", errors="strict")
            value = json.loads(text)
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"invalid JSONL: {exc}") from exc
        if not isinstance(value, dict):
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSONL top-level value must be an object")
        _check_value_limits(value, self.limits)
        return value


def request_payload_sha256(request: Mapping[str, Any]) -> str:
    ignored = {"seq", "wall_time", "engine_frame", "logical_time", "connection_id", "broker_epoch"}
    payload = {key: value for key, value in request.items() if key not in ignored}
    return hashlib.sha256(canonical_json_bytes(payload)).hexdigest()


@dataclass(frozen=True)
class IdempotencyDecision:
    status: str
    response: dict[str, Any] | None = None


@dataclass
class _IdempotencyEntry:
    digest: str
    terminal: dict[str, Any] | None = None


class IdempotencyCache:
    def __init__(self, capacity: int = 256) -> None:
        if capacity < 1:
            raise ValueError("capacity must be positive")
        self.capacity = capacity
        self._entries: OrderedDict[str, _IdempotencyEntry] = OrderedDict()

    def __len__(self) -> int:
        return len(self._entries)

    def accept(self, request: Mapping[str, Any]) -> IdempotencyDecision:
        request_id = str(request.get("request_id", ""))
        if not request_id:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "request_id is required")
        digest = request_payload_sha256(request)
        existing = self._entries.get(request_id)
        if existing is None:
            self._entries[request_id] = _IdempotencyEntry(digest)
            return IdempotencyDecision("new")
        if existing.digest != digest:
            raise ProtocolFailure(
                ErrorCode.IDEMPOTENCY_CONFLICT,
                "request_id was already used with a different payload",
                details={"request_id": request_id},
            )
        self._entries.move_to_end(request_id)
        if existing.terminal is None:
            return IdempotencyDecision("in_flight")
        replay = copy.deepcopy(existing.terminal)
        replay["replayed"] = True
        return IdempotencyDecision("replay", replay)

    def complete(self, request_id: str, terminal: Mapping[str, Any]) -> None:
        entry = self._entries.get(request_id)
        if entry is None:
            raise ProtocolFailure(ErrorCode.NOT_FOUND, "unknown request_id")
        if entry.terminal is not None:
            raise ProtocolFailure(ErrorCode.IDEMPOTENCY_CONFLICT, "request already has a terminal result")
        entry.terminal = copy.deepcopy(dict(terminal))
        self._entries.move_to_end(request_id)
        self._trim()

    def _trim(self) -> None:
        while len(self._entries) > self.capacity:
            removable = next((key for key, value in self._entries.items() if value.terminal is not None), None)
            if removable is None:
                return
            del self._entries[removable]


class CapabilityState(StrEnum):
    AVAILABLE = "available"
    UNAVAILABLE = "unavailable"
    UNKNOWN = "unknown"
    PARTIAL = "partial"


class ConcurrencyClass(StrEnum):
    GAMEPLAY_MUTATION = "gameplay-mutation"
    SNAPSHOT_SAFE_QUERY = "snapshot-safe-query"
    CONTROL = "control"
    CHOICE_CONTINUATION = "choice-continuation"


class CompletionStrategy(StrEnum):
    TYPED_ACTION_REFERENCE = "typed_action_reference"
    CONSOLE_ENQUEUE_CORRELATION = "console_enqueue_correlation"
    AWAITABLE_CMD_RESULT = "awaitable_cmd_result"
    LOCATION_PREDICATE = "location_predicate"
    IMMEDIATE_QUERY = "immediate_query"


@dataclass(frozen=True)
class CommandDescriptor:
    name: str
    kind: str
    concurrency_class: ConcurrencyClass
    completion_strategy: CompletionStrategy
    default_wait_for: str
    max_timeout_ms: int = 60_000
    required_capabilities: tuple[str, ...] = ()

    def validate_wait_for(self, wait_for: str) -> None:
        if self.completion_strategy == CompletionStrategy.IMMEDIATE_QUERY and wait_for != "immediate":
            raise ProtocolFailure(
                ErrorCode.INVALID_ARGUMENT,
                f"{self.name} only supports immediate completion",
            )
        if wait_for in {"peer_observed", "network_barrier", "presentation_finished"}:
            raise ProtocolFailure(
                ErrorCode.CAPABILITY_UNAVAILABLE,
                f"{wait_for} is outside the v0.2 command descriptor",
            )


class MutationLane:
    def __init__(self) -> None:
        self.owner: str | None = None
        self.frozen_code: ErrorCode | None = None

    def acquire_parent(self, request_id: str) -> None:
        if self.frozen_code is not None:
            raise ProtocolFailure(self.frozen_code, "gameplay mutation lane is frozen")
        if self.owner is not None and self.owner != request_id:
            raise ProtocolFailure(ErrorCode.MUTATION_BUSY, "another gameplay mutation owns the lane")
        self.owner = request_id

    def authorize(
        self,
        request_id: str,
        concurrency_class: ConcurrencyClass,
        *,
        blocked_request_id: str | None = None,
        choice_valid: bool = False,
    ) -> None:
        if concurrency_class in {ConcurrencyClass.SNAPSHOT_SAFE_QUERY, ConcurrencyClass.CONTROL}:
            return
        if concurrency_class == ConcurrencyClass.CHOICE_CONTINUATION:
            if self.owner == blocked_request_id and choice_valid:
                return
            raise ProtocolFailure(ErrorCode.STALE_HANDLE, "choice continuation does not match the active parent")
        self.acquire_parent(request_id)

    def release_parent(self, request_id: str) -> None:
        if self.owner != request_id:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "only the lane owner can release it")
        self.owner = None

    def freeze(self, code: ErrorCode) -> None:
        if self.frozen_code is None:
            self.frozen_code = code


class CriticalEventBuffer:
    def __init__(self, capacity: int = 256, telemetry_capacity: int = 1024) -> None:
        if capacity < 1 or telemetry_capacity < 1:
            raise ValueError("channel capacity must be positive")
        self.capacity = capacity
        self.telemetry_capacity = telemetry_capacity
        self._critical: deque[dict[str, Any]] = deque()
        self._telemetry: deque[dict[str, Any]] = deque()
        self._lock = threading.Lock()
        self.overflow: dict[str, Any] | None = None
        self.terminal_table: dict[str, dict[str, Any]] = {}
        self.telemetry_dropped = 0

    @property
    def invalid(self) -> bool:
        return self.overflow is not None

    def try_write(self, event: Mapping[str, Any], affected_request_ids: Iterable[str] = ()) -> bool:
        acquired = self._lock.acquire(blocking=False)
        if not acquired:
            self._latch(event, affected_request_ids, reason="producer_lock_contended")
            return False
        try:
            if len(self._critical) >= self.capacity:
                self._latch(event, affected_request_ids, reason="critical_capacity")
                return False
            self._critical.append(copy.deepcopy(dict(event)))
            return True
        finally:
            self._lock.release()

    def _latch(self, event: Mapping[str, Any], request_ids: Iterable[str], *, reason: str) -> None:
        if self.overflow is None:
            self.overflow = {
                "code": ErrorCode.OBSERVER_OVERFLOW.value,
                "reason": reason,
                "first_lost_kind": event.get("type"),
                "first_lost_request_id": event.get("request_id"),
                "first_lost_seq": event.get("seq"),
            }
        for request_id in request_ids:
            self.terminal_table[request_id] = {
                "type": "failed",
                "request_id": request_id,
                "error": ProtocolFailure(
                    ErrorCode.OBSERVER_OVERFLOW,
                    "critical observer channel overflowed; case is invalid",
                ).to_error(),
            }

    def pop(self) -> dict[str, Any] | None:
        with self._lock:
            return self._critical.popleft() if self._critical else None

    def try_write_telemetry(self, event: Mapping[str, Any]) -> bool:
        acquired = self._lock.acquire(blocking=False)
        if not acquired:
            self.telemetry_dropped += 1
            return False
        try:
            if len(self._telemetry) >= self.telemetry_capacity:
                self.telemetry_dropped += 1
                return False
            self._telemetry.append(copy.deepcopy(dict(event)))
            return True
        finally:
            self._lock.release()


@dataclass
class _RequestState:
    phase: str
    terminal: dict[str, Any] | None = None


class RequestTracker:
    def __init__(self) -> None:
        self._requests: OrderedDict[str, _RequestState] = OrderedDict()

    def accept(self, request_id: str) -> None:
        if request_id in self._requests:
            raise ProtocolFailure(ErrorCode.IDEMPOTENCY_CONFLICT, "request already tracked")
        self._requests[request_id] = _RequestState("accepted")

    def start(self, request_id: str) -> None:
        state = self._state(request_id)
        if state.terminal is not None:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "terminal request cannot start")
        state.phase = "started"

    def complete(self, request_id: str, result: Mapping[str, Any]) -> dict[str, Any]:
        return self._terminal(request_id, {"type": "completed", "request_id": request_id, "result": dict(result)})

    def fail(self, request_id: str, code: ErrorCode, message: str) -> dict[str, Any]:
        return self._terminal(
            request_id,
            {"type": "failed", "request_id": request_id, "error": ProtocolFailure(code, message).to_error()},
        )

    def status(self, request_id: str) -> dict[str, Any] | None:
        state = self._requests.get(request_id)
        if state is None:
            return None
        return {"phase": state.phase, "terminal": copy.deepcopy(state.terminal)}

    def process_exited(self, exit_code: int | None) -> list[dict[str, Any]]:
        terminals: list[dict[str, Any]] = []
        for request_id, state in self._requests.items():
            if state.terminal is None:
                terminals.append(
                    self.fail(request_id, ErrorCode.PROCESS_EXIT, f"game process exited with code {exit_code}")
                )
        return terminals

    def _state(self, request_id: str) -> _RequestState:
        state = self._requests.get(request_id)
        if state is None:
            raise ProtocolFailure(ErrorCode.NOT_FOUND, "unknown request_id")
        return state

    def _terminal(self, request_id: str, terminal: dict[str, Any]) -> dict[str, Any]:
        state = self._state(request_id)
        if state.terminal is not None:
            raise ProtocolFailure(ErrorCode.IDEMPOTENCY_CONFLICT, "request already has a terminal result")
        state.phase = terminal["type"]
        state.terminal = copy.deepcopy(terminal)
        return terminal


class MonotonicBudget:
    def __init__(self, timeout_ms: int, *, clock: Callable[[], int] = time.monotonic_ns) -> None:
        if timeout_ms < 1:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "timeout_ms must be positive")
        self._clock = clock
        self.deadline_ns = clock() + timeout_ms * 1_000_000

    def remaining_ms(self) -> int:
        return max(0, (self.deadline_ns - self._clock()) // 1_000_000)

    @property
    def expired(self) -> bool:
        return self.remaining_ms() == 0

    def resume(self) -> "MonotonicBudget":
        resumed = object.__new__(MonotonicBudget)
        resumed._clock = self._clock
        resumed.deadline_ns = self.deadline_ns
        return resumed
