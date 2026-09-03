from __future__ import annotations

from enum import StrEnum
from typing import Any

from .legacy import HarnessError


class ErrorCode(StrEnum):
    INVALID_ARGUMENT = "E_INVALID_ARGUMENT"
    INVALID_PHASE = "E_INVALID_PHASE"
    NOT_FOUND = "E_NOT_FOUND"
    AMBIGUOUS_ID = "E_AMBIGUOUS_ID"
    STALE_HANDLE = "E_STALE_HANDLE"
    UNSUPPORTED_VERSION = "E_UNSUPPORTED_VERSION"
    CAPABILITY_UNAVAILABLE = "E_CAPABILITY_UNAVAILABLE"
    TIMEOUT_ACTION = "E_TIMEOUT_ACTION"
    TIMEOUT_NETWORK = "E_TIMEOUT_NETWORK"
    CHOICE_REQUIRED = "E_CHOICE_REQUIRED"
    PEER_DISCONNECTED = "E_PEER_DISCONNECTED"
    STATE_DIVERGENCE = "E_STATE_DIVERGENCE"
    SAVE_FAILED = "E_SAVE_FAILED"
    REPLAY_INCOMPATIBLE = "E_REPLAY_INCOMPATIBLE"
    PROCESS_EXIT = "E_PROCESS_EXIT"
    CANCELLED = "E_CANCELLED"
    CANCEL_UNSAFE = "E_CANCEL_UNSAFE"
    IDEMPOTENCY_CONFLICT = "E_IDEMPOTENCY_CONFLICT"
    IDEMPOTENCY_WINDOW_EXPIRED = "E_IDEMPOTENCY_WINDOW_EXPIRED"
    AUTH_FAILED = "E_AUTH_FAILED"
    SERVER_AUTH_FAILED = "E_SERVER_AUTH_FAILED"
    BROKER_EXIT = "E_BROKER_EXIT"
    MUTATION_BUSY = "E_MUTATION_BUSY"
    ACTION_CORRELATION_FAILED = "E_ACTION_CORRELATION_FAILED"
    OBSERVER_OVERFLOW = "E_OBSERVER_OVERFLOW"
    RESUME_WINDOW_EXPIRED = "E_RESUME_WINDOW_EXPIRED"
    PROCESS_IDENTITY_MISMATCH = "E_PROCESS_IDENTITY_MISMATCH"
    ISOLATION_BREACH = "E_ISOLATION_BREACH"
    PEER_CORRELATION_AMBIGUOUS = "E_PEER_CORRELATION_AMBIGUOUS"
    EVIDENCE_TAMPERED = "E_EVIDENCE_TAMPERED"
    MAIN_THREAD_VIOLATION = "E_MAIN_THREAD_VIOLATION"
    OBSERVER_SIDE_EFFECT = "E_OBSERVER_SIDE_EFFECT"


class ProtocolFailure(HarnessError):
    """A stable wire/CLI failure with no implicit traceback disclosure."""

    def __init__(
        self,
        code: ErrorCode,
        message: str,
        *,
        retryable: bool = False,
        details: dict[str, Any] | None = None,
    ) -> None:
        super().__init__(message)
        self.code = code
        self.retryable = retryable
        self.details = details or {}

    def to_error(self) -> dict[str, Any]:
        return {
            "code": self.code.value,
            "message": str(self),
            "retryable": self.retryable,
            "details": self.details,
        }
