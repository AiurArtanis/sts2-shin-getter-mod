from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from typing import Any

import click

from . import __version__
from .core import (
    HarnessError,
    SessionStore,
    codegraph_command,
    discover_command_line_args,
    discover_console_commands,
    discover_godot,
    dotnet_build_command,
    dotnet_restore_command,
    find_project_root,
    godot_command,
    list_project_files,
    project_status,
    run_codegraph,
    run_process,
    search_source,
)
from .core.runtime_session import default_runtime_root, default_state_root, validate_identifier


CONTEXT_SETTINGS = {"help_option_names": ["-h", "--help"], "max_content_width": 110}


def _emit(context: click.Context, payload: Any) -> None:
    if context.obj["json"]:
        # ASCII-safe JSON survives Windows agents whose redirected stdout is GBK.
        click.echo(json.dumps(payload, ensure_ascii=True, sort_keys=True))
    elif isinstance(payload, str):
        click.echo(payload)
    else:
        click.echo(json.dumps(payload, ensure_ascii=False, indent=2))


def _finish_backend(context: click.Context, payload: dict[str, Any]) -> None:
    _emit(context, payload)
    if payload.get("returncode", 0) != 0:
        raise click.exceptions.Exit(int(payload["returncode"]))


def _configured_godot(context: click.Context, explicit: str | None) -> str:
    configured = explicit or context.obj["store"].load()["config"].get("godot_executable")
    candidates = discover_godot(context.obj["root"], configured)
    if not candidates:
        raise HarnessError("No Godot executable found; use session configure --godot PATH")
    return candidates[0]


@click.group(invoke_without_command=True, context_settings=CONTEXT_SETTINGS)
@click.option("--project-root", type=click.Path(path_type=Path, file_okay=False), help="111-beta project root.")
@click.option("--state-dir", type=click.Path(path_type=Path, file_okay=False), help="Harness session directory.")
@click.option(
    "--runtime-root",
    type=click.Path(path_type=Path, file_okay=False),
    envvar="STS2_HEADLESS_RUNTIME_ROOT",
    help="External root for live control sessions (never inside a repository or game tree).",
)
@click.option(
    "--protected-root",
    "protected_roots",
    type=click.Path(path_type=Path),
    multiple=True,
    envvar="STS2_HEADLESS_PROTECTED_ROOTS",
    help="Protected source/game/deployment root; repeat for every tree that must remain read-only.",
)
@click.option(
    "--control-session",
    envvar="STS2_HEADLESS_CONTROL_SESSION",
    help="Live control-session identifier used by process/runtime/evidence commands.",
)
@click.option("--json", "json_mode", is_flag=True, help="Emit one machine-readable JSON value.")
@click.version_option(__version__)
@click.pass_context
def cli(
    context: click.Context,
    project_root: Path | None,
    state_dir: Path | None,
    runtime_root: Path | None,
    protected_roots: tuple[Path, ...],
    control_session: str | None,
    json_mode: bool,
) -> None:
    """Inspect, build, import, and launch the real Slay the Spire 2 111-beta project."""
    try:
        root = find_project_root(project_root)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    state_path = (state_dir or default_state_root()).expanduser().resolve()
    if control_session is not None:
        try:
            control_session = validate_identifier(control_session)
        except Exception as exc:
            raise click.ClickException(str(exc)) from exc
    context.ensure_object(dict)
    context.obj.update(
        {
            "root": root,
            "json": json_mode,
            "store": SessionStore(state_path),
            "state_dir": state_path,
            "runtime_root": (runtime_root or default_runtime_root()).expanduser().resolve(),
            "protected_roots": tuple(path.expanduser().resolve() for path in protected_roots),
            "control_session": control_session,
            "emit": _emit,
        }
    )
    if context.invoked_subcommand is None:
        if sys.stdin.isatty():
            from .repl import run_repl

            run_repl(cli, root, state_path)
        else:
            click.echo(context.get_help())


@cli.group("source")
def source_group() -> None:
    """Inspect source files and launch arguments."""


@source_group.command("status")
@click.pass_context
def source_status(context: click.Context) -> None:
    _emit(context, project_status(context.obj["root"]))


@source_group.command("files")
@click.option("--glob", "pattern", default="*", show_default=True)
@click.option("--limit", type=click.IntRange(1, 5000), default=100, show_default=True)
@click.pass_context
def source_files(context: click.Context, pattern: str, limit: int) -> None:
    _emit(context, list_project_files(context.obj["root"], pattern, limit))


@source_group.command("search")
@click.argument("query")
@click.option("--glob", default="*.cs", show_default=True)
@click.option("--limit", type=click.IntRange(1, 5000), default=100, show_default=True)
@click.pass_context
def source_search(context: click.Context, query: str, glob: str, limit: int) -> None:
    try:
        payload = search_source(context.obj["root"], query, glob, limit)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _emit(context, payload)


@source_group.command("args")
@click.pass_context
def source_args(context: click.Context) -> None:
    arguments = discover_command_line_args(context.obj["root"])
    _emit(context, {"count": len(arguments), "arguments": arguments})


