from __future__ import annotations

import fnmatch
import math
from dataclasses import dataclass
from typing import Any, Mapping, Sequence

from .errors import ErrorCode, ProtocolFailure
from .protocol import canonical_json_bytes
from .state import snapshot_sha256


_MISSING = object()


def _escape(value: str) -> str:
    return value.replace("~", "~0").replace("/", "~1")


def json_pointer_get(value: Any, pointer: str) -> Any:
    if pointer == "":
        return value
    if not pointer.startswith("/"):
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "JSON Pointer must be empty or start with /")
    current = value
    try:
        for raw in pointer[1:].split("/"):
            token = raw.replace("~1", "/").replace("~0", "~")
            if isinstance(current, Mapping):
                current = current[token]
            elif isinstance(current, Sequence) and not isinstance(current, (str, bytes, bytearray)):
                current = current[int(token)]
            else:
                raise KeyError(token)
        return current
    except (KeyError, IndexError, ValueError, TypeError) as exc:
        raise ProtocolFailure(ErrorCode.NOT_FOUND, f"JSON Pointer does not exist: {pointer}") from exc


@dataclass(frozen=True)
class DiffRule:
    pattern: str
    comparison_class: str = "hard"
    array_semantics: str = "ordered"
    sort_key: str | None = None
    absolute_tolerance: float | None = None
    relative_tolerance: float | None = None
    ignore: bool = False
    rule_id: str | None = None

    def matches(self, pointer: str) -> bool:
        return fnmatch.fnmatchcase(pointer, self.pattern)


class DiffPolicy:
    def __init__(self, rules: Sequence[DiffRule] = ()) -> None:
        self.rules = tuple(rules)

    def rule_for(self, pointer: str) -> DiffRule:
        for rule in self.rules:
            if rule.matches(pointer):
                return rule
        if pointer in {"/snapshot_id", "/state_revision", "/clock/wall_time", "/clock/engine_frame", "/hashes/canonical_sha256"}:
            return DiffRule(pointer, comparison_class="diagnostic_only", ignore=True, rule_id="ignore-snapshot-metadata")
        if pointer.startswith("/presentation"):
            return DiffRule(pointer, comparison_class="presentation")
        return DiffRule(pointer)


def _priority(pointer: str, comparison_class: str) -> int:
    if pointer.startswith("/identity"):
        return 0
    if pointer.startswith("/location"):
        return 1
    if pointer.startswith("/local_semantic/actions") or pointer == "/hashes/game_checksum":
        return 2
    if comparison_class == "hard":
        return 3
    if comparison_class == "eventual":
        return 4
    if comparison_class == "presentation" or pointer.startswith("/presentation"):
        return 5
    return 6


def _compact(value: Any, *, depth: int = 0) -> Any:
    if value is _MISSING:
        return {"missing": True}
    if depth >= 2:
        if isinstance(value, Mapping):
            return {"summary": f"object({len(value)})"}
        if isinstance(value, list):
            return {"summary": f"array({len(value)})"}
        return value
    if isinstance(value, Mapping):
        keys = sorted(value)[:5]
        result = {str(key): _compact(value[key], depth=depth + 1) for key in keys}
        if len(value) > 5:
            result["…"] = f"{len(value) - 5} more"
        return result
    if isinstance(value, list):
        result = [_compact(child, depth=depth + 1) for child in value[:5]]
        if len(value) > 5:
            result.append({"…": f"{len(value) - 5} more"})
        return result
    return value


def _parent(value: Any, pointer: str) -> Any:
    if "/" not in pointer[1:]:
        return value
    parent = pointer.rsplit("/", 1)[0]
    try:
        return json_pointer_get(value, parent)
    except ProtocolFailure:
        return _MISSING


