from __future__ import annotations

import copy
import json
from pathlib import Path

import pytest

from cli_anything.slaythespare2_111_beta.core.diff import (
    DiffPolicy,
    DiffRule,
    diff_snapshots,
    json_pointer_get,
)
from cli_anything.slaythespare2_111_beta.core.errors import ErrorCode, ProtocolFailure
from cli_anything.slaythespare2_111_beta.core.schema import SchemaRegistry
from cli_anything.slaythespare2_111_beta.core.state import (
    EpochState,
    StableHandleRegistry,
    canonical_state_bytes,
    finalize_snapshot,
    normalize_state,
    observe_without_rng_side_effect,
    snapshot_sha256,
)


def _golden(golden_root: Path) -> dict:
    return json.loads((golden_root / "state" / "minimal-state.json").read_text(encoding="utf-8"))


def test_finalize_snapshot_validates_schema(schemas_root: Path, golden_root: Path) -> None:
    result = finalize_snapshot(_golden(golden_root), schemas=SchemaRegistry(schemas_root))
    assert result["schema"] == "sts2-state/v1"
    assert len(result["hashes"]["canonical_sha256"]) == 64


def test_finalize_snapshot_does_not_mutate_input(schemas_root: Path, golden_root: Path) -> None:
    original = _golden(golden_root)
    before = copy.deepcopy(original)
    finalize_snapshot(original, schemas=SchemaRegistry(schemas_root))
    assert original == before


def test_canonical_state_hash_ignores_object_key_order() -> None:
    assert snapshot_sha256({"b": 2, "a": 1}) == snapshot_sha256({"a": 1, "b": 2})


def test_canonical_state_hash_ignores_its_own_field(golden_root: Path) -> None:
    first = _golden(golden_root)
    second = copy.deepcopy(first)
    second["hashes"]["canonical_sha256"] = "f" * 64
    assert snapshot_sha256(first) == snapshot_sha256(second)


def test_canonical_state_hash_changes_for_hard_state(golden_root: Path) -> None:
    first = _golden(golden_root)
    second = copy.deepcopy(first)
    second["local_semantic"]["players"][0]["hp"] -= 1
    assert snapshot_sha256(first) != snapshot_sha256(second)


def test_canonical_json_rejects_nan() -> None:
    with pytest.raises(ProtocolFailure) as failure:
        canonical_state_bytes({"value": float("nan")})
    assert failure.value.code == ErrorCode.INVALID_ARGUMENT


def test_normalization_redacts_home_path() -> None:
    result = normalize_state({"path": str(Path.home() / "private" / "save")})
    assert str(Path.home()) not in json.dumps(result.value)
    assert "<redacted>" in result.value["path"]


@pytest.mark.parametrize("key", ["token", "client_proof", "server_proof", "credential", "password"])
def test_normalization_removes_secret_fields(key: str) -> None:
    result = normalize_state({"safe": 1, key: "secret"})
    assert result.value == {"safe": 1}
    assert result.removed_fields == 1


@pytest.mark.parametrize("key", ["steam_id", "account_id", "sentry_installation_id"])
def test_normalization_removes_platform_identity(key: str) -> None:
    assert normalize_state({key: "private", "hp": 5}).value == {"hp": 5}


def test_normalization_preserves_semantic_array_order() -> None:
    value = {"cards": [{"id": "b"}, {"id": "a"}]}
    assert normalize_state(value).value["cards"] == value["cards"]


def test_normalization_sorts_only_declared_set_array() -> None:
    value = {"mods": [{"id": "b"}, {"id": "a"}]}
    result = normalize_state(value, set_like_arrays={"/mods": "id"})
    assert [item["id"] for item in result.value["mods"]] == ["a", "b"]


def test_normalization_keeps_integer_type() -> None:
    value = normalize_state({"amount": 2}).value["amount"]
    assert value == 2 and isinstance(value, int)


def test_normalization_keeps_float_type() -> None:
    value = normalize_state({"chance": 0.25}).value["chance"]
    assert value == 0.25 and isinstance(value, float)


def test_observer_accepts_unchanged_rng() -> None:
    fingerprints = iter(["rng-a", "rng-a"])
    result = observe_without_rng_side_effect(lambda: {"hp": 80}, lambda: next(fingerprints))
    assert result.value == {"hp": 80}
    assert result.rng_before == result.rng_after == "rng-a"


