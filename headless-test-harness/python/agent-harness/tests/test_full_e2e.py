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
    return installed or CLI_NAME


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
