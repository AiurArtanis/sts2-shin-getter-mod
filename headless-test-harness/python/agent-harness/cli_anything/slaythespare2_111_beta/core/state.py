from __future__ import annotations

import hashlib
from copy import deepcopy
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Callable, Mapping

from .errors import ErrorCode, ProtocolFailure
from .protocol import canonical_json_bytes
from .schema import SchemaRegistry


_SECRET_KEY_PARTS = ("token", "proof", "secret", "credential", "password")
_PLATFORM_ID_KEYS = {"steam_id", "account_id", "platform_id", "sentry_installation_id", "installation_id"}


def _pointer_child(pointer: str, key: str | int) -> str:
    escaped = str(key).replace("~", "~0").replace("/", "~1")
    return f"{pointer}/{escaped}"


@dataclass(frozen=True)
class NormalizationResult:
    value: Any
    removed_fields: int


def normalize_state(
    value: Any,
    *,
    set_like_arrays: Mapping[str, str | None] | None = None,
    account_root: Path | None = None,
) -> NormalizationResult:
    """Normalize a snapshot without changing semantic list ordering by default."""

    set_paths = dict(set_like_arrays or {})
    home = str((account_root or Path.home()).expanduser())
    home_forward = home.replace("\\", "/")
    removed = 0

    def visit(item: Any, pointer: str) -> Any:
        nonlocal removed
        if isinstance(item, Mapping):
            result: dict[str, Any] = {}
            for raw_key in sorted(item, key=lambda child: str(child)):
                key = str(raw_key)
                lowered = key.lower()
                if any(part in lowered for part in _SECRET_KEY_PARTS) or lowered in _PLATFORM_ID_KEYS:
                    removed += 1
                    continue
                result[key] = visit(item[raw_key], _pointer_child(pointer, key))
            return result
        if isinstance(item, (list, tuple)):
            normalized = [visit(child, _pointer_child(pointer, index)) for index, child in enumerate(item)]
            if pointer in set_paths:
                sort_key = set_paths[pointer]

                def ordering(child: Any) -> bytes:
                    if sort_key is not None and isinstance(child, Mapping):
                        return canonical_json_bytes(child.get(sort_key))
                    return canonical_json_bytes(child)

                normalized.sort(key=ordering)
            return normalized
        if isinstance(item, str):
            return item.replace(home, "<redacted>").replace(home_forward, "<redacted>")
        return item

    return NormalizationResult(visit(deepcopy(value), ""), removed)


def canonical_state_bytes(value: Any) -> bytes:
    return canonical_json_bytes(value)


def snapshot_sha256(value: Any) -> str:
    normalized = normalize_state(value).value
    if isinstance(normalized, dict):
        hashes = normalized.get("hashes")
        if isinstance(hashes, dict) and "canonical_sha256" in hashes:
            hashes["canonical_sha256"] = "0" * 64
    return hashlib.sha256(canonical_state_bytes(normalized)).hexdigest()


def finalize_snapshot(value: Mapping[str, Any], *, schemas: SchemaRegistry | None = None) -> dict[str, Any]:
    normalized = normalize_state(value).value
    if not isinstance(normalized, dict):
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "snapshot must be an object")
    hashes = normalized.setdefault("hashes", {})
    if not isinstance(hashes, dict):
        raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, "snapshot hashes must be an object")
    before = hashes.get("rng_before")
    after = hashes.get("rng_after")
    if before != after:
        raise ProtocolFailure(
            ErrorCode.OBSERVER_SIDE_EFFECT,
            "state collection changed the gameplay RNG fingerprint",
            details={"rng_before": before, "rng_after": after},
        )
    hashes["canonical_sha256"] = snapshot_sha256(normalized)
    (schemas or SchemaRegistry()).validate("state-v1", normalized)
    return normalized


@dataclass(frozen=True)
class Observation:
    value: Any
    rng_before: str | None
    rng_after: str | None


def observe_without_rng_side_effect(
    builder: Callable[[], Any],
    fingerprint: Callable[[], str | None],
) -> Observation:
    before = fingerprint()
    value = builder()
    after = fingerprint()
    if before != after:
        raise ProtocolFailure(
            ErrorCode.OBSERVER_SIDE_EFFECT,
            "state observer changed the gameplay RNG fingerprint",
            details={"rng_before": before, "rng_after": after},
        )
    return Observation(value, before, after)


@dataclass(frozen=True)
class EpochState:
    process_epoch: str
    run_epoch: str | None
    room_epoch: str | None
    combat_epoch: str | None
    state_revision: int
    choice_generations: Mapping[int, int] = field(default_factory=dict)


@dataclass(frozen=True)
class _HandleBinding:
    kind: str
    object_id: str
    process_epoch: str
    run_epoch: str | None = None
    room_epoch: str | None = None
    combat_epoch: str | None = None
    player_id: int | None = None
    choice_generation: int | None = None
    parent_choice_handle: str | None = None


