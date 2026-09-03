from __future__ import annotations

import os
import secrets
import time
import uuid
from collections import deque
from pathlib import Path
from typing import Any, BinaryIO, Callable, Mapping

from .errors import ErrorCode, ProtocolFailure
from .protocol import (
    PROTOCOL,
    SCHEMA_VERSION,
    HandshakeTranscript,
    JsonLineCodec,
    ack_body_sha256,
    base64url_no_padding,
    client_proof,
    constant_time_hex_equal,
    server_proof,
)
from .schema import SchemaRegistry


class CompanionClient:
    """Synchronous duplex client owned by one long-lived session broker.

    The API deliberately separates dispatch, event waiting, and terminal waiting
    so a choice continuation can be written while its parent remains in-flight.
    """

    def __init__(
        self,
        *,
        pipe_name: str,
        session_id: str,
        instance_id: str,
        token: bytes,
        expected_adapter_id: str | None = None,
        connector: Callable[[str], BinaryIO] | None = None,
        codec: JsonLineCodec | None = None,
        schemas: SchemaRegistry | None = None,
    ) -> None:
        if len(token) < 32:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "companion token must contain at least 256 bits")
        self.pipe_name = pipe_name
        self.session_id = session_id
        self.instance_id = instance_id
        self._token = bytes(token)
        self.expected_adapter_id = expected_adapter_id
        self._connector = connector or self._open_windows_pipe
        self.codec = codec or JsonLineCodec()
        self.schemas = schemas or SchemaRegistry()
        self._stream: BinaryIO | None = None
        self._client_seq = 0
        self.last_seq = 0
        self.process_epoch: str | None = None
        self.connection_id: str | None = None
        self.hello_ack_body: dict[str, Any] = {}
        self._pending: deque[dict[str, Any]] = deque()
        self._status: dict[str, dict[str, Any]] = {}

    @staticmethod
    def _open_windows_pipe(pipe_name: str) -> BinaryIO:
        if os.name != "nt":
            raise ProtocolFailure(ErrorCode.CAPABILITY_UNAVAILABLE, "Windows Named Pipe transport is unavailable")
        return open(rf"\\.\pipe\{pipe_name}", "r+b", buffering=0)

    def __enter__(self) -> "CompanionClient":
        return self

    def __exit__(self, exc_type: Any, exc: Any, traceback: Any) -> None:
        self.close()

    @property
    def connected(self) -> bool:
        return self._stream is not None

    def connect(self, *, timeout_seconds: float = 30.0, resume_from_seq: int | None = None) -> dict[str, Any]:
        self.close()
        deadline = time.monotonic() + timeout_seconds
        last_error: OSError | None = None
        while time.monotonic() < deadline:
            try:
                self._stream = self._connector(self.pipe_name)
                break
            except OSError as exc:
                last_error = exc
                time.sleep(0.025)
        if self._stream is None:
            raise ProtocolFailure(
                ErrorCode.TIMEOUT_ACTION,
                f"timed out connecting to companion pipe: {self.pipe_name}",
                details={"last_error": str(last_error) if last_error else None},
            )
        try:
            challenge = self._read_message(auth_phase=True)
            self.schemas.validate("protocol-v1", challenge)
            if challenge.get("type") != "challenge":
                raise ProtocolFailure(ErrorCode.AUTH_FAILED, "companion did not send a challenge")
            self._require_identity(challenge)
            if challenge.get("protocol_min") != PROTOCOL or challenge.get("protocol_max") != PROTOCOL:
                raise ProtocolFailure(ErrorCode.UNSUPPORTED_VERSION, "no compatible protocol major")
            self.process_epoch = str(challenge["process_epoch"])
            self.connection_id = str(challenge["connection_id"])
            client_nonce = base64url_no_padding(secrets.token_bytes(32))
            resume = self.last_seq if resume_from_seq is None else resume_from_seq
            transcript = HandshakeTranscript(
                self.session_id,
                self.instance_id,
                self.process_epoch,
                self.connection_id,
                PROTOCOL,
                resume,
                str(challenge["server_nonce"]),
                client_nonce,
            )
            hello = {
                "protocol": PROTOCOL,
                "schema_version": SCHEMA_VERSION,
                "type": "hello",
                "session_id": self.session_id,
                "instance_id": self.instance_id,
                "process_epoch": self.process_epoch,
                "connection_id": self.connection_id,
                "negotiated_protocol": PROTOCOL,
                "resume_from_seq": resume,
                "server_nonce": challenge["server_nonce"],
                "client_nonce": client_nonce,
                "client_proof": client_proof(self._token, transcript),
            }
            self.schemas.validate("protocol-v1", hello)
            self._write_message(hello)
            try:
                acknowledgement = self._read_message(auth_phase=True)
            except ProtocolFailure as exc:
                raise ProtocolFailure(ErrorCode.AUTH_FAILED, "companion rejected client proof") from exc
            self.schemas.validate("protocol-v1", acknowledgement)
            if acknowledgement.get("type") != "hello_ack":
                raise ProtocolFailure(ErrorCode.SERVER_AUTH_FAILED, "companion did not send hello_ack")
            body = acknowledgement["body"]
            if ack_body_sha256(body) != acknowledgement.get("body_sha256"):
                raise ProtocolFailure(ErrorCode.SERVER_AUTH_FAILED, "hello_ack body hash mismatch")
            if not constant_time_hex_equal(
                server_proof(self._token, transcript, body), str(acknowledgement.get("server_proof", ""))
            ):
                raise ProtocolFailure(ErrorCode.SERVER_AUTH_FAILED, "companion server proof mismatch")
            self._require_identity(body)
            if body.get("process_epoch") != self.process_epoch or body.get("connection_id") != self.connection_id:
                raise ProtocolFailure(ErrorCode.SERVER_AUTH_FAILED, "hello_ack epoch identity mismatch")
            adapter_id = body.get("adapter", {}).get("id")
            if self.expected_adapter_id is not None and adapter_id != self.expected_adapter_id:
                raise ProtocolFailure(
                    ErrorCode.SERVER_AUTH_FAILED,
                    "unexpected companion adapter",
                    details={"expected": self.expected_adapter_id, "actual": adapter_id},
                )
            self.hello_ack_body = dict(body)
            self._client_seq = 0
            return self.hello_ack_body
        except Exception:
            self.close()
            raise

    def close(self) -> None:
        stream, self._stream = self._stream, None
        if stream is not None:
            try:
                stream.close()
            except OSError:
                pass

    def new_request_id(self) -> str:
        return str(uuid.uuid4())

    def send_only(
        self,
        command: str,
        args: Mapping[str, Any],
        *,
        wait_for: str = "immediate",
        timeout_ms: int = 10_000,
        request_id: str | None = None,
        trace: Mapping[str, Any] | None = None,
    ) -> str:
        if self._stream is None:
            raise ProtocolFailure(ErrorCode.BROKER_EXIT, "companion connection is not active")
        self._client_seq += 1
        identifier = request_id or self.new_request_id()
        request: dict[str, Any] = {
            "protocol": PROTOCOL,
            "schema_version": SCHEMA_VERSION,
            "type": "request",
            "seq": self._client_seq,
            "request_id": identifier,
            "instance_id": self.instance_id,
            "command": command,
            "args": dict(args),
            "wait_for": wait_for,
            "timeout_ms": timeout_ms,
        }
        if trace:
            request["trace"] = dict(trace)
        self.schemas.validate("protocol-v1", request)
        self._write_message(request)
        self._status[identifier] = {"phase": "sent", "terminal": None}
        return identifier

    def dispatch(self, command: str, args: Mapping[str, Any], **kwargs: Any) -> str:
        request_id = self.send_only(command, args, **kwargs)
        message = self._wait_for(lambda item: item.get("request_id") == request_id and item.get("type") == "accepted")
        self._remember(message)
        return request_id

    def request(self, command: str, args: Mapping[str, Any], **kwargs: Any) -> dict[str, Any]:
        request_id = self.send_only(command, args, **kwargs)
        return self.wait_terminal(request_id)

    def wait_event(self, request_id: str, name: str) -> dict[str, Any]:
        message = self._wait_for(
            lambda item: item.get("request_id") == request_id
            and item.get("type") == "event"
            and item.get("name") == name
        )
        self._remember(message)
        return message

    def wait_terminal(self, request_id: str) -> dict[str, Any]:
        message = self._wait_for(
            lambda item: item.get("request_id") == request_id and item.get("type") in {"completed", "failed"}
        )
        self._remember(message)
        return message

    def request_status(self, request_id: str) -> dict[str, Any]:
        return dict(self._status.get(request_id, {"phase": "unknown", "terminal": None}))

    def _wait_for(self, predicate: Callable[[dict[str, Any]], bool]) -> dict[str, Any]:
        for _ in range(len(self._pending)):
            message = self._pending.popleft()
            if predicate(message):
                return message
            self._pending.append(message)
        while True:
            message = self._read_message()
            self._remember(message)
            if predicate(message):
                return message
            self._pending.append(message)

    def _remember(self, message: Mapping[str, Any]) -> None:
        request_id = message.get("request_id")
        if not isinstance(request_id, str):
            return
        phase = str(message.get("type", "unknown"))
        terminal = dict(message) if phase in {"completed", "failed"} else None
        self._status[request_id] = {"phase": phase, "terminal": terminal}

    def _read_message(self, *, auth_phase: bool = False) -> dict[str, Any]:
        if self._stream is None:
            raise ProtocolFailure(ErrorCode.BROKER_EXIT, "companion connection is not active")
        line = bytearray()
        while True:
            chunk = self._stream.read(1)
            if not chunk:
                code = ErrorCode.AUTH_FAILED if auth_phase else ErrorCode.PROCESS_EXIT
                raise ProtocolFailure(code, "companion pipe closed before a complete message")
            line.extend(chunk)
            if len(line) > self.codec.limits.max_line_bytes:
                raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "companion JSONL line limit exceeded")
            if chunk == b"\n":
                break
        message = self.codec.decode(bytes(line))
        if not auth_phase:
            self.schemas.validate("protocol-v1", message)
            sequence = message.get("seq")
            if isinstance(sequence, int):
                self.last_seq = max(self.last_seq, sequence)
        return message

    def _write_message(self, message: Mapping[str, Any]) -> None:
        if self._stream is None:
            raise ProtocolFailure(ErrorCode.BROKER_EXIT, "companion connection is not active")
        try:
            self._stream.write(self.codec.encode(message))
            self._stream.flush()
        except OSError as exc:
            raise ProtocolFailure(ErrorCode.PROCESS_EXIT, f"companion pipe write failed: {exc}") from exc

    def _require_identity(self, value: Mapping[str, Any]) -> None:
        if value.get("session_id") != self.session_id or value.get("instance_id") != self.instance_id:
            raise ProtocolFailure(
                ErrorCode.SERVER_AUTH_FAILED,
                "companion session or instance identity mismatch",
            )