def test_observer_rejects_rng_side_effect() -> None:
    fingerprints = iter(["rng-a", "rng-b"])
    with pytest.raises(ProtocolFailure) as failure:
        observe_without_rng_side_effect(lambda: {"hp": 80}, lambda: next(fingerprints))
    assert failure.value.code == ErrorCode.OBSERVER_SIDE_EFFECT


def test_player_handle_survives_state_revision() -> None:
    registry = StableHandleRegistry(EpochState("p1", "r1", "room1", "c1", 1))
    handle = registry.issue("player", 1)
    registry.epochs = EpochState("p1", "r1", "room1", "c1", 99)
    assert registry.resolve(handle) == ("player", "1")


@pytest.mark.parametrize(
    ("kind", "changed"),
    [
        ("player", EpochState("p2", "r1", "room1", "c1", 1)),
        ("player", EpochState("p1", "r2", "room1", "c1", 1)),
        ("room", EpochState("p1", "r1", "room2", "c1", 1)),
        ("creature", EpochState("p1", "r1", "room1", "c2", 1)),
        ("combat-card", EpochState("p1", "r1", "room1", "c2", 1)),
    ],
)
def test_handle_expires_at_declared_epoch(kind: str, changed: EpochState) -> None:
    registry = StableHandleRegistry(EpochState("p1", "r1", "room1", "c1", 1))
    handle = registry.issue(kind, 7)
    registry.epochs = changed
    with pytest.raises(ProtocolFailure) as failure:
        registry.resolve(handle)
    assert failure.value.code == ErrorCode.STALE_HANDLE


def test_same_object_gets_stable_handle_within_epoch() -> None:
    registry = StableHandleRegistry(EpochState("p1", "r1", "room1", "c1", 1))
    assert registry.issue("creature", 7) == registry.issue("creature", 7)


def test_choice_generation_is_per_player() -> None:
    registry = StableHandleRegistry(EpochState("p1", "r1", "room1", "c1", 1))
    first = registry.begin_choice(1)
    second = registry.begin_choice(2)
    registry.resolve(first)
    registry.end_choice(2)
    registry.resolve(first)
    with pytest.raises(ProtocolFailure):
        registry.resolve(second)


def test_choice_end_invalidates_candidate() -> None:
    registry = StableHandleRegistry(EpochState("p1", "r1", "room1", "c1", 1))
    choice = registry.begin_choice(1)
    candidate = registry.issue_choice_item(choice, 0)
    registry.end_choice(1)
    with pytest.raises(ProtocolFailure) as failure:
        registry.resolve(candidate)
    assert failure.value.code == ErrorCode.STALE_HANDLE


def test_new_choice_invalidates_previous_choice_candidate() -> None:
    registry = StableHandleRegistry(EpochState("p1", "r1", "room1", "c1", 1))
    first_choice = registry.begin_choice(1)
    old_candidate = registry.issue_choice_item(first_choice, 0)
    second_choice = registry.begin_choice(1)
    registry.issue_choice_item(second_choice, 0)

    with pytest.raises(ProtocolFailure) as failure:
        registry.resolve(old_candidate)

    assert failure.value.code == ErrorCode.STALE_HANDLE


def test_unknown_handle_is_stale() -> None:
    registry = StableHandleRegistry(EpochState("p1", "r1", None, None, 1))
    with pytest.raises(ProtocolFailure) as failure:
        registry.resolve("creature:invented")
    assert failure.value.code == ErrorCode.STALE_HANDLE


def test_json_pointer_get_decodes_escape_sequences() -> None:
    assert json_pointer_get({"a/b": {"~key": 3}}, "/a~1b/~0key") == 3


def test_json_pointer_get_rejects_missing_path() -> None:
    with pytest.raises(ProtocolFailure) as failure:
        json_pointer_get({"a": 1}, "/missing")
    assert failure.value.code == ErrorCode.NOT_FOUND


def test_diff_equal_snapshots(golden_root: Path) -> None:
    value = _golden(golden_root)
    result = diff_snapshots(value, copy.deepcopy(value))
    assert result["equal"] is True
    assert result["difference_count"] == 0


def test_diff_reports_snapshot_hashes(golden_root: Path) -> None:
    left = _golden(golden_root)
    right = copy.deepcopy(left)
    right["local_semantic"]["players"][0]["hp"] = 1
    result = diff_snapshots(left, right)
    assert result["left"]["canonical_sha256"] == snapshot_sha256(left)
    assert result["right"]["canonical_sha256"] == snapshot_sha256(right)


