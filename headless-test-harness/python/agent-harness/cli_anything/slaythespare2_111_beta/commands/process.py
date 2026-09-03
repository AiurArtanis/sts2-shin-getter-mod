from __future__ import annotations

from pathlib import Path

import click

from ..core.errors import ProtocolFailure
from .common import bootstrap_broker, create_or_open_session, emit, fail, provenance, request


def register_process_commands(cli: click.Group) -> None:
    @cli.group("process")
    def process_group() -> None:
        """Start, inspect, and exactly stop broker-owned test processes."""

    @process_group.command("start", context_settings={"ignore_unknown_options": True})
    @click.option("--instance", "instance_id", required=True)
    @click.option("--role", type=click.Choice(["single", "host", "client"]), default="single", show_default=True)
    @click.option("--adapter", "adapter_expected", default="sts2-0.111", show_default=True)
    @click.option("--cwd", type=click.Path(path_type=Path, file_okay=False), required=True)
    @click.option("--settings-template", type=click.Path(path_type=Path, dir_okay=False, exists=True))
    @click.option("--client-id", type=click.IntRange(min=1), default=1, show_default=True)
    @click.option("--timeout", "timeout_seconds", type=click.FloatRange(min=0.1, max=120), default=30.0)
    @click.argument("argv", nargs=-1, required=True, type=click.UNPROCESSED)
    @click.pass_context
    def process_start(
        context: click.Context,
        instance_id: str,
        role: str,
        adapter_expected: str,
        cwd: Path,
        settings_template: Path | None,
        client_id: int,
        timeout_seconds: float,
        argv: tuple[str, ...],
    ) -> None:
        try:
            session = create_or_open_session(context)
            client = bootstrap_broker(context, session)
            result = client.request(
                "process.start",
                {
                    "instance_id": instance_id,
                    "role": role,
                    "adapter_expected": adapter_expected,
                    "cwd": str(cwd.resolve()),
                    "settings_template": str(settings_template.resolve()) if settings_template else None,
                    "client_id": client_id,
                    "timeout_seconds": timeout_seconds,
                    "argv": list(argv),
                },
                timeout_seconds=timeout_seconds + 10,
            )
        except ProtocolFailure as exc:
            fail(context, exc)
            raise AssertionError("unreachable")
        result.update(
            {
                "session_id": session.load_index()["session_id"],
                "session_root": str(session.paths.root),
                "software": provenance(),
            }
        )
        emit(context, result)

    @process_group.command("list")
    @click.pass_context
    def process_list(context: click.Context) -> None:
        emit(context, request(context, "process.list", {}))

    @process_group.command("status")
    @click.option("--instance", "instance_id", required=True)
    @click.pass_context
    def process_status(context: click.Context, instance_id: str) -> None:
        emit(context, request(context, "process.status", {"instance_id": instance_id}))

    @process_group.command("stop")
    @click.option("--instance", "instance_id", required=True)
    @click.option("--grace", "grace_seconds", type=click.FloatRange(min=0, max=60), default=5.0)
    @click.option("--force", is_flag=True)
    @click.pass_context
    def process_stop(context: click.Context, instance_id: str, grace_seconds: float, force: bool) -> None:
        emit(
            context,
            request(
                context,
                "process.stop",
                {"instance_id": instance_id, "grace_seconds": grace_seconds, "force": force},
            ),
        )

    session_group = cli.commands["session"]

    @session_group.command("close")
    @click.pass_context
    def session_close(context: click.Context) -> None:
        """Close the live control session and all of its owned processes."""

        emit(context, request(context, "session.close", {}))
