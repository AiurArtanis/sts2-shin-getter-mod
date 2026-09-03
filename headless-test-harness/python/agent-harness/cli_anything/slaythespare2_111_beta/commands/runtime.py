from __future__ import annotations

import json
from typing import Any

import click

from .common import emit, request


def _args(value: str) -> dict[str, Any]:
    try:
        parsed = json.loads(value)
    except json.JSONDecodeError as exc:
        raise click.BadParameter(f"invalid JSON object: {exc}") from exc
    if not isinstance(parsed, dict):
        raise click.BadParameter("command arguments must be a JSON object")
    return parsed


def register_runtime_commands(cli: click.Group) -> None:
    @cli.group("runtime")
    def runtime_group() -> None:
        """Use the broker-owned authenticated companion connection."""

    @runtime_group.command("handshake")
    @click.option("--instance", "instance_id", required=True)
    @click.pass_context
    def handshake(context: click.Context, instance_id: str) -> None:
        emit(context, request(context, "runtime.handshake", {"instance_id": instance_id}))

    @runtime_group.command("connect")
    @click.option("--instance", "instance_id", required=True)
    @click.option("--resume-from-seq", type=click.IntRange(min=0), default=0)
    @click.option("--timeout", "timeout_seconds", type=click.FloatRange(min=0.1, max=120), default=30.0)
    @click.pass_context
    def connect(context: click.Context, instance_id: str, resume_from_seq: int, timeout_seconds: float) -> None:
        emit(
            context,
            request(
                context,
                "runtime.connect",
                {
                    "instance_id": instance_id,
                    "resume_from_seq": resume_from_seq,
                    "timeout_seconds": timeout_seconds,
                    "local_timeout_seconds": timeout_seconds,
                },
            ),
        )

    def common_options(function):
        function = click.option("--request-id")(function)
        function = click.option(
            "--local-timeout",
            "local_timeout_seconds",
            type=click.FloatRange(min=0.1, max=120),
            default=None,
            help="Broker/pipe monotonic deadline; defaults to command timeout plus two seconds.",
        )(function)
        function = click.option("--timeout-ms", type=click.IntRange(min=1, max=60000), default=10000)(function)
        function = click.option("--wait-for", default="immediate", show_default=True)(function)
        function = click.option("--args-json", default="{}", callback=lambda _c, _p, value: _args(value))(function)
        function = click.option("--command", required=True)(function)
        function = click.option("--instance", "instance_id", required=True)(function)
        return function

    @runtime_group.command("exec")
    @common_options
    @click.pass_context
    def execute(
        context: click.Context,
        instance_id: str,
        command: str,
        args_json: dict[str, Any],
        wait_for: str,
        timeout_ms: int,
        request_id: str | None,
        local_timeout_seconds: float | None,
    ) -> None:
        emit(
            context,
            request(
                context,
                "runtime.exec",
                {
                    "instance_id": instance_id,
                    "command": command,
                    "args": args_json,
                    "wait_for": wait_for,
                    "timeout_ms": timeout_ms,
                    "request_id": request_id,
                    "local_timeout_seconds": local_timeout_seconds or timeout_ms / 1000.0 + 2.0,
                },
            ),
        )

    @runtime_group.command("dispatch")
    @common_options
    @click.pass_context
    def dispatch(
        context: click.Context,
        instance_id: str,
        command: str,
        args_json: dict[str, Any],
        wait_for: str,
        timeout_ms: int,
        request_id: str | None,
        local_timeout_seconds: float | None,
    ) -> None:
        emit(
            context,
            request(
                context,
                "runtime.dispatch",
                {
                    "instance_id": instance_id,
                    "command": command,
                    "args": args_json,
                    "wait_for": wait_for,
                    "timeout_ms": timeout_ms,
                    "request_id": request_id,
                    "local_timeout_seconds": local_timeout_seconds or timeout_ms / 1000.0 + 2.0,
                },
            ),
        )

    @runtime_group.command("wait-event")
    @click.option("--instance", "instance_id", required=True)
    @click.option("--request-id", required=True)
    @click.option("--name", required=True)
    @click.option("--timeout", "local_timeout_seconds", type=click.FloatRange(min=0.1, max=120), default=30.0)
    @click.pass_context
    def wait_event(
        context: click.Context,
        instance_id: str,
        request_id: str,
        name: str,
        local_timeout_seconds: float,
    ) -> None:
        emit(
            context,
            request(
                context,
                "runtime.wait_event",
                {
                    "instance_id": instance_id,
                    "request_id": request_id,
                    "name": name,
                    "local_timeout_seconds": local_timeout_seconds,
                },
            ),
        )

    @runtime_group.command("wait-terminal")
    @click.option("--instance", "instance_id", required=True)
    @click.option("--request-id", required=True)
    @click.option("--timeout", "local_timeout_seconds", type=click.FloatRange(min=0.1, max=120), default=30.0)
    @click.pass_context
    def wait_terminal(
        context: click.Context,
        instance_id: str,
        request_id: str,
        local_timeout_seconds: float,
    ) -> None:
        emit(
            context,
            request(
                context,
                "runtime.wait_terminal",
                {
                    "instance_id": instance_id,
                    "request_id": request_id,
                    "local_timeout_seconds": local_timeout_seconds,
                },
            ),
        )

    @runtime_group.command("request-status")
    @click.option("--instance", "instance_id", required=True)
    @click.option("--request-id", required=True)
    @click.pass_context
    def request_status(context: click.Context, instance_id: str, request_id: str) -> None:
        emit(
            context,
            request(
                context,
                "runtime.request_status",
                {"instance_id": instance_id, "request_id": request_id},
            ),
        )
