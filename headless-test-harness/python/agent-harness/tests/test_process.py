from __future__ import annotations

import json
import os
import subprocess
import sys
import threading
import time
from dataclasses import replace
from pathlib import Path

import pytest

from cli_anything.slaythespare2_111_beta.core.errors import ErrorCode, ProtocolFailure
from cli_anything.slaythespare2_111_beta.core.process_manager import (
    BrokerIdentity,
    ExactProcessManager,
    ProcessIdentity,
    ProcessRecord,
    WriteSentinel,
    assert_broker_alive,
    build_game_environment,
    capture_process_identity,
    identity_matches,
    redact_environment,
    validate_isolated_user_data,
)
from cli_anything.slaythespare2_111_beta.core.runtime_session import (
    ControlSession,
    InstanceState,
    RuntimePathGuard,
    append_jsonl,
    atomic_write_json,
    default_runtime_root,
    default_state_root,
    validate_identifier,
)


def test_default_runtime_root_uses_local_app_data(tmp_path: Path) -> None:
    result = default_runtime_root({"LOCALAPPDATA": str(tmp_path)})
    assert result == tmp_path / "cli-anything" / "slaythespare2-111-beta" / "sessions"


def test_default_runtime_root_falls_back_to_temp(tmp_path: Path) -> None:
    result = default_runtime_root({"TEMP": str(tmp_path)})
    assert result == tmp_path / "cli-anything" / "slaythespare2-111-beta" / "sessions"


def test_default_state_root_never_uses_project_tree(tmp_path: Path) -> None:
    project = tmp_path / "reverse-source"
    project.mkdir()
    result = default_state_root({"LOCALAPPDATA": str(tmp_path / "local")})
    assert result == tmp_path / "local" / "cli-anything" / "slaythespare2-111-beta" / "state"
    assert project not in result.parents


@pytest.mark.parametrize("value", ["session-1", "host", "client-1000", "a.b_c"])
def test_identifier_accepts_safe_values(value: str) -> None:
    assert validate_identifier(value) == value


@pytest.mark.parametrize("value", ["", "../escape", "a/b", "a\\b", ".hidden", "x" * 65])
def test_identifier_rejects_unsafe_values(value: str) -> None:
    with pytest.raises(ProtocolFailure) as failure:
        validate_identifier(value)
    assert failure.value.code == ErrorCode.INVALID_ARGUMENT


