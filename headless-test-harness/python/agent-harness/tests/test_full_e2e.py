from __future__ import annotations

import json
import os
import shutil
import subprocess
from pathlib import Path

import pytest


CLI_NAME = "cli-anything-slaythespare2-111-beta"


def _resolve_cli() -> str:
    installed = shutil.which(CLI_NAME)
    if os.environ.get("CLI_ANYTHING_FORCE_INSTALLED") == "1":
        assert installed, f"Installed command not found on PATH: {CLI_NAME}"
        return installed
    resolved = installed or CLI_NAME
    print(f"[_resolve_cli] Using installed command: {resolved}")
    return resolved


def _run_cli(project_root: Path, *args: str, timeout: int = 180) -> dict:
    command = [_resolve_cli(), "--project-root", str(project_root), "--json", *args]
    completed = subprocess.run(
        command,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=timeout,
        check=False,
    )
    assert completed.returncode == 0, f"{command}\nSTDOUT:\n{completed.stdout}\nSTDERR:\n{completed.stderr}"
    return json.loads(completed.stdout)


def _run_control_cli(
    project_root: Path,
    runtime_root: Path,
    session_id: str,
    *args: str,
    timeout: int = 180,
) -> dict:
    command = [
        _resolve_cli(),
        "--project-root",
        str(project_root),
        "--runtime-root",
        str(runtime_root),
        "--control-session",
        session_id,
        "--json",
        *args,
    ]
    completed = subprocess.run(
        command,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=timeout,
        check=False,
    )
    assert completed.returncode == 0, f"{command}\nSTDOUT:\n{completed.stdout}\nSTDERR:\n{completed.stderr}"
    return json.loads(completed.stdout)


@pytest.mark.e2e
def test_installed_cli_source_and_console(project_root: Path) -> None:
    source = _run_cli(project_root, "source", "status")
    assert source["counts"]["csharp"] > 3000
    console = _run_cli(project_root, "console", "list")
    assert console["count"] == 39


@pytest.mark.e2e
def test_real_codegraph_status_and_explore(project_root: Path) -> None:
    status = _run_cli(project_root, "graph", "status", timeout=240)
    assert status["returncode"] == 0
    # A newly edited harness may legitimately appear as pending index input.
    assert "CodeGraph Status" in status["stdout"]
    assert "Files:" in status["stdout"]
    explore = _run_cli(
        project_root,
        "graph",
        "explore",
        "CommandLineHelper",
        "HasArg",
        "--max-chars",
        "6000",
        timeout=300,
    )
    assert explore["returncode"] == 0
    assert "CommandLineHelper" in explore["stdout"]


@pytest.mark.e2e
def test_real_dotnet_build(project_root: Path) -> None:
    result = _run_cli(project_root, "build", "run", "--no-restore", timeout=1200)
    assert result["returncode"] == 0
    assert "sts2.dll" in result["stdout"]


@pytest.mark.e2e
def test_godot_discovery(project_root: Path) -> None:
    result = _run_cli(project_root, "game", "doctor")
    assert result["ready"] is True
    assert result["godot_candidates"]


@pytest.mark.e2e
def test_installed_cli_broker_component_poc0_from_arbitrary_cwd(
    project_root: Path,
    harness_root: Path,
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    component_project = (
        harness_root
        / "bridge"
        / "Sts2HeadlessTestBridge"
        / "tests"
        / "ComponentHost"
        / "ComponentHost.csproj"
    )
    built = subprocess.run(
        ["dotnet", "build", str(component_project), "--configuration", "Release", "--nologo", "--verbosity", "minimal"],
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        timeout=180,
        check=False,
    )
    assert built.returncode == 0, built.stdout
    component_dll = component_project.parent / "bin" / "Release" / "net9.0" / "ComponentHost.dll"
    runtime_root = tmp_path / "external-runtime"
    arbitrary_cwd = tmp_path / "unrelated-cwd"
    arbitrary_cwd.mkdir()
    monkeypatch.chdir(arbitrary_cwd)
    session_id = "installed-poc0"

    started = _run_control_cli(
        project_root,
        runtime_root,
        session_id,
        "process",
        "start",
        "--instance",
        "solo",
        "--role",
        "single",
        "--adapter",
        "component-test-host",
        "--cwd",
        str(tmp_path),
        "--",
        shutil.which("dotnet") or "dotnet",
        str(component_dll),
    )
    try:
        assert started["handshake"]["adapter"]["id"] == "component-test-host"
        assert Path(started["software"]["package_location"]).is_absolute()
        ping = _run_control_cli(
            project_root,
            runtime_root,
            session_id,
            "runtime",
            "exec",
            "--instance",
            "solo",
            "--command",
            "runtime.ping",
        )
        assert ping["result"]["backend"] == "component_test_host"
        status = _run_control_cli(project_root, runtime_root, session_id, "process", "status", "--instance", "solo")
        assert status["alive"] is True
        _run_control_cli(
            project_root,
            runtime_root,
            session_id,
            "process",
            "stop",
            "--instance",
            "solo",
            "--grace",
            "2",
            "--force",
        )

        handshake = started["handshake"]
        process = started["process"]
        metadata = {
            "schema": "sts2-evidence/v1",
            "case": {
                "id": "installed-poc0",
                "scenario_sha256": "0" * 64,
                "started_at": "2026-09-03T00:00:00Z",
                "ended_at": "2026-09-03T00:00:01Z",
                "result": "passed",
            },
            "software": {
                "harness_version": started["software"]["harness_version"],
                "harness_commit": started["software"]["harness_commit"],
                "python": "test",
                "os": "Windows",
            },
            "game": {
                "version": handshake["game"]["version"],
                "executable_sha256": process["executable_sha256"],
                "assembly_sha256": handshake["game"]["assembly_sha256"],
            },
            "mods": [],
            "adapter": {
                "id": handshake["adapter"]["id"],
                "assembly_sha256": handshake["adapter"]["assembly_sha256"],
                "capabilities": handshake["capabilities"],
            },
            "instances": [
                {
                    "id": "solo",
                    "role": "single",
                    "pid": process["pid"],
                    "process_start_time_utc": process["process_start_time_utc"],
                    "driver": "component",
                    "user_data_root": handshake["runtime"]["user_data_path"],
                }
            ],
            "determinism": {"seed": 424242, "rng_fingerprints": [], "fault_injection": False},
            "artifacts": [],
            "assertions": [{"id": "handshake", "passed": True, "oracle": "authenticated hello_ack"}],
            "redaction": {"rules_version": 1, "removed_fields": 0},
            "aggregate_sha256": "0" * 64,
        }
        metadata_path = tmp_path / "metadata.json"
        metadata_path.write_text(json.dumps(metadata), encoding="utf-8")
        finalized = _run_control_cli(
            project_root,
            runtime_root,
            session_id,
            "evidence",
            "finalize",
            "--metadata",
            str(metadata_path),
        )
        verified = _run_control_cli(project_root, runtime_root, session_id, "evidence", "verify")
        assert verified["ok"] is True
        assert verified["aggregate_sha256"] == finalized["aggregate_sha256"]
    finally:
        _run_control_cli(project_root, runtime_root, session_id, "session", "close")
