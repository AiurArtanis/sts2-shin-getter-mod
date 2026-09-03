from __future__ import annotations

import argparse
import json
import os
import secrets
import shutil
import threading
from dataclasses import dataclass, replace
from multiprocessing.connection import Listener
from pathlib import Path
from typing import Any, Mapping

from .broker_client import control_address
from .companion_client import CompanionClient
from .errors import ErrorCode, ProtocolFailure
from .evidence import EvidenceBundle
from .legacy import FileLock
from .process_manager import (
    ExactProcessManager,
    OwnedProcess,
    ProcessJob,
    ProcessRecord,
    build_game_environment,
    capture_process_identity,
    redact_environment,
    validate_isolated_user_data,
)
from .runtime_session import ControlSession, InstanceState, append_jsonl, atomic_write_json, is_reparse_point


_CONTROL_LIMIT = 1024 * 1024
_SENSITIVE_PARTS = ("token", "proof", "secret", "credential", "password", "settings_template")


def _redact(value: Any) -> Any:
    if isinstance(value, Mapping):
        return {
            str(key): _redact(child)
            for key, child in value.items()
            if not any(part in str(key).lower() for part in _SENSITIVE_PARTS)
        }
    if isinstance(value, (list, tuple)):
        return [_redact(child) for child in value]
    return value


@dataclass
class BrokerInstance:
    process: OwnedProcess
    record: ProcessRecord
    companion: CompanionClient
    token: bytearray

    def destroy_secret(self) -> None:
        for index in range(len(self.token)):
            self.token[index] = 0


