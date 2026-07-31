#!/usr/bin/env python3
"""Static compatibility gate for the Slay the Spire 2 beta 110 target."""

from __future__ import annotations

import argparse
import hashlib
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src"
EXPECTED_STS2_SIZE = 10_617_344
EXPECTED_STS2_SHA256 = "2F17B0DE15DE65A73F91F27BC81E98210ED1EFE94BB49DD5EE908F211FDE7ABD"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-109", type=Path, required=True)
    parser.add_argument("--source-110", type=Path, required=True)
    parser.add_argument("--sts2-dll", type=Path, required=True)
    return parser.parse_args()


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def assert_dll_identity(path: Path) -> None:
    assert path.stat().st_size == EXPECTED_STS2_SIZE, path
    digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
    assert digest == EXPECTED_STS2_SHA256, (path, digest)


def find_type_source(source_root: Path, type_name: str) -> Path:
    matches = list(source_root.rglob(f"{type_name}.cs"))
    assert len(matches) == 1, (type_name, matches)
    return matches[0]


def collect_harmony_targets() -> set[tuple[str, str]]:
    targets: set[tuple[str, str]] = set()
    direct = re.compile(
        r"\[HarmonyPatch\(typeof\((\w+)\),\s*"
        r"(?:nameof\(\w+\.(\w+)\)|\"([^\"]+)\")"
    )
    type_only = re.compile(r"\[HarmonyPatch\(typeof\((\w+)\)\)\]")
    method_only = re.compile(r"\[HarmonyPatch\(\"([^\"]+)\"\)\]")
    access_method = re.compile(
        r"AccessTools\.Method\(typeof\((\w+)\),\s*"
        r"(?:nameof\(\w+\.(\w+)\)|\"([^\"]+)\")"
    )

    for path in SOURCE.rglob("*.cs"):
        source = read(path)
        for match in direct.finditer(source):
            targets.add((match.group(1), match.group(2) or match.group(3)))
        for match in access_method.finditer(source):
            targets.add((match.group(1), match.group(2) or match.group(3)))

        current_type: str | None = None
        for line in source.splitlines():
            if match := type_only.search(line):
                current_type = match.group(1)
            elif match := method_only.search(line):
                assert current_type is not None, (path, line)
                targets.add((current_type, match.group(1)))

    return targets


def collect_private_field_targets() -> set[tuple[str, str]]:
    fields: set[tuple[str, str]] = set()
    patterns = (
        re.compile(r"FieldRefAccess<(\w+)\s*,[^>]+>\(\"([^\"]+)\"\)"),
        re.compile(r"AccessTools\.Field\(typeof\((\w+)\),\s*\"([^\"]+)\"\)"),
    )
    for path in SOURCE.rglob("*.cs"):
        source = read(path)
        for pattern in patterns:
            fields.update((match.group(1), match.group(2)) for match in pattern.finditer(source))
    return fields


def assert_harmony_contracts(source_110: Path) -> tuple[int, int]:
    targets = collect_harmony_targets()
    fields = collect_private_field_targets()

    for type_name, method_name in sorted(targets):
        source = read(find_type_source(source_110, type_name))
        if method_name.startswith("get_"):
            property_name = re.escape(method_name.removeprefix("get_"))
            assert re.search(rf"\b{property_name}\b\s*(?:=>|\{{)", source), (
                type_name,
                method_name,
            )
        else:
            assert re.search(rf"\b{re.escape(method_name)}\s*\(", source), (
                type_name,
                method_name,
            )

    for type_name, field_name in sorted(fields):
        source = read(find_type_source(source_110, type_name))
        assert re.search(rf"\b{re.escape(field_name)}\b", source), (type_name, field_name)

    return len(targets), len(fields)


def assert_lobby_player_split(source_109: Path, source_110: Path) -> None:
    patch = read(SOURCE / "Patches" / "ShinGetterVisualsPatch.cs")
    assert "StartRunLobbyPlayer player" in patch
    assert not re.search(r"(?<!StartRun)LobbyPlayer player", patch)

    screen_109 = read(
        source_109
        / "src/Core/Nodes/Screens/CharacterSelect/NCharacterSelectScreen.cs"
    )
    screen_110 = read(
        source_110
        / "src/Core/Nodes/Screens/CharacterSelect/NCharacterSelectScreen.cs"
    )
    assert "PlayerChanged(LobbyPlayer player, bool isRandomCharacterResolution)" in screen_109
    assert (
        "PlayerChanged(StartRunLobbyPlayer player, bool isRandomCharacterResolution)"
        in screen_110
    )
    assert not (source_110 / "src/Core/Entities/Multiplayer/LobbyPlayer.cs").exists()
    assert (
        source_110 / "src/Core/Entities/Multiplayer/StartRunLobbyPlayer.cs"
    ).is_file()


def assert_original_resources(source_109: Path, source_110: Path) -> int:
    paths: set[str] = set()
    pattern = re.compile(r"res://[A-Za-z0-9_./-]+")
    suffixes = {".cs", ".gd", ".gdshader", ".tres", ".tscn"}
    for path in ROOT.rglob("*"):
        if not path.is_file() or path.suffix not in suffixes or ".godot" in path.parts:
            continue
        paths.update(pattern.findall(read(path)))

    checked = 0
    for resource_path in sorted(paths):
        relative = Path(resource_path.removeprefix("res://"))
        if (source_109 / relative).exists():
            assert (source_110 / relative).exists(), resource_path
            checked += 1
    assert checked > 0
    return checked


def assert_saved_property_contracts_unchanged(
    source_109: Path, source_110: Path
) -> int:
    relative_paths = (
        "src/Core/Saves/Runs/SavedPropertyAttribute.cs",
        "src/Core/Saves/Runs/SerializationCondition.cs",
        "src/Core/Saves/Runs/SavedProperties.cs",
        "src/Core/Multiplayer/Serialization/ModelIdSerializationCache.cs",
        "src/Core/Models/AbstractModel.cs",
        "src/Core/Models/RelicModel.cs",
        "src/Core/Saves/Runs/SerializableCard.cs",
        "src/Core/Saves/Runs/SerializableRelic.cs",
        "src/Core/Saves/Validation/DeserializationContext.cs",
    )
    for relative_path in relative_paths:
        assert (source_109 / relative_path).read_bytes() == (
            source_110 / relative_path
        ).read_bytes(), relative_path
    return len(relative_paths)


def main() -> None:
    args = parse_args()
    source_109 = args.source_109.resolve()
    source_110 = args.source_110.resolve()
    assert source_109.is_dir(), source_109
    assert source_110.is_dir(), source_110

    assert_dll_identity(args.sts2_dll.resolve())
    assert_lobby_player_split(source_109, source_110)
    harmony_methods, private_fields = assert_harmony_contracts(source_110)
    original_resources = assert_original_resources(source_109, source_110)
    serialization_files = assert_saved_property_contracts_unchanged(
        source_109, source_110
    )
    print(
        "support-110-beta static compatibility: PASS "
        f"({harmony_methods} Harmony methods, {private_fields} private fields, "
        f"{original_resources} original resources, "
        f"{serialization_files} serialization contracts)"
    )


if __name__ == "__main__":
    main()
