from __future__ import annotations

import copy
import io
import os
import secrets
import threading
import time
import uuid
from collections import deque
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
    """Thread-safe duplex client owned by one long-lived session broker.

    A dedicated reader pump is the sole pipe reader. Dispatch, event waiting,
    terminal waiting, and reconnects therefore share one ordered inbox without
    allowing any CLI call to block forever in ``read(1)``.
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
        self._condition = threading.Condition(threading.RLock())
        self._write_lock = threading.Lock()
        self._lifecycle_lock = threading.RLock()
        self._stream: BinaryIO | None = None
        self._reader_thread: threading.Thread | None = None
        self._reader_generation = 0
        self._reader_failure: ProtocolFailure | None = None
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
        with self._condition:
            return self._stream is not None and self._reader_failure is None

    def connect(self, *, timeout_seconds: float = 30.0, resume_from_seq: int | None = None) -> dict[str, Any]:
        if timeout_seconds <= 0:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "companion timeout must be positive")
        with self._lifecycle_lock:
            self.close()
            deadline = time.monotonic() + timeout_seconds
            last_error: OSError | None = None
            stream: BinaryIO | None = None
            while time.monotonic() < deadline:
                try:
                    stream = self._connector(self.pipe_name)
                    break
                except OSError as exc:
                    last_error = exc
                    time.sleep(min(0.025, max(0.0, deadline - time.monotonic())))
            if stream is None:
                raise ProtocolFailure(
                    ErrorCode.TIMEOUT_ACTION,
                    f"timed out connecting to companion pipe: {self.pipe_name}",
                    details={"last_error": str(last_error) if last_error else None},
                )

            with self._condition:
                self._reader_generation += 1
                generation = self._reader_generation
                self._stream = stream
                self._reader_failure = None
                self._client_seq = 0
                reader = threading.Thread(
                    target=self._reader_loop,
                    args=(stream, generation),
                    name=f"sts2-companion-reader-{self.instance_id}",
                    daemon=True,
                )
                self._reader_thread = reader
                reader.start()

            try:
                challenge = self._wait_for(
                    lambda item: item.get("type") == "challenge",
                    deadline=deadline,
                    timeout_message="companion challenge exceeded the monotonic local deadline",
                )
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
                    acknowledgement = self._wait_for(
                        lambda item: item.get("type") == "hello_ack",
                        deadline=deadline,
                        timeout_message="companion hello_ack exceeded the monotonic local deadline",
                    )
                except ProtocolFailure as exc:
                    raise ProtocolFailure(ErrorCode.AUTH_FAILED, "companion rejected client proof") from exc
                body = acknowledgement["body"]
                if ack_body_sha256(body) != acknowledgement.get("body_sha256"):
                    raise ProtocolFailure(ErrorCode.SERVER_AUTH_FAILED, "hello_ack body hash mismatch")
                if not constant_time_hex_equal(
                    server_proof(self._token, transcript, body), str(acknowledgement.get("server_proof", ""))
                ):
                    raise ProtocolFailure(ErrorCode.SERVER_AUTH_FAILED, "hello_ack server proof mismatch")
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
                resume_status = body.get("resume")
                if isinstance(resume_status, Mapping) and resume_status.get("status") == "expired":
                    error = resume_status.get("error")
                    details = dict(error.get("details") or {}) if isinstance(error, Mapping) else {}
                    message = (
                        str(error.get("message", "companion replay window expired"))
                        if isinstance(error, Mapping)
                        else "companion replay window expired"
                    )
                    raise ProtocolFailure(ErrorCode.RESUME_WINDOW_EXPIRED, message, details=details)
                self.hello_ack_body = dict(body)
                return self.hello_ack_body
            except Exception:
                self.close()
                raise

    def close(self) -> None:
        with self._lifecycle_lock:
            with self._condition:
                stream = self._stream
                reader = self._reader_thread
                self._stream = None
                self._reader_thread = None
                self._reader_generation += 1
                self._reader_failure = None
                self._condition.notify_all()
            if stream is not None:
                try:
                    stream.close()
                except OSError:
                    pass
            if reader is not None and reader is not threading.current_thread():
                reader.join(timeout=1.0)

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
        identifier = request_id or self.new_request_id()
        with self._write_lock:
            with self._condition:
                if self._stream is None or self._reader_failure is not None:
                    raise self._reader_failure or ProtocolFailure(ErrorCode.BROKER_EXIT, "companion connection is not active")
                self._client_seq += 1
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
                self._status[identifier] = {"phase": "sent", "terminal": None}
                stream = self._stream
            self._write_to_stream(stream, request)
        return identifier

    def dispatch(self, command: str, args: Mapping[str, Any], **kwargs: Any) -> str:
        options = dict(kwargs)
        local_timeout = float(options.pop("local_timeout_seconds", self._default_local_timeout(options)))
        request_id = self.send_only(command, args, **options)
        self._wait_for(
            lambda item: item.get("request_id") == request_id and item.get("type") == "accepted",
            timeout_seconds=local_timeout,
            request_id=request_id,
            terminal_before_match=True,
            timeout_message="request acceptance exceeded the monotonic local deadline",
        )
        return request_id

    def request(self, command: str, args: Mapping[str, Any], **kwargs: Any) -> dict[str, Any]:
        options = dict(kwargs)
        local_timeout = float(options.pop("local_timeout_seconds", self._default_local_timeout(options)))
        request_id = self.send_only(command, args, **options)
        return self.wait_terminal(request_id, timeout_seconds=local_timeout)

    def wait_event(self, request_id: str, name: str, *, timeout_seconds: float = 30.0) -> dict[str, Any]:
        return self._wait_for(
            lambda item: item.get("request_id") == request_id
            and item.get("type") == "event"
            and item.get("name") == name,
            timeout_seconds=timeout_seconds,
            request_id=request_id,
            terminal_before_match=True,
            timeout_message=f"event {name!r} exceeded the monotonic local deadline",
        )

    def wait_terminal(self, request_id: str, *, timeout_seconds: float = 30.0) -> dict[str, Any]:
        return self._wait_for(
            lambda item: item.get("request_id") == request_id and item.get("type") in {"completed", "failed"},
            timeout_seconds=timeout_seconds,
            request_id=request_id,
            timeout_message="request terminal exceeded the monotonic local deadline",
        )

    def request_status(self, request_id: str) -> dict[str, Any]:
        with self._condition:
            return copy.deepcopy(self._status.get(request_id, {"phase": "unknown", "terminal": None}))

    @staticmethod
    def _default_local_timeout(options: Mapping[str, Any]) -> float:
        return max(1.0, float(options.get("timeout_ms", 10_000)) / 1000.0 + 2.0)

    def _wait_for(
        self,
        predicate: Callable[[dict[str, Any]], bool],
        *,
        timeout_seconds: float | None = None,
        deadline: float | None = None,
        request_id: str | None = None,
        terminal_before_match: bool = False,
        timeout_message: str,
    ) -> dict[str, Any]:
        if deadline is None:
            if timeout_seconds is None or timeout_seconds <= 0:
                raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "local wait timeout must be positive")
            deadline = time.monotonic() + timeout_seconds
        with self._condition:
            while True:
                for _ in range(len(self._pending)):
                    message = self._pending.popleft()
                    if predicate(message):
                        return message
                    self._pending.append(message)
                if terminal_before_match and request_id is not None:
                    terminal = self._status.get(request_id, {}).get("terminal")
                    if isinstance(terminal, Mapping):
                        raise self._terminal_preempted_wait(dict(terminal))
                if self._reader_failure is not None:
                    raise self._reader_failure
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise ProtocolFailure(
                        ErrorCode.TIMEOUT_ACTION,
                        timeout_message,
                        details={"request_id": request_id, "timeout_seconds": timeout_seconds},
                    )
                self._condition.wait(remaining)

    @staticmethod
    def _terminal_preempted_wait(terminal: Mapping[str, Any]) -> ProtocolFailure:
        error = terminal.get("error")
        if isinstance(error, Mapping):
            try:
                code = ErrorCode(str(error.get("code")))
            except ValueError:
                code = ErrorCode.INVALID_PHASE
            return ProtocolFailure(
                code,
                str(error.get("message", "request failed before the target event")),
                retryable=bool(error.get("retryable", False)),
                details={"terminal": dict(terminal), **dict(error.get("details") or {})},
            )
        return ProtocolFailure(
            ErrorCode.INVALID_PHASE,
            "request completed before the target event was observed",
            details={"terminal": dict(terminal)},
        )

    def _reader_loop(self, stream: BinaryIO, generation: int) -> None:
        failure: ProtocolFailure | None = None
        try:
            while True:
                message = self._read_from_stream(stream)
                self.schemas.validate("protocol-v1", message)
                with self._condition:
                    if generation != self._reader_generation or stream is not self._stream:
                        return
                    sequence = message.get("seq")
                    if isinstance(sequence, int):
                        self.last_seq = max(self.last_seq, sequence)
                    self._remember(message)
                    self._pending.append(message)
                    self._condition.notify_all()
        except ProtocolFailure as exc:
            failure = exc
        except (OSError, ValueError) as exc:
            failure = ProtocolFailure(ErrorCode.PROCESS_EXIT, f"companion pipe read failed: {exc}")
        finally:
            if failure is not None:
                self._record_reader_failure(stream, generation, failure)
            try:
                stream.close()
            except OSError:
                pass

    def _record_reader_failure(self, stream: BinaryIO, generation: int, failure: ProtocolFailure) -> None:
        with self._condition:
            if generation != self._reader_generation or stream is not self._stream:
                return
            self._stream = None
            self._reader_failure = failure
            if failure.code == ErrorCode.PROCESS_EXIT:
                for request_id, status in list(self._status.items()):
                    if status.get("terminal") is not None:
                        continue
                    terminal = {
                        "protocol": PROTOCOL,
                        "schema_version": SCHEMA_VERSION,
                        "type": "failed",
                        "seq": self.last_seq,
                        "request_id": request_id,
                        "instance_id": self.instance_id,
                        "synthetic": True,
                        "error": failure.to_error(),
                    }
                    status["phase"] = "failed"
                    status["terminal"] = terminal
                    self._pending.append(terminal)
            self._condition.notify_all()

    def _remember(self, message: Mapping[str, Any]) -> None:
        request_id = message.get("request_id")
        if not isinstance(request_id, str):
            return
        phase = str(message.get("type", "unknown"))
        terminal = copy.deepcopy(dict(message)) if phase in {"completed", "failed"} else None
        self._status[request_id] = {"phase": phase, "terminal": terminal}

    def _read_from_stream(self, stream: BinaryIO) -> dict[str, Any]:
        line = bytearray()
        while True:
            if os.name == "nt" and self._windows_pipe_bytes_available(stream) == 0:
                # FileIO serializes simultaneous read/write calls on one object.
                # A blocking read would therefore prevent the broker thread from
                # writing the next request to this duplex Named Pipe. Peek first
                # so read() is only entered when at least one byte is available.
                time.sleep(0.005)
                continue
            chunk = stream.read(1)
            if not chunk:
                raise ProtocolFailure(ErrorCode.PROCESS_EXIT, "companion pipe closed before a complete message")
            line.extend(chunk)
            if len(line) > self.codec.limits.max_line_bytes:
                raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "companion JSONL line limit exceeded")
            if chunk == b"\n":
                break
        return self.codec.decode(bytes(line))

    @staticmethod
    def _windows_pipe_bytes_available(stream: BinaryIO) -> int:
        try:
            import ctypes
            import msvcrt
            from ctypes import wintypes

            kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
            kernel32.PeekNamedPipe.argtypes = [
                wintypes.HANDLE,
                wintypes.LPVOID,
                wintypes.DWORD,
                wintypes.LPVOID,
                ctypes.POINTER(wintypes.DWORD),
                wintypes.LPVOID,
            ]
            kernel32.PeekNamedPipe.restype = wintypes.BOOL
            available = wintypes.DWORD()
            handle = wintypes.HANDLE(msvcrt.get_osfhandle(stream.fileno()))
            if not kernel32.PeekNamedPipe(handle, None, 0, None, ctypes.byref(available), None):
                error = ctypes.get_last_error()
                raise OSError(error, f"PeekNamedPipe failed with WinError {error}")
            return int(available.value)
        except (AttributeError, ImportError, io.UnsupportedOperation):
            # In-memory/fake test streams do not expose a Windows OS handle.
            return 1

    def _write_message(self, message: Mapping[str, Any]) -> None:
        with self._write_lock:
            with self._condition:
                if self._stream is None:
                    raise self._reader_failure or ProtocolFailure(ErrorCode.BROKER_EXIT, "companion connection is not active")
                stream = self._stream
            self._write_to_stream(stream, message)

    def _write_to_stream(self, stream: BinaryIO, message: Mapping[str, Any]) -> None:
        try:
            stream.write(self.codec.encode(message))
            stream.flush()
        except OSError as exc:
            failure = ProtocolFailure(ErrorCode.PROCESS_EXIT, f"companion pipe write failed: {exc}")
            with self._condition:
                if stream is self._stream:
                    self._reader_failure = failure
                    self._condition.notify_all()
            raise failure from exc

    def _require_identity(self, value: Mapping[str, Any]) -> None:
        if value.get("session_id") != self.session_id or value.get("instance_id") != self.instance_id:
            raise ProtocolFailure(
                ErrorCode.SERVER_AUTH_FAILED,
                "companion session or instance identity mismatch",
            )