class SessionBroker:
    def __init__(self, session: ControlSession, broker_epoch: str) -> None:
        self.session = session
        self.broker_epoch = broker_epoch
        self.processes = ExactProcessManager()
        self.job = ProcessJob()
        self.instances: dict[str, BrokerInstance] = {}
        self.stop_requested = False
        self.finalized = False
        self._gate = threading.RLock()

    def handle(self, operation: str, arguments: Mapping[str, Any]) -> dict[str, Any]:
        journal = not self.finalized and not operation.startswith("evidence.")
        if journal:
            append_jsonl(
                self.session.paths.requests_jsonl,
                {"kind": "broker_request", "operation": operation, "arguments": _redact(arguments)},
            )
        try:
            with self._gate:
                result = self._dispatch(operation, arguments)
        except ProtocolFailure:
            raise
        except Exception as exc:
            raise ProtocolFailure(ErrorCode.BROKER_EXIT, f"broker operation failed: {exc}") from exc
        if journal and not self.finalized:
            append_jsonl(
                self.session.paths.broker_events_jsonl,
                {"kind": "broker_result", "operation": operation, "result": _redact(result)},
            )
        return result

    def _dispatch(self, operation: str, arguments: Mapping[str, Any]) -> dict[str, Any]:
        if operation == "broker.status":
            return {
                "broker_epoch": self.broker_epoch,
                "pid": os.getpid(),
                "instance_count": len(self.instances),
                "finalized": self.finalized,
                "job_object": self.job.supported,
            }
        if operation == "process.start":
            return self._start(arguments)
        if operation == "process.list":
            return {"instances": [self._status(identifier) for identifier in sorted(self.instances)]}
        if operation == "process.status":
            return self._status(str(arguments.get("instance_id", "")))
        if operation == "process.stop":
            return self._stop(
                str(arguments.get("instance_id", "")),
                grace_seconds=float(arguments.get("grace_seconds", 5.0)),
                force=bool(arguments.get("force", False)),
            )
        if operation == "runtime.handshake":
            instance = self._instance(arguments)
            return {"handshake": dict(instance.companion.hello_ack_body)}
        if operation == "runtime.connect":
            instance = self._instance(arguments)
            body = instance.companion.connect(
                timeout_seconds=float(arguments.get("timeout_seconds", 30.0)),
                resume_from_seq=int(arguments.get("resume_from_seq", instance.companion.last_seq)),
            )
            return {"handshake": body, "resume_from_seq": instance.companion.last_seq}
        if operation == "runtime.exec":
            instance = self._instance(arguments)
            terminal = instance.companion.request(
                str(arguments.get("command", "")),
                self._object(arguments.get("args", {}), "args"),
                wait_for=str(arguments.get("wait_for", "immediate")),
                timeout_ms=int(arguments.get("timeout_ms", 10_000)),
                request_id=str(arguments["request_id"]) if arguments.get("request_id") else None,
            )
            self._bridge_event(str(arguments.get("instance_id")), terminal)
            return terminal
        if operation == "runtime.dispatch":
            instance = self._instance(arguments)
            request_id = instance.companion.dispatch(
                str(arguments.get("command", "")),
                self._object(arguments.get("args", {}), "args"),
                wait_for=str(arguments.get("wait_for", "immediate")),
                timeout_ms=int(arguments.get("timeout_ms", 10_000)),
                request_id=str(arguments["request_id"]) if arguments.get("request_id") else None,
            )
            return {"request_id": request_id, "terminal": False}
        if operation == "runtime.wait_event":
            instance = self._instance(arguments)
            event = instance.companion.wait_event(str(arguments.get("request_id", "")), str(arguments.get("name", "")))
            self._bridge_event(str(arguments.get("instance_id")), event)
            return event
        if operation == "runtime.wait_terminal":
            instance = self._instance(arguments)
            terminal = instance.companion.wait_terminal(str(arguments.get("request_id", "")))
            self._bridge_event(str(arguments.get("instance_id")), terminal)
            return terminal
        if operation == "runtime.request_status":
            instance = self._instance(arguments)
            return instance.companion.request_status(str(arguments.get("request_id", "")))
        if operation == "evidence.finalize":
            if self.finalized:
                raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, "session evidence is already finalized")
            manifest = EvidenceBundle(self.session.paths.root).finalize(self._object(arguments.get("metadata"), "metadata"))
            self.finalized = True
            return manifest
        if operation == "evidence.verify":
            return EvidenceBundle(self.session.paths.root).verify()
        if operation == "session.close":
            for identifier in list(self.instances):
                if self.instances[identifier].process.process.poll() is None:
                    self._stop(identifier, grace_seconds=1.0, force=True)
            self.stop_requested = True
            return {"closed": True, "instance_count": len(self.instances)}
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"unknown broker operation: {operation}")

    @staticmethod
    def _object(value: Any, name: str) -> dict[str, Any]:
        if not isinstance(value, Mapping):
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"{name} must be an object")
        return dict(value)

    def _instance(self, arguments: Mapping[str, Any]) -> BrokerInstance:
        identifier = str(arguments.get("instance_id", ""))
        try:
            return self.instances[identifier]
        except KeyError as exc:
            raise ProtocolFailure(ErrorCode.NOT_FOUND, f"unknown broker instance: {identifier}") from exc

    def _start(self, arguments: Mapping[str, Any]) -> dict[str, Any]:
        if self.finalized:
            raise ProtocolFailure(ErrorCode.EVIDENCE_TAMPERED, "finalized sessions reject new processes")
        identifier = str(arguments.get("instance_id", ""))
        if identifier in self.instances:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "instance is already owned by this broker")
        role = str(arguments.get("role", "single"))
        raw_argv = arguments.get("argv")
        if not isinstance(raw_argv, list) or not raw_argv:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "process.start argv must be a non-empty array")
        argv = [str(item) for item in raw_argv]
        cwd = Path(str(arguments.get("cwd", ""))).expanduser()
        if not cwd.is_absolute() or not cwd.is_dir():
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "process.start cwd must be an existing absolute directory")
        adapter_expected = str(arguments.get("adapter_expected", "sts2-0.111"))
        timeout_seconds = float(arguments.get("timeout_seconds", 30.0))

        public = self.session.define_instance(identifier, role=role)
        root = Path(public["root"])
        app_data = root / "user-data" / "appdata"
        local_app_data = root / "user-data" / "localappdata"
        app_data.mkdir(parents=True)
        local_app_data.mkdir(parents=True)
        settings_template = arguments.get("settings_template")
        if settings_template:
            template = Path(str(settings_template)).expanduser().resolve(strict=True)
            if not template.is_file() or is_reparse_point(template):
                raise ProtocolFailure(ErrorCode.ISOLATION_BREACH, "settings template must be a regular non-reparse file")
            client_id = int(arguments.get("client_id", 1))
            if client_id < 1:
                raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "client_id must be positive")
            destination = app_data / "SlayTheSpire2" / "default" / str(client_id) / "settings.save"
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(template, destination)
        self.session.transition_instance(identifier, InstanceState.STARTING)
        pipe_name = f"sts2-test-{self.session.load_index()['session_id'][:16]}-{identifier}-{secrets.token_hex(16)}"
        token = bytearray(secrets.token_bytes(32))
        environment = build_game_environment(
            base={},
            session_id=self.session.load_index()["session_id"],
            instance_id=identifier,
            pipe_name=pipe_name,
            token=bytes(token),
            output_root=root,
            app_data=app_data,
            local_app_data=local_app_data,
        )
        owned: OwnedProcess | None = None
        companion: CompanionClient | None = None
        try:
            owned = self.processes.spawn(
                argv,
                cwd=cwd.resolve(),
                environment=environment,
                stdout_path=root / "stdout.log",
                stderr_path=root / "stderr.log",
            )
            environment.clear()
            self.job.assign(owned.process)
            self.session.transition_instance(identifier, InstanceState.PIPE_WAITING)
            record = ProcessRecord(
                session_id=self.session.load_index()["session_id"],
                instance_id=identifier,
                role=role,
                identity=owned.identity,
                command_argv_redacted=argv,
                environment_allowlist=redact_environment(
                    {
                        "STS2_TEST_ENABLE": "1",
                        "STS2_TEST_SESSION_ID": "",
                        "STS2_TEST_INSTANCE_ID": "",
                        "STS2_TEST_PIPE": "",
                        "STS2_TEST_OUTPUT_ROOT": "",
                        "APPDATA": "",
                        "LOCALAPPDATA": "",
                    }
                )["names"],
                pipe_name=pipe_name,
                adapter_expected=adapter_expected,
                state=InstanceState.PIPE_WAITING.value,
            )
            atomic_write_json(root / "process.json", record.to_dict())
            self.session.transition_instance(identifier, InstanceState.AUTHENTICATING)
            companion = CompanionClient(
                pipe_name=pipe_name,
                session_id=record.session_id,
                instance_id=identifier,
                token=bytes(token),
                expected_adapter_id=adapter_expected,
            )
            handshake = companion.connect(timeout_seconds=timeout_seconds)
            runtime = handshake.get("runtime")
            if not isinstance(runtime, Mapping) or not isinstance(runtime.get("user_data_path"), str):
                raise ProtocolFailure(ErrorCode.ISOLATION_BREACH, "companion did not report its user-data path")
            validate_isolated_user_data(Path(runtime["user_data_path"]), root)
            reported_output = runtime.get("output_root")
            if reported_output is not None:
                validate_isolated_user_data(Path(str(reported_output)), root)
            self.session.transition_instance(identifier, InstanceState.READY)
            record = replace(record, state=InstanceState.READY.value)
            atomic_write_json(root / "process.json", record.to_dict())
            atomic_write_json(root / "runtime.json", _redact(handshake))
            instance = BrokerInstance(owned, record, companion, token)
            self.instances[identifier] = instance
            return {"process": record.to_dict(), "handshake": handshake}
        except Exception:
            environment.clear()
            if companion is not None:
                companion.close()
            if owned is not None and owned.process.poll() is None:
                try:
                    self.processes.stop(owned.identity, grace_seconds=0.1, force=True)
                except Exception:
                    pass
            for index in range(len(token)):
                token[index] = 0
            try:
                current = self.session.instance(identifier)["state"]
                if current in {InstanceState.PIPE_WAITING.value, InstanceState.AUTHENTICATING.value}:
                    self.session.transition_instance(identifier, InstanceState.FAILED)
            except Exception:
                pass
            raise

    def _status(self, identifier: str) -> dict[str, Any]:
        try:
            instance = self.instances[identifier]
        except KeyError as exc:
            raise ProtocolFailure(ErrorCode.NOT_FOUND, f"unknown broker instance: {identifier}") from exc
        status = self.processes.status(instance.record.identity)
        return {
            "instance_id": identifier,
            "state": instance.record.state,
            "connected": instance.companion.connected,
            "last_seq": instance.companion.last_seq,
            **status,
        }

    def _stop(self, identifier: str, *, grace_seconds: float, force: bool) -> dict[str, Any]:
        instance = self._instance({"instance_id": identifier})
        if instance.record.state != InstanceState.EXITED.value:
            try:
                self.session.transition_instance(identifier, InstanceState.STOPPING)
            except ProtocolFailure as exc:
                if exc.code != ErrorCode.INVALID_PHASE:
                    raise

        def shutdown() -> None:
            if instance.companion.connected:
                terminal = instance.companion.request("runtime.shutdown", {}, wait_for="immediate", timeout_ms=5_000)
                self._bridge_event(identifier, terminal)

        try:
            result = self.processes.stop(
                instance.record.identity,
                grace_seconds=grace_seconds,
                force=force,
                request_shutdown=shutdown,
            )
        finally:
            instance.companion.close()
            instance.destroy_secret()
        instance.record = replace(
            instance.record,
            state=InstanceState.EXITED.value,
            exit_code=result.get("exit_code"),
        )
        atomic_write_json(self.session.paths.instances / identifier / "process.json", instance.record.to_dict())
        try:
            self.session.transition_instance(identifier, InstanceState.EXITED)
        except ProtocolFailure as exc:
            if exc.code != ErrorCode.INVALID_PHASE:
                raise
        return result

    def _bridge_event(self, identifier: str, message: Mapping[str, Any]) -> None:
        append_jsonl(self.session.paths.instances / identifier / "bridge-events.jsonl", _redact(message))

    def close(self) -> None:
        for instance in self.instances.values():
            instance.companion.close()
            instance.destroy_secret()
        self.job.close()


