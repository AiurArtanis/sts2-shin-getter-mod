from __future__ import annotations

import shlex
from pathlib import Path
from typing import Any

import click

from . import __version__
from .utils.repl_skin import ReplSkin


def run_repl(cli: Any, root: Path, state_dir: Path) -> None:
    skin = ReplSkin("slaythespare2_111_beta", version=__version__)
    skin.print_banner()
    try:
        prompt_session = skin.create_prompt_session()
    except Exception:
        prompt_session = None

    while True:
        try:
            if prompt_session is not None:
                line = skin.get_input(prompt_session, project_name=root.name)
            else:
                line = input(f"{root.name}> ")
        except (EOFError, KeyboardInterrupt):
            break
        line = line.strip()
        if not line:
            continue
        if line.lower() in {"quit", "exit"}:
            break
        args = shlex.split(line, posix=False)
        if args == ["help"]:
            args = ["--help"]
        prefix = ["--project-root", str(root), "--state-dir", str(state_dir)]
        try:
            cli.main(args=[*prefix, *args], prog_name="cli-anything-slaythespare2-111-beta", standalone_mode=False)
        except click.ClickException as exc:
            skin.error(exc.format_message())
        except click.exceptions.Exit:
            continue
        except Exception as exc:
            skin.error(str(exc))
    skin.print_goodbye()
