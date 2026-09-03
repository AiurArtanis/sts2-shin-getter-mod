from __future__ import annotations

import json
import zipfile
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
from cli_anything.slaythespare2_111_beta.core.release_scan import (
    DEFAULT_FORBIDDEN_SIGNATURES,
    scan_production_targets,
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


def test_reverse_scan_accepts_clean_binary(tmp_path: Path) -> None:
    target = tmp_path / "ShinGetterMod.dll"
    target.write_bytes(b"production payload")
    result = scan_production_targets([target])
    assert result["ok"] is True
    assert result["hits"] == []


def test_reverse_scan_finds_bridge_name(tmp_path: Path) -> None:
    target = tmp_path / "ShinGetterMod.dll"
    target.write_bytes(b"prefix Sts2HeadlessTestBridge suffix")
    result = scan_production_targets([target])
    assert result["ok"] is False
    assert result["hits"][0]["signature"] == "Sts2HeadlessTestBridge"


def test_reverse_scan_finds_signature_across_chunk_boundary(tmp_path: Path) -> None:
    target = tmp_path / "ShinGetterMod.pck"
    signature = DEFAULT_FORBIDDEN_SIGNATURES[0].encode("utf-8")
    target.write_bytes(b"x" * 31 + signature + b"y")
    result = scan_production_targets([target], chunk_size=32)
    assert result["ok"] is False


def test_reverse_scan_checks_zip_entry_names(tmp_path: Path) -> None:
    target = tmp_path / "release.zip"
    with zipfile.ZipFile(target, "w") as archive:
        archive.writestr("Sts2HeadlessTestBridge.dll", b"clean")
    result = scan_production_targets([target])
    assert any(hit["location"] == "entry_name" for hit in result["hits"])


def test_reverse_scan_checks_zip_entry_content(tmp_path: Path) -> None:
    target = tmp_path / "release.zip"
    with zipfile.ZipFile(target, "w") as archive:
        archive.writestr("ShinGetterMod.json", b'{"debug":"STS2_TEST_ENABLE"}')
    result = scan_production_targets([target])
    assert any(hit["location"] == "entry_content" for hit in result["hits"])


def test_reverse_scan_excludes_headless_harness_directory(tmp_path: Path) -> None:
    production = tmp_path / "production"
    (production / "headless-test-harness").mkdir(parents=True)
    (production / "headless-test-harness" / "bridge.txt").write_text(
        "Sts2HeadlessTestBridge", encoding="utf-8"
    )
    (production / "ShinGetterMod.json").write_text("{}", encoding="utf-8")
    result = scan_production_targets([production])
    assert result["ok"] is True


def test_reverse_scan_records_target_hash_and_size(tmp_path: Path) -> None:
    target = tmp_path / "clean.dll"
    target.write_bytes(b"abc")
    result = scan_production_targets([target])
    assert result["targets"][0]["bytes"] == 3
    assert result["targets"][0]["sha256"] == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"


def test_reverse_scan_rejects_missing_target(tmp_path: Path) -> None:
    result = scan_production_targets([tmp_path / "missing.zip"])
    assert result["ok"] is False
    assert result["errors"][0]["code"] == "E_NOT_FOUND"
