from __future__ import annotations

import click


def register_live_runtime_commands(cli: click.Group) -> None:
    from .evidence import register_evidence_commands
    from .process import register_process_commands
    from .runtime import register_runtime_commands

    register_process_commands(cli)
    register_runtime_commands(cli)
    register_evidence_commands(cli)