@cli.group("graph")
def graph_group() -> None:
    """Call the existing CodeGraph index."""


@graph_group.command("status")
@click.pass_context
def graph_status(context: click.Context) -> None:
    try:
        result = run_codegraph(context.obj["root"], "status")
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@graph_group.command("sync")
@click.option("--dry-run", is_flag=True)
@click.pass_context
def graph_sync(context: click.Context, dry_run: bool) -> None:
    try:
        command = codegraph_command("sync")
        if dry_run:
            _emit(context, {"dry_run": True, "command": command, "cwd": str(context.obj["root"])})
            return
        result = run_codegraph(context.obj["root"], "sync", timeout=600)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@graph_group.command("query")
@click.argument("search")
@click.option("--max-chars", type=click.IntRange(100, 200000), default=20000, show_default=True)
@click.pass_context
def graph_query(context: click.Context, search: str, max_chars: int) -> None:
    try:
        result = run_codegraph(context.obj["root"], "query", [search], max_chars=max_chars)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@graph_group.command("explore")
@click.argument("query", nargs=-1, required=True)
@click.option("--max-chars", type=click.IntRange(100, 200000), default=20000, show_default=True)
@click.pass_context
def graph_explore(context: click.Context, query: tuple[str, ...], max_chars: int) -> None:
    try:
        result = run_codegraph(context.obj["root"], "explore", list(query), max_chars=max_chars)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@cli.group("build")
def build_group() -> None:
    """Restore and build the real C# project."""


@build_group.command("status")
@click.option("--configuration", type=click.Choice(["Debug", "Release"]), default="Debug")
@click.pass_context
def build_status(context: click.Context, configuration: str) -> None:
    root = context.obj["root"]
    dll = root / ".godot" / "mono" / "temp" / "bin" / configuration / "sts2.dll"
    _emit(
        context,
        {
            "project": str(root / "sts2.csproj"),
            "configuration": configuration,
            "output": str(dll),
            "output_exists": dll.is_file(),
            "output_size": dll.stat().st_size if dll.is_file() else None,
        },
    )


@build_group.command("restore")
@click.option("--dry-run", is_flag=True)
@click.pass_context
def build_restore(context: click.Context, dry_run: bool) -> None:
    try:
        command = dotnet_restore_command(context.obj["root"])
        if dry_run:
            _emit(context, {"dry_run": True, "command": command, "cwd": str(context.obj["root"])})
            return
        result = run_process(command, cwd=context.obj["root"], timeout=900)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@build_group.command("run")
@click.option("--configuration", type=click.Choice(["Debug", "Release"]), default="Debug", show_default=True)
@click.option("--no-restore/--restore", default=True, show_default=True)
@click.option("--dry-run", is_flag=True)
@click.pass_context
def build_run(context: click.Context, configuration: str, no_restore: bool, dry_run: bool) -> None:
    try:
        command = dotnet_build_command(context.obj["root"], configuration, no_restore)
        if dry_run:
            _emit(context, {"dry_run": True, "command": command, "cwd": str(context.obj["root"])})
            return
        result = run_process(command, cwd=context.obj["root"], timeout=1200)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@cli.group("game")
def game_group() -> None:
    """Discover Godot and operate the real project."""


@game_group.command("doctor")
@click.option("--godot", type=click.Path(path_type=Path, dir_okay=False))
@click.pass_context
def game_doctor(context: click.Context, godot: Path | None) -> None:
    root = context.obj["root"]
    configured = str(godot) if godot else context.obj["store"].load()["config"].get("godot_executable")
    candidates = discover_godot(root, configured)
    _emit(
        context,
        {
            "project_root": str(root),
            "project_file": (root / "project.godot").is_file(),
            "csharp_project": (root / "sts2.csproj").is_file(),
            "godot_candidates": candidates,
            "ready": bool(candidates),
        },
    )


@game_group.command("import")
@click.option("--godot", type=click.Path(path_type=Path, dir_okay=False))
@click.option("--dry-run", is_flag=True)
@click.pass_context
def game_import(context: click.Context, godot: Path | None, dry_run: bool) -> None:
    try:
        executable = _configured_godot(context, str(godot) if godot else None)
        command = godot_command(executable, context.obj["root"], "import")
        if dry_run:
            _emit(context, {"dry_run": True, "command": command, "cwd": str(context.obj["root"])})
            return
        result = run_process(command, cwd=context.obj["root"], timeout=1800)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@game_group.command("smoke")
@click.option("--godot", type=click.Path(path_type=Path, dir_okay=False))
@click.option("--quit-after", type=click.IntRange(1, 10000), default=120, show_default=True)
@click.option("--dry-run", is_flag=True)
@click.pass_context
def game_smoke(context: click.Context, godot: Path | None, quit_after: int, dry_run: bool) -> None:
    try:
        executable = _configured_godot(context, str(godot) if godot else None)
        command = godot_command(executable, context.obj["root"], "smoke", quit_after=quit_after)
        if dry_run:
            _emit(context, {"dry_run": True, "command": command, "cwd": str(context.obj["root"])})
            return
        result = run_process(command, cwd=context.obj["root"], timeout=900)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@game_group.command("launch", context_settings={"ignore_unknown_options": True})
