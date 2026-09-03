from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any

from jsonschema import Draft202012Validator, FormatChecker

from .errors import ErrorCode, ProtocolFailure


SCHEMA_FILES = {
    "protocol-v1": "protocol-v1.schema.json",
    "state-v1": "state-v1.schema.json",
    "evidence-v1": "evidence-v1.schema.json",
    "scenario-v1": "scenario-v1.schema.json",
}


def default_harness_root() -> Path:
    configured = os.environ.get("STS2_HEADLESS_HARNESS_ROOT")
    if configured:
        return Path(configured).expanduser().resolve()
    return Path(__file__).resolve().parents[5]


class SchemaRegistry:
    def __init__(self, schemas_root: Path | None = None) -> None:
        self.schemas_root = (schemas_root or default_harness_root() / "schemas").resolve()
        self._validators: dict[str, Draft202012Validator] = {}

    def schema(self, name: str) -> dict[str, Any]:
        filename = SCHEMA_FILES.get(name)
        if filename is None:
            raise ProtocolFailure(
                ErrorCode.INVALID_ARGUMENT,
                f"unknown schema: {name}",
                details={"schema": name},
            )
        path = self.schemas_root / filename
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as exc:
            raise ProtocolFailure(
                ErrorCode.INVALID_ARGUMENT,
                f"cannot load {name} schema: {exc}",
                details={"schema": name, "path": str(path)},
            ) from exc

    def validator(self, name: str) -> Draft202012Validator:
        if name not in self._validators:
            schema = self.schema(name)
            Draft202012Validator.check_schema(schema)
            self._validators[name] = Draft202012Validator(schema, format_checker=FormatChecker())
        return self._validators[name]

    def validate(self, name: str, value: Any) -> None:
        errors = sorted(
            self.validator(name).iter_errors(value),
            key=lambda error: tuple(str(part) for part in error.absolute_path),
        )
        if not errors:
            return
        error = errors[0]
        pointer = "".join(f"/{part}" for part in error.absolute_path) or "/"
        raise ProtocolFailure(
            ErrorCode.INVALID_ARGUMENT,
            f"{name} validation failed at {pointer}: {error.message}",
            details={"schema": name, "path": pointer, "validator": error.validator},
        )
