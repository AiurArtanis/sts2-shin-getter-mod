from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Mapping, Sequence

import pytest

from cli_anything.slaythespare2_111_beta.core.evidence import evidence_metadata_template


CLI_NAME = "cli-anything-slaythespare2-111-beta"
PROFILE_ENV = "STS2_HEADLESS_RUNTIME_PROFILE"
RELEASE_GATE_ENV = "STS2_HEADLESS_RUNTIME_RELEASE_GATE"

pytestmark = [pytest.mark.runtime, pytest.mark.runtime_release]


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _json_object(raw: str, *, source: str) -> dict[str, Any]:
    try:
        value = json.loads(raw)
    except json.JSONDecodeError as exc:
        pytest.fail(f"{source} did not emit one JSON value: {exc}\n{raw}")
    if not isinstance(value, dict):
        pytest.fail(f"{source} must emit a JSON object")
    return value


@pytest.fixture(scope="session")
def runtime_release_profile() -> dict[str, Any]:
    release_gate = os.environ.get(RELEASE_GATE_ENV) == "1"
    configured = os.environ.get(PROFILE_ENV)
    if not release_gate:
        pytest.skip(
            f"real runtime gate is opt-in; set {RELEASE_GATE_ENV}=1 and {PROFILE_ENV}=<absolute-profile.json>"
        )
    if not configured:
        pytest.fail(
            f"release gate requires an explicit real-runtime profile: {PROFILE_ENV}=<absolute-profile.json>"
        )
    if os.environ.get("CLI_ANYTHING_FORCE_INSTALLED") != "1":
        pytest.fail("release gate requires CLI_ANYTHING_FORCE_INSTALLED=1")

    profile_path = Path(configured).expanduser()
    if not profile_path.is_absolute() or not profile_path.is_file():
        pytest.fail(f"runtime profile must be an existing absolute file: {profile_path}")
    profile = _json_object(profile_path.read_text(encoding="utf-8"), source=str(profile_path))
    if profile.get("schema") != "sts2-runtime-release-profile/v1":
        pytest.fail("runtime profile schema must be sts2-runtime-release-profile/v1")

    required_paths = (
        "project_root",
        "runtime_root",
        "staging_root",
        "game_executable",
        "settings_template",
        "companion_project",
        "companion_build_dll",
        "companion_staged_dll",
        "companion_manifest",
        "shin_getter_manifest",
        "shin_getter_dll",
    )
    for key in required_paths:
        raw = profile.get(key)
        if not isinstance(raw, str) or not Path(raw).is_absolute():
            pytest.fail(f"runtime profile field {key!r} must be an absolute path")
    for key in required_paths:
        if key == "runtime_root":
            continue
        if not Path(profile[key]).exists():
            pytest.fail(f"runtime profile path does not exist: {key}={profile[key]}")

    protected = profile.get("protected_roots")
    if not isinstance(protected, list) or not protected:
        pytest.fail("runtime profile requires a non-empty protected_roots array")
    for root in protected:
        if not isinstance(root, str) or not Path(root).is_absolute() or not Path(root).exists():
            pytest.fail(f"protected root must be an existing absolute path: {root!r}")
    game_args = profile.get("game_args")
    if not isinstance(game_args, list) or not all(isinstance(item, str) for item in game_args):
        pytest.fail("runtime profile game_args must be an array of strings")
    if profile.get("stage_companion") is not True:
        pytest.fail("release profile must explicitly opt in with stage_companion=true")
    return profile


def _run(
    command: Sequence[str],
    *,
    cwd: Path,
    timeout: float = 180.0,
    require_success: bool = True,
) -> tuple[subprocess.CompletedProcess[str], dict[str, Any]]:
    completed = subprocess.run(
        list(command),
        cwd=cwd,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=timeout,
        check=False,
    )
    payload = _json_object(completed.stdout.strip(), source=" ".join(command[:3]))
    if require_success and completed.returncode != 0:
        pytest.fail(
            f"command failed ({completed.returncode}): {command}\n"
            f"STDOUT:\n{completed.stdout}\nSTDERR:\n{completed.stderr}"
        )
    return completed, payload