@click.option("--godot", type=click.Path(path_type=Path, dir_okay=False))
@click.option("--editor", is_flag=True)
@click.option("--headless", is_flag=True)
@click.option("--detach", is_flag=True)
@click.option("--dry-run", is_flag=True)
@click.argument("game_args", nargs=-1, type=click.UNPROCESSED)
@click.pass_context
def game_launch(
    context: click.Context,
    godot: Path | None,
    editor: bool,
    headless: bool,
    detach: bool,
    dry_run: bool,
    game_args: tuple[str, ...],
) -> None:
    try:
        executable = _configured_godot(context, str(godot) if godot else None)
        command = godot_command(
            executable,
            context.obj["root"],
            "launch",
            editor=editor,
            headless=headless,
            extra_args=game_args,
        )
        if dry_run:
            _emit(context, {"dry_run": True, "command": command, "cwd": str(context.obj["root"])})
            return
        if detach:
            creationflags = subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
            process = subprocess.Popen(command, cwd=str(context.obj["root"]), creationflags=creationflags)
            _emit(context, {"detached": True, "pid": process.pid, "command": command})
            return
        result = run_process(command, cwd=context.obj["root"], timeout=86400)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _finish_backend(context, result)


@cli.group("console")
def console_group() -> None:
    """Index in-game DevConsole definitions without faking runtime execution."""


@console_group.command("list")
@click.option("--limit", type=click.IntRange(1, 1000), default=100, show_default=True)
@click.pass_context
def console_list(context: click.Context, limit: int) -> None:
    commands = discover_console_commands(context.obj["root"])
    _emit(context, {"count": len(commands), "returned": min(limit, len(commands)), "commands": commands[:limit]})


@console_group.command("search")
@click.argument("query")
@click.pass_context
def console_search(context: click.Context, query: str) -> None:
    lowered = query.lower()
    matches = [
        command
        for command in discover_console_commands(context.obj["root"])
        if lowered in " ".join(str(value or "") for value in command.values()).lower()
    ]
    _emit(context, {"query": query, "count": len(matches), "commands": matches})


@console_group.command("show")
@click.argument("name")
@click.option("--max-lines", type=click.IntRange(1, 2000), default=240, show_default=True)
@click.pass_context
def console_show(context: click.Context, name: str, max_lines: int) -> None:
    commands = discover_console_commands(context.obj["root"])
    command = next((item for item in commands if str(item["name"]).lower() == name.lower()), None)
    if not command:
        raise click.ClickException(f"Unknown DevConsole command: {name}")
    path = context.obj["root"] / command["source"]
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    payload = dict(command)
    payload.update(
        {
            "source_text": "\n".join(f"{index:4}: {line}" for index, line in enumerate(lines[:max_lines], 1)),
            "source_truncated": len(lines) > max_lines,
        }
    )
    _emit(context, payload)


@cli.group("session")
def session_group() -> None:
    """Manage persistent harness configuration with undo/redo."""


@session_group.command("status")
@click.pass_context
def session_status(context: click.Context) -> None:
    state = context.obj["store"].load()
    _emit(
        context,
        {
            "state_dir": str(context.obj["state_dir"]),
            "config": state["config"],
            "undo_depth": len(state["history"]),
            "redo_depth": len(state["future"]),
            "updated_at": state["updated_at"],
        },
    )


@session_group.command("configure")
@click.option("--godot", type=click.Path(path_type=Path, dir_okay=False))
@click.option("--game-executable", type=click.Path(path_type=Path, dir_okay=False))
@click.option("--dry-run", is_flag=True)
@click.pass_context
def session_configure(
    context: click.Context, godot: Path | None, game_executable: Path | None, dry_run: bool
) -> None:
    if godot is None and game_executable is None:
        raise click.ClickException("Provide --godot and/or --game-executable")
    updates = {
        "godot_executable": str(godot.resolve()) if godot else None,
        "game_executable": str(game_executable.resolve()) if game_executable else None,
    }
    try:
        result = context.obj["store"].configure(updates, dry_run=dry_run)
    except HarnessError as exc:
        raise click.ClickException(str(exc)) from exc
    _emit(context, result)


@session_group.command("undo")
@click.option("--dry-run", is_flag=True)
@click.pass_context
def session_undo(context: click.Context, dry_run: bool) -> None:
    _emit(context, context.obj["store"].undo(dry_run=dry_run))


@session_group.command("redo")
@click.option("--dry-run", is_flag=True)
@click.pass_context
def session_redo(context: click.Context, dry_run: bool) -> None:
    _emit(context, context.obj["store"].redo(dry_run=dry_run))


# Keep the imported 0.1 command definitions above source-compatible while the
# v0.2 live-runtime groups remain isolated in focused registration modules.
from .commands import register_live_runtime_commands  # noqa: E402


register_live_runtime_commands(cli)


def main() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            reconfigure(encoding="utf-8", errors="backslashreplace")
    cli(prog_name="cli-anything-slaythespare2-111-beta")


if __name__ == "__main__":
    main()