def test_diff_orders_identity_before_hard_state(golden_root: Path) -> None:
    left = _golden(golden_root)
    right = copy.deepcopy(left)
    right["identity"]["adapter_id"] = "other"
    right["local_semantic"]["players"][0]["hp"] = 1
    result = diff_snapshots(left, right)
    assert result["first_difference"]["path"] == "/identity/adapter_id"


def test_diff_orders_location_before_action_and_hard(golden_root: Path) -> None:
    left = _golden(golden_root)
    right = copy.deepcopy(left)
    right["location"]["turn"] = 2
    right["hashes"]["game_checksum"] = "13"
    right["local_semantic"]["players"][0]["hp"] = 1
    assert diff_snapshots(left, right)["first_difference"]["path"] == "/location/turn"


def test_diff_orders_action_before_general_hard_state(golden_root: Path) -> None:
    left = _golden(golden_root)
    right = copy.deepcopy(left)
    right["hashes"]["game_checksum"] = "13"
    right["local_semantic"]["players"][0]["hp"] = 1
    assert diff_snapshots(left, right)["first_difference"]["path"] == "/hashes/game_checksum"


def test_diff_orders_eventual_before_presentation() -> None:
    left = {"local_semantic": {"eventual": 1}, "presentation": {"frame": 1}}
    right = {"local_semantic": {"eventual": 2}, "presentation": {"frame": 2}}
    policy = DiffPolicy([DiffRule("/local_semantic/eventual", comparison_class="eventual")])
    assert diff_snapshots(left, right, policy=policy)["first_difference"]["class"] == "eventual"


def test_diff_ordered_array_detects_reordering() -> None:
    result = diff_snapshots({"cards": ["a", "b"]}, {"cards": ["b", "a"]})
    assert result["equal"] is False


def test_diff_set_array_ignores_reordering() -> None:
    policy = DiffPolicy([DiffRule("/mods", array_semantics="set", sort_key="id")])
    left = {"mods": [{"id": "a"}, {"id": "b"}]}
    right = {"mods": [{"id": "b"}, {"id": "a"}]}
    assert diff_snapshots(left, right, policy=policy)["equal"] is True


def test_diff_absolute_tolerance_accepts_close_values() -> None:
    policy = DiffPolicy([DiffRule("/chance", absolute_tolerance=0.01)])
    assert diff_snapshots({"chance": 1.0}, {"chance": 1.005}, policy=policy)["equal"] is True


def test_diff_relative_tolerance_accepts_close_values() -> None:
    policy = DiffPolicy([DiffRule("/damage", relative_tolerance=0.02)])
    assert diff_snapshots({"damage": 100.0}, {"damage": 101.0}, policy=policy)["equal"] is True


def test_diff_outside_tolerance_reports_rule() -> None:
    policy = DiffPolicy([DiffRule("/chance", comparison_class="eventual", absolute_tolerance=0.01)])
    result = diff_snapshots({"chance": 1.0}, {"chance": 1.1}, policy=policy)
    assert result["first_difference"]["tolerance"]["absolute"] == 0.01
    assert result["first_difference"]["class"] == "eventual"


def test_diff_ignore_rule_is_reported() -> None:
    policy = DiffPolicy([DiffRule("/diagnostic", rule_id="ignore-diagnostic", ignore=True)])
    result = diff_snapshots({"diagnostic": 1}, {"diagnostic": 2}, policy=policy)
    assert result["equal"] is True
    assert result["ignored"] == {"count": 1, "rule_ids": ["ignore-diagnostic"]}


def test_diff_is_bounded_but_counts_all_differences() -> None:
    left = {f"k{i:03}": 0 for i in range(130)}
    right = {f"k{i:03}": 1 for i in range(130)}
    result = diff_snapshots(left, right, max_differences=100)
    assert result["difference_count"] == 130
    assert len(result["differences"]) == 100
    assert result["truncated"] is True


def test_diff_reports_missing_side() -> None:
    result = diff_snapshots({"left_only": 1}, {})
    assert result["first_difference"]["right"] == {"missing": True}


def test_diff_json_pointer_escapes_keys() -> None:
    result = diff_snapshots({"a/b": 1}, {"a/b": 2})
    assert result["first_difference"]["path"] == "/a~1b"


def test_diff_context_is_small_and_local() -> None:
    left = {"player": {"hp": 80, "block": 0, "energy": 3, "powers": [], "cards": list(range(20))}}
    right = copy.deepcopy(left)
    right["player"]["hp"] = 79
    context = diff_snapshots(left, right)["first_difference"]["context"]
    assert set(context) == {"left_parent", "right_parent"}
    assert len(json.dumps(context)) < 600