def _runtime_exec(
    base: Sequence[str],
    cwd: Path,
    command: str,
    arguments: Mapping[str, Any],
    *,
    wait_for: str = "immediate",
    timeout_ms: int = 30_000,
    local_timeout: float = 45.0,
    request_id: str | None = None,
) -> dict[str, Any]:
    argv = [
        *base,
        "runtime",
        "exec",
        "--instance",
        "solo",
        "--command",
        command,
        "--args-json",
        json.dumps(dict(arguments), ensure_ascii=True, separators=(",", ":")),
        "--wait-for",
        wait_for,
        "--timeout-ms",
        str(timeout_ms),
        "--local-timeout",
        str(local_timeout),
    ]
    if request_id is not None:
        argv.extend(("--request-id", request_id))
    return _run(argv, cwd=cwd, timeout=local_timeout + 15.0)[1]


def _runtime_dispatch(
    base: Sequence[str],
    cwd: Path,
    command: str,
    arguments: Mapping[str, Any],
    *,
    wait_for: str,
    request_id: str,
    timeout_ms: int = 60_000,
    local_timeout: float = 90.0,
) -> dict[str, Any]:
    return _run(
        [
            *base,
            "runtime",
            "dispatch",
            "--instance",
            "solo",
            "--command",
            command,
            "--args-json",
            json.dumps(dict(arguments), ensure_ascii=True, separators=(",", ":")),
            "--wait-for",
            wait_for,
            "--timeout-ms",
            str(timeout_ms),
            "--local-timeout",
            str(local_timeout),
            "--request-id",
            request_id,
        ],
        cwd=cwd,
        timeout=local_timeout + 15.0,
    )[1]


def _wait_event(base: Sequence[str], cwd: Path, request_id: str, name: str) -> dict[str, Any]:
    return _run(
        [
            *base,
            "runtime",
            "wait-event",
            "--instance",
            "solo",
            "--request-id",
            request_id,
            "--name",
            name,
            "--timeout",
            "45",
        ],
        cwd=cwd,
        timeout=60.0,
    )[1]


def _wait_terminal(base: Sequence[str], cwd: Path, request_id: str) -> dict[str, Any]:
    return _run(
        [
            *base,
            "runtime",
            "wait-terminal",
            "--instance",
            "solo",
            "--request-id",
            request_id,
            "--timeout",
            "45",
        ],
        cwd=cwd,
        timeout=60.0,
    )[1]


def _snapshot(reference: Mapping[str, Any], session_root: Path) -> dict[str, Any]:
    path = Path(str(reference["path"])).resolve(strict=True)
    if not path.is_relative_to(session_root.resolve(strict=True)):
        pytest.fail(f"snapshot escaped the control session: {path}")
    snapshot = _json_object(path.read_text(encoding="utf-8"), source=str(path))
    assert snapshot["hashes"]["canonical_sha256"] == reference["canonical_sha256"]
    return snapshot


def _player(snapshot: Mapping[str, Any]) -> Mapping[str, Any]:
    players = snapshot["local_semantic"]["players"]
    assert len(players) == 1
    return players[0]


def _assert_settled(snapshot: Mapping[str, Any]) -> None:
    actions = snapshot["local_semantic"]["actions"]
    assert actions["queue_empty"] is True
    assert actions["executor_running"] is False
    assert actions["pending_choice"] is False
    assert actions["pending"] == []
    assert snapshot["local_semantic"]["choices"] == []


def _assert_known_stderr(stderr_path: Path, stdout_path: Path, extra_patterns: Sequence[str]) -> None:
    stderr = stderr_path.read_text(encoding="utf-8", errors="replace")
    stdout = stdout_path.read_text(encoding="utf-8", errors="replace")
    patterns = [
        r"^ERROR: Invalid Task ID$",
        r"^(?:ERROR|WARNING): .*\bRIDs?\b.*\bleaked\b.*$",
        r"^WARNING: ObjectDB instances leaked at exit.*$",
        r"^ERROR: \d+ resources still in use at exit.*$",
        *extra_patterns,
    ]
    severity = [line.strip() for line in stderr.splitlines() if re.match(r"^(?:ERROR|WARNING):", line.strip())]
    unexpected = [line for line in severity if not any(re.match(pattern, line) for pattern in patterns)]
    assert not unexpected, f"unexpected game stderr diagnostics: {unexpected}"
    severe_stdout = [
        line for line in stdout.splitlines()
        if re.match(r"^\[(?:ERROR|FATAL)\]", line.strip()) or "Unhandled exception" in line
    ]
    assert not severe_stdout, f"unexpected game stdout failures: {severe_stdout}"