def test_runtime_path_guard_allows_external_root(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    runtime = tmp_path / "runtime"
    assert RuntimePathGuard(repository, []).validate(runtime) == runtime.resolve()


def test_runtime_path_guard_rejects_repository_root(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    with pytest.raises(ProtocolFailure) as failure:
        RuntimePathGuard(repository, []).validate(repository)
    assert failure.value.code == ErrorCode.ISOLATION_BREACH


def test_runtime_path_guard_rejects_repository_descendant(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    with pytest.raises(ProtocolFailure):
        RuntimePathGuard(repository, []).validate(repository / "runtime")


def test_runtime_path_guard_rejects_protected_game_tree(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    game = tmp_path / "game"
    repository.mkdir()
    game.mkdir()
    with pytest.raises(ProtocolFailure):
        RuntimePathGuard(repository, [game]).validate(game / "sessions")


def test_runtime_path_guard_rejects_relative_path(tmp_path: Path) -> None:
    with pytest.raises(ProtocolFailure):
        RuntimePathGuard(tmp_path / "repo", []).validate(Path("relative/runtime"))


def test_runtime_path_guard_rejects_reparse_ancestor(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    from cli_anything.slaythespare2_111_beta.core import runtime_session

    repository = tmp_path / "repo"
    runtime = tmp_path / "runtime"
    repository.mkdir()
    runtime.mkdir()
    monkeypatch.setattr(runtime_session, "is_reparse_point", lambda path: path == runtime)
    with pytest.raises(ProtocolFailure):
        RuntimePathGuard(repository, []).validate(runtime / "child")


def test_control_session_creates_frozen_layout(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    runtime = tmp_path / "runtime"
    repository.mkdir()
    session = ControlSession.create(runtime, "session-1", repository_root=repository)
    expected = [
        session.paths.session_json,
        session.paths.requests_jsonl,
        session.paths.broker_events_jsonl,
        session.paths.instances,
        session.paths.evidence,
    ]
    assert all(path.exists() for path in expected)


def test_control_session_index_contains_no_secret(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    session = ControlSession.create(tmp_path / "runtime", "session-1", repository_root=repository)
    text = session.paths.session_json.read_text(encoding="utf-8")
    assert "token" not in text.lower()
    assert "proof" not in text.lower()


def test_control_session_persists_and_revalidates_protected_roots(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    protected = tmp_path / "steam"
    repository.mkdir()
    protected.mkdir()
    session = ControlSession.create(
        tmp_path / "runtime",
        "session-1",
        repository_root=repository,
        protected_roots=[protected],
    )
    index = session.load_index()
    assert index["repository_root"] == str(repository.resolve())
    assert index["protected_roots"] == [str(repository.resolve()), str(protected.resolve())]

    reopened = ControlSession.open(
        session.paths.root,
        repository_root=repository,
        protected_roots=[protected],
    )
    assert reopened.paths.root == session.paths.root


def test_control_session_open_rejects_preexisting_session_under_new_protected_root(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    runtime = tmp_path / "runtime"
    repository.mkdir()
    session = ControlSession.create(runtime, "session-1", repository_root=repository)
    with pytest.raises(ProtocolFailure) as failure:
        ControlSession.open(
            session.paths.root,
            repository_root=repository,
            protected_roots=[runtime],
        )
    assert failure.value.code == ErrorCode.ISOLATION_BREACH


def test_control_session_rejects_process_cwd_in_any_protected_tree(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    protected = tmp_path / "daily-deploy"
    repository.mkdir()
    protected.mkdir()
    session = ControlSession.create(
        tmp_path / "runtime",
        "session-1",
        repository_root=repository,
        protected_roots=[protected],
    )
    with pytest.raises(ProtocolFailure) as failure:
        session.validate_process_cwd(protected)
    assert failure.value.code == ErrorCode.ISOLATION_BREACH


def test_broker_record_rejects_secret_fields(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    session = ControlSession.create(tmp_path / "runtime", "session-1", repository_root=repository)
    with pytest.raises(ProtocolFailure):
        session.record_broker({"pid": 1, "token": "secret"})


def test_broker_record_persists_only_public_identity(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    session = ControlSession.create(tmp_path / "runtime", "session-1", repository_root=repository)
    session.record_broker(
        {"pid": 42, "process_start_time_utc": "2026-09-03T00:00:00Z", "executable_path": "python", "executable_sha256": "a" * 64, "control_pipe": "sts2-broker-x", "broker_epoch": "epoch"}
    )
    data = json.loads(session.paths.broker_json.read_text(encoding="utf-8"))
    assert data["pid"] == 42
    assert sorted(data) == ["broker_epoch", "control_pipe", "executable_path", "executable_sha256", "pid", "process_start_time_utc"]


def test_instance_state_machine_accepts_documented_path(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    session = ControlSession.create(tmp_path / "runtime", "session-1", repository_root=repository)
    session.define_instance("solo", role="single")
    for state in [
        InstanceState.STARTING,
        InstanceState.PIPE_WAITING,
        InstanceState.AUTHENTICATING,
        InstanceState.READY,
        InstanceState.BUSY,
        InstanceState.READY,
        InstanceState.STOPPING,
        InstanceState.EXITED,
    ]:
        session.transition_instance("solo", state)
    assert session.instance("solo")["state"] == "exited"


def test_instance_state_machine_rejects_illegal_transition(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    session = ControlSession.create(tmp_path / "runtime", "session-1", repository_root=repository)
    session.define_instance("solo", role="single")
    with pytest.raises(ProtocolFailure):
        session.transition_instance("solo", InstanceState.READY)


def test_instance_layout_is_per_instance(tmp_path: Path) -> None:
    repository = tmp_path / "repo"
    repository.mkdir()
    session = ControlSession.create(tmp_path / "runtime", "session-1", repository_root=repository)
    record = session.define_instance("client-1000", role="client")
    root = Path(record["root"])
    assert (root / "blobs").is_dir()
    assert record["process_path"] == str(root / "process.json")


def test_atomic_write_json_is_lf_and_no_bom(tmp_path: Path) -> None:
    path = tmp_path / "value.json"
    atomic_write_json(path, {"b": 2, "a": "中文"})
    raw = path.read_bytes()
    assert raw.endswith(b"\n")
    assert not raw.startswith(b"\xef\xbb\xbf")
    assert json.loads(raw) == {"a": "中文", "b": 2}


def test_atomic_write_json_remains_valid_under_threads(tmp_path: Path) -> None:
    path = tmp_path / "value.json"
    threads = [threading.Thread(target=atomic_write_json, args=(path, {"value": i})) for i in range(8)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()
    assert json.loads(path.read_text(encoding="utf-8"))["value"] in range(8)


def test_append_jsonl_is_stable(tmp_path: Path) -> None:
    path = tmp_path / "events.jsonl"
    append_jsonl(path, {"z": 2, "a": 1})
    append_jsonl(path, {"n": 2})
    assert path.read_bytes() == b'{"a":1,"z":2}\n{"n":2}\n'


def test_build_game_environment_keeps_token_in_child_block_only(tmp_path: Path) -> None:
    environment = build_game_environment(
        base={"PATH": "x"}, session_id="session-1", instance_id="solo",
        pipe_name="sts2-test-pipe", token=bytes(range(32)), output_root=tmp_path,
    )
    assert environment["STS2_TEST_ENABLE"] == "1"
    assert environment["STS2_TEST_TOKEN"]
    assert environment["STS2_TEST_OUTPUT_ROOT"] == str(tmp_path.resolve())


def test_redacted_environment_contains_names_not_values(tmp_path: Path) -> None:
    environment = build_game_environment(
        base={}, session_id="session-1", instance_id="solo", pipe_name="pipe",
        token=b"x" * 32, output_root=tmp_path,
    )
    redacted = redact_environment(environment)
    assert "STS2_TEST_TOKEN" not in redacted["names"]
    assert "secret" not in json.dumps(redacted).lower()
    assert redacted["has_companion_token"] is True


def test_capture_current_process_identity() -> None:
    identity = capture_process_identity(os.getpid())
    assert identity.pid == os.getpid()
    assert Path(identity.executable_path).samefile(sys.executable)
    assert len(identity.executable_sha256) == 64


@pytest.mark.parametrize("field", ["pid", "process_start_time_utc", "executable_path", "executable_sha256"])
def test_identity_match_binds_all_fields(field: str) -> None:
    identity = ProcessIdentity(42, "2026-09-03T00:00:00.0000000Z", "C:/Game.exe", "a" * 64)
    replacements = {
        "pid": 43,
        "process_start_time_utc": "2026-09-03T00:00:01.0000000Z",
        "executable_path": "C:/Other.exe",
        "executable_sha256": "b" * 64,
    }
    assert identity_matches(identity, replace(identity, **{field: replacements[field]})) is False


def test_process_record_round_trip() -> None:
    identity = ProcessIdentity(42, "2026-09-03T00:00:00Z", "C:/Game.exe", "a" * 64)
    record = ProcessRecord(
        session_id="s", instance_id="solo", role="single", identity=identity,
        command_argv_redacted=["Game.exe", "--headless"], environment_allowlist=["APPDATA"],
        pipe_name="pipe", adapter_expected="sts2-0.111", state="ready",
    )
    assert ProcessRecord.from_dict(record.to_dict()) == record


def test_exact_stop_rejects_wrong_identity_without_killing(tmp_path: Path) -> None:
    manager = ExactProcessManager()
    owned = manager.spawn(
        [sys.executable, "-c", "import time; time.sleep(60)"], cwd=tmp_path,
        environment={}, stdout_path=tmp_path / "stdout.log", stderr_path=tmp_path / "stderr.log",
    )
    try:
        wrong = replace(owned.identity, process_start_time_utc="1970-01-01T00:00:00Z")
        with pytest.raises(ProtocolFailure) as failure:
            manager.stop(wrong, grace_seconds=0.1, force=True)
        assert failure.value.code == ErrorCode.PROCESS_IDENTITY_MISMATCH
        assert owned.process.poll() is None
    finally:
        manager.stop(owned.identity, grace_seconds=0.1, force=True)


def test_exact_stop_terminates_owned_child(tmp_path: Path) -> None:
    manager = ExactProcessManager()
    owned = manager.spawn(
        [sys.executable, "-c", "import time; time.sleep(60)"], cwd=tmp_path,
        environment={}, stdout_path=tmp_path / "stdout.log", stderr_path=tmp_path / "stderr.log",
    )
    result = manager.stop(owned.identity, grace_seconds=0.1, force=True)
    assert result["pid"] == owned.identity.pid
    assert result["state"] == "exited"
    assert owned.process.poll() is not None


def test_assert_broker_alive_maps_missing_process_to_broker_exit() -> None:
    identity = BrokerIdentity(999_999_999, "1970-01-01T00:00:00Z", "C:/missing.exe", "0" * 64)
    with pytest.raises(ProtocolFailure) as failure:
        assert_broker_alive(identity)
    assert failure.value.code == ErrorCode.BROKER_EXIT


def test_validate_isolated_user_data_accepts_descendant(tmp_path: Path) -> None:
    expected = tmp_path / "user-data"
    actual = expected / "SlayTheSpire2"
    actual.mkdir(parents=True)
    assert validate_isolated_user_data(actual, expected) == actual.resolve()


def test_validate_isolated_user_data_rejects_shared_path(tmp_path: Path) -> None:
    expected = tmp_path / "user-data"
    shared = tmp_path / "shared"
    shared.mkdir()
    with pytest.raises(ProtocolFailure) as failure:
        validate_isolated_user_data(shared, expected)
    assert failure.value.code == ErrorCode.ISOLATION_BREACH


def test_write_sentinel_detects_protected_creation(tmp_path: Path) -> None:
    protected = tmp_path / "steam"
    protected.mkdir()
    sentinel = WriteSentinel([protected])
    before = sentinel.capture()
    (protected / "unexpected.txt").write_text("changed", encoding="utf-8")
    with pytest.raises(ProtocolFailure) as failure:
        sentinel.assert_unchanged(before)
    assert failure.value.code == ErrorCode.ISOLATION_BREACH
    assert "unexpected.txt" in json.dumps(failure.value.details)


def test_write_sentinel_ignores_allowed_root(tmp_path: Path) -> None:
    protected = tmp_path / "steam"
    allowed = tmp_path / "runtime"
    protected.mkdir()
    allowed.mkdir()
    sentinel = WriteSentinel([protected], allowed_roots=[allowed])
    before = sentinel.capture()
    (allowed / "ok.txt").write_text("ok", encoding="utf-8")
    sentinel.assert_unchanged(before)


def test_process_exit_is_reflected_in_status(tmp_path: Path) -> None:
    manager = ExactProcessManager()
    owned = manager.spawn(
        [sys.executable, "-c", "raise SystemExit(7)"], cwd=tmp_path,
        environment={}, stdout_path=tmp_path / "stdout.log", stderr_path=tmp_path / "stderr.log",
    )
    owned.process.wait(timeout=5)
    status = manager.status(owned.identity)
    assert status["alive"] is False
    assert status["exit_code"] == 7


def test_spawn_records_argument_vector_without_shell(tmp_path: Path) -> None:
    marker = tmp_path / "space marker.txt"
    manager = ExactProcessManager()
    owned = manager.spawn(
        [sys.executable, "-c", "import pathlib,sys; pathlib.Path(sys.argv[1]).write_text('ok')", str(marker)],
        cwd=tmp_path, environment={}, stdout_path=tmp_path / "stdout.log", stderr_path=tmp_path / "stderr.log",
    )
    owned.process.wait(timeout=5)
    manager.status(owned.identity)
    assert marker.read_text(encoding="utf-8") == "ok"
    assert owned.argv[-1] == str(marker)


def test_spawn_real_child_receives_only_system_allowlist_and_explicit_environment(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("STS2_PARENT_SECRET_SHOULD_NOT_LEAK", "parent-secret")
    output = tmp_path / "child-environment.json"
    script = (
        "import json,os,pathlib,sys;"
        "pathlib.Path(sys.argv[1]).write_text(json.dumps(dict(os.environ)), encoding='utf-8')"
    )
    manager = ExactProcessManager()
    owned = manager.spawn(
        [sys.executable, "-c", script, str(output)],
        cwd=tmp_path,
        environment={"STS2_EXPLICIT_TEST_VALUE": "present"},
        stdout_path=tmp_path / "stdout.log",
        stderr_path=tmp_path / "stderr.log",
    )
    owned.process.wait(timeout=10)
    manager.status(owned.identity)
    child = json.loads(output.read_text(encoding="utf-8"))
    assert child["STS2_EXPLICIT_TEST_VALUE"] == "present"
    assert "STS2_PARENT_SECRET_SHOULD_NOT_LEAK" not in child