def _equivalent(left: Any, right: Any, rule: DiffRule) -> bool:
    numeric = (
        isinstance(left, (int, float))
        and not isinstance(left, bool)
        and isinstance(right, (int, float))
        and not isinstance(right, bool)
    )
    if numeric and (rule.absolute_tolerance is not None or rule.relative_tolerance is not None):
        return math.isclose(
            float(left),
            float(right),
            rel_tol=rule.relative_tolerance or 0.0,
            abs_tol=rule.absolute_tolerance or 0.0,
        )
    return left == right


def _sort_set(values: list[Any], key: str | None) -> list[Any]:
    def ordering(item: Any) -> bytes:
        if key is not None and isinstance(item, Mapping):
            return canonical_json_bytes(item.get(key))
        return canonical_json_bytes(item)

    return sorted(values, key=ordering)


def diff_snapshots(
    left: Any,
    right: Any,
    *,
    policy: DiffPolicy | None = None,
    max_differences: int = 100,
) -> dict[str, Any]:
    if max_differences < 1:
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "max_differences must be positive")
    selected_policy = policy or DiffPolicy()
    differences: list[dict[str, Any]] = []
    ignored_count = 0
    ignored_rules: set[str] = set()

    def record(pointer: str, left_value: Any, right_value: Any, rule: DiffRule) -> None:
        nonlocal ignored_count
        if rule.ignore:
            if left_value != right_value:
                ignored_count += 1
                ignored_rules.add(rule.rule_id or rule.pattern)
            return
        tolerance = None
        if rule.absolute_tolerance is not None or rule.relative_tolerance is not None:
            tolerance = {"absolute": rule.absolute_tolerance, "relative": rule.relative_tolerance}
        differences.append(
            {
                "path": pointer or "",
                "left": _compact(left_value),
                "right": _compact(right_value),
                "class": rule.comparison_class,
                "tolerance": tolerance,
                "context": {
                    "left_parent": _compact(_parent(left, pointer)),
                    "right_parent": _compact(_parent(right, pointer)),
                },
            }
        )

    def walk(left_value: Any, right_value: Any, pointer: str) -> None:
        nonlocal ignored_count
        rule = selected_policy.rule_for(pointer)
        if rule.ignore:
            if left_value != right_value:
                ignored_count += 1
                ignored_rules.add(rule.rule_id or rule.pattern)
            return
        if left_value is _MISSING or right_value is _MISSING:
            record(pointer, left_value, right_value, rule)
            return
        if isinstance(left_value, Mapping) and isinstance(right_value, Mapping):
            for key in sorted(set(left_value) | set(right_value), key=str):
                child = f"{pointer}/{_escape(str(key))}"
                walk(left_value.get(key, _MISSING), right_value.get(key, _MISSING), child)
            return
        if isinstance(left_value, list) and isinstance(right_value, list):
            left_items, right_items = left_value, right_value
            if rule.array_semantics == "set":
                left_items = _sort_set(left_value, rule.sort_key)
                right_items = _sort_set(right_value, rule.sort_key)
            for index in range(max(len(left_items), len(right_items))):
                child = f"{pointer}/{index}"
                walk(
                    left_items[index] if index < len(left_items) else _MISSING,
                    right_items[index] if index < len(right_items) else _MISSING,
                    child,
                )
            return
        if not _equivalent(left_value, right_value, rule):
            record(pointer, left_value, right_value, rule)

    walk(left, right, "")
    differences.sort(key=lambda item: (_priority(item["path"], item["class"]), item["path"]))
    limited = differences[:max_differences]
    left_id = left.get("snapshot_id") if isinstance(left, Mapping) else None
    right_id = right.get("snapshot_id") if isinstance(right, Mapping) else None
    return {
        "equal": not differences,
        "left": {"snapshot_id": left_id, "canonical_sha256": snapshot_sha256(left)},
        "right": {"snapshot_id": right_id, "canonical_sha256": snapshot_sha256(right)},
        "difference_count": len(differences),
        "differences": limited,
        "first_difference": limited[0] if limited else None,
        "truncated": len(differences) > len(limited),
        "ignored": {"count": ignored_count, "rule_ids": sorted(ignored_rules)},
    }