def test_real_runtime_release_gate(
    runtime_release_profile: dict[str, Any],
    tmp_path: Path,
) -> None:
    profile = runtime_release_profile
    cli = shutil.which(CLI_NAME)
    assert cli, f"installed command not found on PATH: {CLI_NAME}"
    external_cwd = tmp_path / "installed-cli-cwd"
    external_cwd.mkdir()

    companion_manifest = _json_object(
        Path(profile["companion_manifest"]).read_text(encoding="utf-8"),
        source=profile["companion_manifest"],
    )
    assert companion_manifest.get("id") == "Sts2HeadlessTestBridge"
    assert companion_manifest.get("test_only") is True
    built = subprocess.run(
        [
            "dotnet",
            "build",
            profile["companion_project"],
            "--configuration",
            "Release",
            "--nologo",
            "--verbosity",
            "minimal",
        ],
        cwd=external_cwd,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        timeout=180,
        check=False,
    )
    assert built.returncode == 0, built.stdout

    build_dll = Path(profile["companion_build_dll"]).resolve(strict=True)
    staged_dll = Path(profile["companion_staged_dll"])
    staging_root = Path(profile["staging_root"]).resolve(strict=True)
    assert staged_dll.resolve().is_relative_to(staging_root)
    staged_dll.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(build_dll, staged_dll)
    assert _sha256(staged_dll) == _sha256(build_dll)

    runtime_root = Path(profile["runtime_root"])
    runtime_root.mkdir(parents=True, exist_ok=True)
    session_id = f"runtime-release-{time.strftime('%Y%m%d')}-{uuid.uuid4().hex[:8]}"
    session_root = runtime_root / session_id
    base = [
        cli,
        "--project-root",
        profile["project_root"],
        "--runtime-root",
        str(runtime_root),
        "--control-session",
        session_id,
    ]
    for root in profile["protected_roots"]:
        base.extend(("--protected-root", root))
    base.append("--json")

    started_at = _utc_now()
    started: dict[str, Any] | None = None
    stopped = False
    finalized: dict[str, Any] | None = None
    assertions: list[dict[str, Any]] = []
    scenario: dict[str, Any] = {"schema": "sts2-runtime-release-scenario/v1", "session_id": session_id}
    cleanup_errors: list[str] = []
    try:
        started = _run(
            [
                *base,
                "process",
                "start",
                "--instance",
                "solo",
                "--role",
                "single",
                "--adapter",
                str(profile.get("adapter_id", "sts2-0.111")),
                "--cwd",
                profile["staging_root"],
                "--settings-template",
                profile["settings_template"],
                "--client-id",
                "1",
                "--timeout",
                "120",
                "--",
                profile["game_executable"],
                *profile["game_args"],
            ],
            cwd=external_cwd,
            timeout=150.0,
        )[1]
        handshake = started["handshake"]
        process = started["process"]
        assert handshake["game"]["version"] == profile["expected_game_version"]
        assert handshake["game"]["commit"] == profile["expected_game_commit"]
        assert handshake["adapter"]["id"] == profile.get("adapter_id", "sts2-0.111")
        assert handshake["adapter"]["assembly_sha256"] == _sha256(staged_dll)
        assert handshake["runtime"]["display_driver"] == "headless"
        assert handshake["runtime"]["audio_driver"] == "Dummy"
        assert Path(handshake["runtime"]["output_root"]).resolve().is_relative_to(session_root.resolve())
        assertions.append({"id": "poc0-authenticated-real-runtime", "passed": True, "oracle": "hello_ack fingerprints"})

        ping = _runtime_exec(base, external_cwd, "runtime.ping", {})
        assert ping["type"] == "completed"
        new_run = _runtime_exec(
            base,
            external_cwd,
            "run.new",
            {"character": "SHIN_GETTER", "ascension": 0, "seed": "424242"},
            wait_for="queue_settled",
            timeout_ms=60_000,
            local_timeout=90.0,
        )
        assert new_run["result"]["character_id"] == "CHARACTER.SHIN_GETTER"
        fight = _runtime_exec(
            base,
            external_cwd,
            "console.exec",
            {"input": "fight CULTISTS_NORMAL"},
            wait_for="queue_settled",
            timeout_ms=60_000,
            local_timeout=90.0,
        )
        assert fight["result"]["location"]["phase"] == "Play"

        defend = _runtime_exec(
            base,
            external_cwd,
            "combat.add_card",
            {"model_id": "DEFEND_IRONCLAD", "pile": "Hand"},
            wait_for="queue_settled",
        )
        defend_args = {"card": defend["result"]["card_handle"], "target": None}
        defend_parent = f"release-defend-{uuid.uuid4()}"
        _runtime_dispatch(
            base,
            external_cwd,
            "combat.play_card",
            defend_args,
            wait_for="queue_settled",
            request_id=defend_parent,
        )
        defend_enqueued = _wait_event(base, external_cwd, defend_parent, "action_enqueued")
        defend_finished = _wait_event(base, external_cwd, defend_parent, "action_finished")
        defend_terminal = _wait_terminal(base, external_cwd, defend_parent)
        assert defend_enqueued["data"]["correlation"] == "exact_reference"
        assert defend_enqueued["seq"] < defend_finished["seq"] < defend_terminal["seq"]
        defend_pre = _snapshot(defend_terminal["result"]["pre_snapshot"], session_root)
        defend_post = _snapshot(defend_terminal["result"]["post_snapshot"], session_root)
        assert _player(defend_pre)["energy"] == 3
        assert _player(defend_post)["energy"] == 2
        assert _player(defend_pre)["block"] == 0
        assert _player(defend_post)["block"] == 5
        assert defend_pre["authoritative_run"]["rng_fingerprint"] == defend_post["authoritative_run"]["rng_fingerprint"]
        _assert_settled(defend_post)
        assertions.append({"id": "poc1-exact-defend", "passed": True, "oracle": "typed action snapshots"})

        armaments = _runtime_exec(
            base,
            external_cwd,
            "combat.add_card",
            {"model_id": "ARMAMENTS", "pile": "Hand"},
            wait_for="queue_settled",
        )
        armaments_args = {"card": armaments["result"]["card_handle"], "target": None}
        choice_parent = f"release-choice-{uuid.uuid4()}"
        _runtime_dispatch(
            base,
            external_cwd,
            "combat.play_card",
            armaments_args,
            wait_for="queue_settled",
            request_id=choice_parent,
        )
        choice_enqueued = _wait_event(base, external_cwd, choice_parent, "action_enqueued")
        choice_required = _wait_event(base, external_cwd, choice_parent, "choice_required")
        status = _run([*base, "process", "status", "--instance", "solo"], cwd=external_cwd)[1]
        resume_from = int(status["last_seq"])
        original_connection = handshake["connection_id"]
        reconnect = _run(
            [
                *base,
                "runtime",
                "connect",
                "--instance",
                "solo",
                "--resume-from-seq",
                str(resume_from),
                "--timeout",
                "30",
            ],
            cwd=external_cwd,
            timeout=45.0,
        )[1]
        assert reconnect["handshake"]["process_epoch"] == handshake["process_epoch"]
        assert reconnect["handshake"]["connection_id"] != original_connection
        assert reconnect["handshake"]["resume"]["status"] == "ok"

        duplicate = _runtime_dispatch(
            base,
            external_cwd,
            "combat.play_card",
            armaments_args,
            wait_for="queue_settled",
            request_id=choice_parent,
        )
        assert duplicate["request_id"] == choice_parent
        inflight = _run(
            [*base, "runtime", "request-status", "--instance", "solo", "--request-id", choice_parent],
            cwd=external_cwd,
        )[1]
        assert inflight["terminal"] is None

        conflict = _runtime_exec(
            base,
            external_cwd,
            "combat.play_card",
            {"card": armaments["result"]["card_handle"], "target": "creature:conflict"},
            wait_for="queue_settled",
            request_id=choice_parent,
            local_timeout=15.0,
        )
        assert conflict["type"] == "failed"
        assert conflict["error"]["code"] == "E_IDEMPOTENCY_CONFLICT"

        choice_data = choice_required["data"]
        selected = choice_data["candidates"][0]
        valid_choice = {
            "blocked_request_id": choice_parent,
            "owner_id": choice_data["owner_id"],
            "choice_handle": choice_data["choice_handle"],
            "choice_generation": choice_data["choice_generation"],
            "candidates": [selected["handle"]],
        }
        stale_choice = dict(valid_choice)
        stale_choice["choice_generation"] = int(choice_data["choice_generation"]) + 1
        stale = _runtime_exec(base, external_cwd, "choice.select", stale_choice)
        assert stale["type"] == "failed"
        assert stale["error"]["code"] == "E_STALE_HANDLE"
        busy = _runtime_exec(
            base,
            external_cwd,
            "combat.add_card",
            {"model_id": "DEFEND_IRONCLAD", "pile": "Hand"},
            wait_for="queue_settled",
        )
        assert busy["type"] == "failed"
        assert busy["error"]["code"] == "E_MUTATION_BUSY"
        choice = _runtime_exec(base, external_cwd, "choice.select", valid_choice)
        assert choice["type"] == "completed"
        assert choice["result"]["selector_accepted"] is True

        choice_finished = _wait_event(base, external_cwd, choice_parent, "action_finished")
        choice_terminal = _wait_terminal(base, external_cwd, choice_parent)
        assert choice_enqueued["seq"] < choice_required["seq"] < choice_finished["seq"] < choice_terminal["seq"]
        choice_pre = _snapshot(choice_terminal["result"]["pre_snapshot"], session_root)
        choice_post = _snapshot(choice_terminal["result"]["post_snapshot"], session_root)
        assert _player(choice_pre)["energy"] == 2
        assert _player(choice_post)["energy"] == 1
        upgraded = [
            card
            for pile in _player(choice_post)["piles"]
            for card in pile["cards"]
            if card["model_id"] == selected["model_id"] and card["upgrade_level"] == 1
        ]
        assert upgraded
        assert choice_pre["authoritative_run"]["rng_fingerprint"] == choice_post["authoritative_run"]["rng_fingerprint"]
        _assert_settled(choice_post)

        replay = _runtime_exec(
            base,
            external_cwd,
            "combat.play_card",
            armaments_args,
            wait_for="queue_settled",
            request_id=choice_parent,
            timeout_ms=60_000,
            local_timeout=15.0,
        )
        assert replay["type"] == "completed"
        assert replay["replayed"] is True
        assert replay["result"] == choice_terminal["result"]
        assertions.extend(
            [
                {"id": "poc1b-inflight-reconnect", "passed": True, "oracle": "same epoch/new connection/future terminal"},
                {"id": "poc1b-production-idempotency", "passed": True, "oracle": "duplicate/conflict/replayed terminal"},
                {"id": "poc1b-choice-continuation", "passed": True, "oracle": "stale/busy/accepted and post snapshot"},
            ]
        )

        stopped_result = _run(
            [*base, "process", "stop", "--instance", "solo", "--grace", "20"],
            cwd=external_cwd,
            timeout=45.0,
        )[1]
        stopped = True
        assert stopped_result["method"] == "graceful"
        assert stopped_result["exit_code"] == 0
        instance_root = session_root / "instances" / "solo"
        _assert_known_stderr(
            instance_root / "stderr.log",
            instance_root / "stdout.log",
            [str(item) for item in profile.get("stderr_allow_patterns", [])],
        )
        assertions.append({"id": "graceful-shutdown", "passed": True, "oracle": "exact process exit_code=0"})

        scenario.update(
            {
                "poc0": {"ping_request_id": ping["request_id"]},
                "poc1": {
                    "request_id": defend_parent,
                    "pre_snapshot": defend_terminal["result"]["pre_snapshot"],
                    "post_snapshot": defend_terminal["result"]["post_snapshot"],
                },
                "poc1b": {
                    "request_id": choice_parent,
                    "resume_from_seq": resume_from,
                    "choice_required_seq": choice_required["seq"],
                    "terminal_seq": choice_terminal["seq"],
                    "selected_model_id": selected["model_id"],
                    "pre_snapshot": choice_terminal["result"]["pre_snapshot"],
                    "post_snapshot": choice_terminal["result"]["post_snapshot"],
                },
            }
        )
        scenario_path = session_root / "scenario.json"
        scenario_path.write_text(
            json.dumps(scenario, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n",
            encoding="utf-8",
            newline="\n",
        )

        shin_manifest = _json_object(
            Path(profile["shin_getter_manifest"]).read_text(encoding="utf-8"),
            source=profile["shin_getter_manifest"],
        )
        metadata = evidence_metadata_template(
            case_id=session_id,
            result="passed",
            scenario_sha256=_sha256(scenario_path),
            harness_commit=started["software"]["harness_commit"],
            harness_version=started["software"]["harness_version"],
            game_version=handshake["game"]["version"],
            game_commit=handshake["game"]["commit"],
            game_executable_sha256=process["executable_sha256"],
            game_assembly_sha256=handshake["game"]["assembly_sha256"],
            adapter_id=handshake["adapter"]["id"],
            adapter_sha256=handshake["adapter"]["assembly_sha256"],
            capabilities=handshake["capabilities"],
            mods=[
                {
                    "id": shin_manifest["id"],
                    "version": shin_manifest["version"],
                    "dll_sha256": _sha256(Path(profile["shin_getter_dll"])),
                }
            ],
            instances=[
                {
                    "id": "solo",
                    "role": "single",
                    "pid": process["pid"],
                    "process_start_time_utc": process["process_start_time_utc"],
                    "driver": "headless/Dummy",
                    "user_data_root": handshake["runtime"]["user_data_path"],
                }
            ],
            seed=424242,
            rng_fingerprints=[
                defend_post["authoritative_run"]["rng_fingerprint"],
                choice_post["authoritative_run"]["rng_fingerprint"],
            ],
            assertions=assertions,
            started_at=started_at,
            ended_at=_utc_now(),
        )
        metadata_path = tmp_path / "runtime-release-metadata.json"
        metadata_path.write_text(json.dumps(metadata), encoding="utf-8")
        finalized = _run(
            [*base, "evidence", "finalize", "--metadata", str(metadata_path)],
            cwd=external_cwd,
        )[1]
        verified = _run([*base, "evidence", "verify"], cwd=external_cwd)[1]
        assert verified["ok"] is True
        assert verified["aggregate_sha256"] == finalized["aggregate_sha256"]
        assert verified["artifact_count"] >= 12
        print(f"\n  Runtime evidence: {session_root} ({verified['artifact_count']} artifacts)")
    finally:
        if started is not None and not stopped:
            try:
                status = _run(
                    [*base, "process", "status", "--instance", "solo"],
                    cwd=external_cwd,
                    timeout=15.0,
                )[1]
                if status.get("alive"):
                    _run(
                        [*base, "process", "stop", "--instance", "solo", "--grace", "2", "--force"],
                        cwd=external_cwd,
                        timeout=30.0,
                    )
            except Exception as exc:  # pragma: no cover - safety-net path
                cleanup_errors.append(f"game cleanup failed: {exc}")
        if started is not None:
            try:
                _run([*base, "session", "close"], cwd=external_cwd, timeout=30.0)
            except Exception as exc:  # pragma: no cover - safety-net path
                cleanup_errors.append(f"broker cleanup failed: {exc}")
        if cleanup_errors and sys.exc_info()[0] is None:
            pytest.fail("; ".join(cleanup_errors))
