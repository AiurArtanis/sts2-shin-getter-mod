from __future__ import annotations

import subprocess
import uuid
from pathlib import Path
from typing import Any, Mapping

import click

from .. import __version__
from ..core.broker_client import BrokerClient
from ..core.errors import ProtocolFailure
from ..core.runtime_session import ControlSession, validate_identifier
from ..core.schema import default_harness_root


def emit(context: click.Context, payload: Any) -> None:
    context.obj["emit"](context, payload)


def fail(context: click.Context, failure: ProtocolFailure) -> None:
    if context.obj["json"]:
        emit(context, {"ok": False, "error": failure.to_error()})
        raise click.exceptions.Exit(1)
    raise click.ClickException(f"{failure.code.value}: {failure}")


def request(context: click.Context, operation: str, arguments: Mapping[str, Any]) -> dict[str, Any]:
    try:
        return broker(context).request(operation, arguments)
    except ProtocolFailure as exc:
        fail(context, exc)
        raise AssertionError("unreachable")


def repository_root() -> Path:
    return default_harness_root().parent.resolve()


def control_session_id(context: click.Context, *, create: bool = False) -> str:
    value = context.obj.get("control_session")
    if value:
        return validate_identifier(str(value))
    if create:
        value = f"session-{uuid.uuid4().hex[:16]}"
        context.obj["control_session"] = value
        return value
    raise click.UsageError("--control-session is required for this command")


def open_session(context: click.Context) -> ControlSession:
    identifier = control_session_id(context)
    root = Path(context.obj["runtime_root"]) / identifier
    try:
        return ControlSession.open(root, repository_root=repository_root())
    except ProtocolFailure as exc:
        fail(context, exc)
        raise AssertionError("unreachable")


def create_or_open_session(context: click.Context) -> ControlSession:
    identifier = control_session_id(context, create=True)
    root = Path(context.obj["runtime_root"]) / identifier
    if root.exists() and any(root.iterdir()):
        return ControlSession.open(root, repository_root=repository_root())
    return ControlSession.create(
        Path(context.obj["runtime_root"]),
        identifier,
        repository_root=repository_root(),
        protected_roots=[Path(context.obj["root"])],
    )


def broker(context: click.Context) -> BrokerClient:
    return BrokerClient.from_session(open_session(context))


def bootstrap_broker(context: click.Context, session: ControlSession) -> BrokerClient:
    return BrokerClient.bootstrap(session, repository_root=repository_root())


def provenance() -> dict[str, Any]:
    repo = repository_root()
    commit = "unknown"
    completed = subprocess.run(
        ["git", "rev-parse", "HEAD"],
        cwd=repo,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        check=False,
        timeout=10,
    )
    if completed.returncode == 0:
        commit = completed.stdout.strip()
    return {
        "harness_version": __version__,
        "harness_commit": commit,
        "package_location": str(Path(__file__).resolve().parents[1]),
    }
