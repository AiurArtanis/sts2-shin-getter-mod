from __future__ import annotations

import json
from pathlib import Path

import click

from .common import emit, request


def register_evidence_commands(cli: click.Group) -> None:
    @cli.group("evidence")
    def evidence_group() -> None:
        """Finalize or verify immutable control-session evidence."""

    @evidence_group.command("finalize")
    @click.option("--metadata", type=click.Path(path_type=Path, dir_okay=False, exists=True), required=True)
    @click.pass_context
    def finalize(context: click.Context, metadata: Path) -> None:
        try:
            value = json.loads(metadata.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise click.BadParameter(f"invalid evidence metadata: {exc}") from exc
        emit(context, request(context, "evidence.finalize", {"metadata": value}))

    @evidence_group.command("verify")
    @click.pass_context
    def verify(context: click.Context) -> None:
        emit(context, request(context, "evidence.verify", {}))