def _response(result: dict[str, Any]) -> bytes:
    return json.dumps({"ok": True, "result": result}, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def _error_response(error: ProtocolFailure) -> bytes:
    return json.dumps({"ok": False, "error": error.to_error()}, ensure_ascii=False, separators=(",", ":")).encode("utf-8")


def serve(session_root: Path, repository_root: Path) -> int:
    session = ControlSession.open(session_root, repository_root=repository_root)
    broker_epoch = secrets.token_hex(16)
    pipe_name = f"sts2-broker-{session.load_index()['session_id'][:16]}-{secrets.token_hex(16)}"
    lease = FileLock(session.paths.session_lock, timeout=0.1)
    lease.__enter__()
    listener: Listener | None = None
    broker = SessionBroker(session, broker_epoch)
    try:
        identity = capture_process_identity(os.getpid())
        session.record_broker(
            {
                "pid": identity.pid,
                "process_start_time_utc": identity.process_start_time_utc,
                "executable_path": identity.executable_path,
                "executable_sha256": identity.executable_sha256,
                "control_pipe": pipe_name,
                "broker_epoch": broker_epoch,
            }
        )
        address = control_address(pipe_name, session.paths.root)
        listener = Listener(address, family="AF_PIPE" if os.name == "nt" else "AF_UNIX", authkey=None)
        while not broker.stop_requested:
            connection = listener.accept()
            with connection:
                try:
                    raw = connection.recv_bytes(_CONTROL_LIMIT)
                    request = json.loads(raw)
                    if not isinstance(request, dict) or not isinstance(request.get("operation"), str):
                        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "invalid broker request envelope")
                    arguments = request.get("arguments", {})
                    if not isinstance(arguments, dict):
                        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "broker arguments must be an object")
                    payload = _response(broker.handle(request["operation"], arguments))
                except ProtocolFailure as exc:
                    payload = _error_response(exc)
                except (UnicodeDecodeError, json.JSONDecodeError, EOFError, OSError) as exc:
                    payload = _error_response(ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"invalid broker request: {exc}"))
                connection.send_bytes(payload)
        return 0
    finally:
        if listener is not None:
            listener.close()
        broker.close()
        lease.__exit__(None, None, None)
        if os.name != "nt":
            socket_path = Path(control_address(pipe_name, session.paths.root))
            if socket_path.exists():
                socket_path.unlink()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="TEST-ONLY Slay the Spire 2 control-session broker")
    parser.add_argument("--session-root", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    args = parser.parse_args(argv)
    return serve(args.session_root.resolve(strict=True), args.repository_root.resolve(strict=True))


if __name__ == "__main__":
    raise SystemExit(main())
