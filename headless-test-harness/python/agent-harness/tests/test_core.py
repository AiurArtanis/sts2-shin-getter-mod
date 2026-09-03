from __future__ import annotations

import json
from pathlib import Path

from click.testing import CliRunner

from cli_anything.slaythespare2_111_beta.cli import cli
from cli_anything.slaythespare2_111_beta.core import (
    SessionStore,
    discover_command_line_args,
    discover_console_commands,
    discover_godot,
    dotnet_build_command,
    godot_command,
    project_status,
)


def test_project_status_reads_real_project(project_root: Path) -> None:
    status = project_status(project_root)
    assert status["project_name"] == "Slay the Spire 2"
    assert status["main_scene"] == "res://scenes/game.tscn"
    # The decompiled 111-beta project records this literal framework moniker.
    assert status["target_framework"] == "net90"
    assert status["counts"]["csharp"] > 3000
    assert status["counts"]["console_commands"] >= 40


def test_discovers_real_command_line_arguments(project_root: Path) -> None:
    arguments = {item["argument"] for item in discover_command_line_args(project_root)}
    assert {"nomods", "autoslay", "fastmp", "force-steam", "+connect_lobby"} <= arguments


def test_parses_real_console_commands(project_root: Path) -> None:
    commands = {item["name"]: item for item in discover_console_commands(project_root)}
    # There are 40 *ConsoleCmd.cs files: one abstract base and 39 commands.
    assert len(commands) == 39
    assert commands["win"]["networked"] is True
    assert commands["upgrade"]["args"] == "<hand-index:int>"


def test_build_and_godot_commands_are_argument_vectors(project_root: Path) -> None:
    build = dotnet_build_command(project_root, "Debug", no_restore=True)
    assert build[1:3] == ["build", str(project_root / "sts2.csproj")]
    assert build[-1] == "--no-restore"
    godot_candidates = discover_godot(project_root)
    assert godot_candidates
    command = godot_command(godot_candidates[0], project_root, "import")
    assert command[-2:] == ["--headless", "--import"]


def test_session_dry_run_autosave_undo_redo(tmp_path: Path) -> None:
    store = SessionStore(tmp_path / "state")
    preview = store.configure({"godot_executable": "C:/Godot.exe"}, dry_run=True)
    assert preview["changed"] is True
    assert not store.state_file.exists()

    saved = store.configure({"godot_executable": "C:/Godot.exe"})
    assert saved["after"]["godot_executable"] == "C:/Godot.exe"
    assert store.state_file.is_file()
    assert store.undo()["after"] == {}
    assert store.redo()["after"]["godot_executable"] == "C:/Godot.exe"


def test_cli_json_output(project_root: Path, tmp_path: Path) -> None:
    runner = CliRunner()
    result = runner.invoke(
        cli,
        [
            "--project-root",
            str(project_root),
            "--state-dir",
            str(tmp_path / "state"),
            "--json",
            "source",
            "status",
        ],
    )
    assert result.exit_code == 0, result.output
    payload = json.loads(result.output)
    assert payload["project_name"] == "Slay the Spire 2"