class StableHandleRegistry:
    """Issue server-side handles whose validity follows structural epochs."""

    _RUN_KINDS = {"player", "deck", "map", "room", "creature", "combat-card", "action"}
    _ROOM_KINDS = {"room"}
    _COMBAT_KINDS = {"creature", "combat-card", "action"}

    def __init__(self, epochs: EpochState) -> None:
        self.epochs = epochs
        self._bindings: dict[str, _HandleBinding] = {}
        self._issued: dict[tuple[Any, ...], str] = {}
        self._choice_generation: dict[int, int] = dict(epochs.choice_generations)
        self._active_choice: dict[int, str] = {}

    def issue(self, kind: str, object_id: str | int) -> str:
        if kind not in self._RUN_KINDS:
            raise ProtocolFailure(ErrorCode.INVALID_ARGUMENT, f"unsupported handle kind: {kind}")
        if kind in self._RUN_KINDS and self.epochs.run_epoch is None:
            raise ProtocolFailure(ErrorCode.INVALID_PHASE, f"{kind} handle requires an active run")
        if kind in self._ROOM_KINDS and self.epochs.room_epoch is None:
            raise ProtocolFailure(ErrorCode.INVALID_PHASE, f"{kind} handle requires an active room")
        if kind in self._COMBAT_KINDS and self.epochs.combat_epoch is None:
            raise ProtocolFailure(ErrorCode.INVALID_PHASE, f"{kind} handle requires an active combat")
        identifier = str(object_id)
        key = (
            kind,
            identifier,
            self.epochs.process_epoch,
            self.epochs.run_epoch,
            self.epochs.room_epoch if kind in self._ROOM_KINDS else None,
            self.epochs.combat_epoch if kind in self._COMBAT_KINDS else None,
        )
        existing = self._issued.get(key)
        if existing is not None:
            return existing
        pieces = [kind, self.epochs.process_epoch, str(self.epochs.run_epoch)]
        if kind in self._ROOM_KINDS:
            pieces.append(str(self.epochs.room_epoch))
        if kind in self._COMBAT_KINDS:
            pieces.append(str(self.epochs.combat_epoch))
        pieces.append(identifier)
        handle = ":".join(pieces)
        self._bindings[handle] = _HandleBinding(
            kind,
            identifier,
            self.epochs.process_epoch,
            self.epochs.run_epoch,
            self.epochs.room_epoch if kind in self._ROOM_KINDS else None,
            self.epochs.combat_epoch if kind in self._COMBAT_KINDS else None,
        )
        self._issued[key] = handle
        return handle

    def begin_choice(self, player_id: int) -> str:
        generation = self._choice_generation.get(player_id, 0) + 1
        self._choice_generation[player_id] = generation
        handle = (
            f"choice:{self.epochs.process_epoch}:{self.epochs.run_epoch}:"
            f"{self.epochs.combat_epoch}:player-{player_id}:g{generation}"
        )
        self._bindings[handle] = _HandleBinding(
            "choice",
            str(player_id),
            self.epochs.process_epoch,
            self.epochs.run_epoch,
            combat_epoch=self.epochs.combat_epoch,
            player_id=player_id,
            choice_generation=generation,
        )
        self._active_choice[player_id] = handle
        return handle

    def issue_choice_item(self, choice_handle: str, index: int) -> str:
        self.resolve(choice_handle)
        choice = self._bindings[choice_handle]
        handle = f"choice-item:{choice_handle}:{index}"
        self._bindings[handle] = _HandleBinding(
            "choice-item",
            str(index),
            choice.process_epoch,
            choice.run_epoch,
            combat_epoch=choice.combat_epoch,
            player_id=choice.player_id,
            choice_generation=choice.choice_generation,
            parent_choice_handle=choice_handle,
        )
        return handle

    def end_choice(self, player_id: int) -> None:
        self._choice_generation[player_id] = self._choice_generation.get(player_id, 0) + 1
        self._active_choice.pop(player_id, None)

    def resolve(self, handle: str) -> tuple[str, str]:
        binding = self._bindings.get(handle)
        if binding is None or not self._valid(binding):
            raise ProtocolFailure(ErrorCode.STALE_HANDLE, "server-issued handle is stale or unknown")
        return binding.kind, binding.object_id

    def _valid(self, binding: _HandleBinding) -> bool:
        if binding.process_epoch != self.epochs.process_epoch:
            return False
        if binding.kind in self._RUN_KINDS | {"choice", "choice-item"} and binding.run_epoch != self.epochs.run_epoch:
            return False
        if binding.kind in self._ROOM_KINDS and binding.room_epoch != self.epochs.room_epoch:
            return False
        if binding.kind in self._COMBAT_KINDS | {"choice", "choice-item"} and binding.combat_epoch != self.epochs.combat_epoch:
            return False
        if binding.player_id is not None:
            if self._choice_generation.get(binding.player_id, 0) != binding.choice_generation:
                return False
            active_choice = self._active_choice.get(binding.player_id)
            if binding.kind == "choice" and active_choice not in self._bindings:
                return False
            if binding.kind == "choice" and self._bindings.get(active_choice) != binding:
                return False
            if binding.kind == "choice-item" and active_choice != binding.parent_choice_handle:
                return False
        return True
