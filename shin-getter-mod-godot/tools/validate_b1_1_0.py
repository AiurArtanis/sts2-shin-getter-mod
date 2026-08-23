#!/usr/bin/env python3
"""Static regression gate for the B1.1.0 balance and sprite-sheet update."""

from __future__ import annotations

import json
from pathlib import Path

from build_character_sprite_sheets import FRAME_COUNTS, load_frame_manifest


ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parent


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8-sig")


def require(relative_path: str, *fragments: str) -> None:
    text = read(relative_path)
    for fragment in fragments:
        if fragment not in text:
            raise AssertionError(f"{relative_path}: missing {fragment!r}")


def reject(relative_path: str, *fragments: str) -> None:
    text = read(relative_path)
    for fragment in fragments:
        if fragment in text:
            raise AssertionError(f"{relative_path}: forbidden {fragment!r}")


def validate_balance() -> None:
    require(
        "src/Models/Cards/SGC_ChangeAttack.cs",
        "new DamageVar(7m",
        "if (i < x - 1)",
        "PlayNormalFollowupAnimation()",
    )
    require("src/Models/Cards/SGC_SeizeFuture.cs", "new BlockVar(7m", "UpgradeValueBy(2m)")
    require("src/Models/Cards/SGC_TripleUnity.cs", "new PowerVar<SGP_TripleUnity>(3m)", "UpgradeValueBy(1m)")
    require("src/Models/Cards/SGC_ChosenOne.cs", "new PowerVar<SGP_ChosenOne>(1m)", "new BlockVar(4m", "UpgradeValueBy(2m)")
    require("src/Models/Cards/SGC_IronWall.cs", "new PowerVar<SGP_IronWall>(7m)", "ShinGetterForm.Getter3")
    require("src/Models/Cards/SGC_LigerAssault.cs", "new PowerVar<SGP_Shade>(1m)", "new PowerVar<BufferPower>(1m)", "PowerCmd.Apply<BufferPower>")
    reject("src/Models/Cards/SGC_LigerAssault.cs", "if (x > 0 && HasForm")
    require(
        "src/Models/Cards/SGC_TornadoDrill.cs",
        "new CalculationBaseVar(16m)",
        "new ExtraDamageVar(16m)",
        "UpgradeValueBy(5m)",
    )
    require("src/Models/Cards/SGC_Enable.cs", "ShinGetterForm.Getter1", "PlayerCmd.EndTurn")
    require("src/Models/Cards/ShinGetterCardBase.cs", "GetPower<SGP_Seal>()", "FlashBlockedTransform()")
    require(
        "src/Models/Powers/SGP_Evolution.cs",
        "AfterPlayerTurnStartLate",
        "int evolutionAmount = Amount;",
        "PowerCmd.ModifyAmount(choiceContext, this, -1m",
    )
    reject("src/Models/Powers/SGP_Evolution.cs", "BeforeSideTurnEnd")
    # Evolution Engine was superseded by B1.1.1 and is gated by validate_issue_47.py.
    require(
        "src/Patches/ShinGetterSpiritCommandRetainPatch.cs",
        'HarmonyPatch(typeof(CardModel), "get_ShouldRetainThisTurn")',
        "SpiritRequirement: > 0",
        "GetPower<SGP_Ki>()?.Amount > 0",
    )
    spirit_commands = (
        "SGC_HotBlood.cs",
        "SGC_Spirit.cs",
        "SGC_Acceleration.cs",
        "SGC_Insight.cs",
        "SGC_FightingSpirit.cs",
        "SGC_IronWall.cs",
        "SGC_Guts.cs",
        "SGC_AwakenedSoul.cs",
        "SGC_Enable.cs",
    )
    for filename in spirit_commands:
        reject(f"src/Models/Cards/{filename}", "保留", "CardKeyword.Retain", "ShouldRetainThisTurn")


def validate_localization() -> None:
    evolution_requirements = {
        "eng": ("start of your turn", "lose 1 Evolution"),
        "jpn": ("ターン開始時", "進化を1失う"),
        "zhs": ("回合开始时", "失去1层进化"),
    }
    for language in ("eng", "jpn", "zhs"):
        cards_path = f"ShinGetterMod/localization/{language}/cards.json"
        powers_path = f"ShinGetterMod/localization/{language}/powers.json"
        hover_tips_path = f"ShinGetterMod/localization/{language}/static_hover_tips.json"
        cards = json.loads(read(cards_path))
        powers = json.loads(read(powers_path))
        hover_tips = json.loads(read(hover_tips_path))
        if "{BufferPower:diff()}" not in cards["S_G_C_LIGER_ASSAULT.description"]:
            raise AssertionError(f"{cards_path}: Liger Assault is missing BufferPower")
        if "S_G_P_EVOLUTION.description" not in powers:
            raise AssertionError(f"{powers_path}: Evolution description is missing")
        evolution = powers["S_G_P_EVOLUTION.description"]
        if any(fragment not in evolution for fragment in evolution_requirements[language]):
            raise AssertionError(f"{powers_path}: Evolution timing/retention is stale")
        stale_fragments = ("not consumed", "進化のスタックは消費されない", "进化层数不会消耗")
        if any(fragment in evolution for fragment in stale_fragments):
            raise AssertionError(f"{powers_path}: Evolution still claims it is not consumed")
        forbidden_retain_markers = {
            "eng": ("retain",),
            "jpn": ("保留",),
            "zhs": ("保留",),
        }[language]
        spirit_command_descriptions = {
            powers_path: powers["S_G_P_KI.description"],
            hover_tips_path: hover_tips["SHIN_GETTER_SPIRIT_COMMAND.description"],
        }
        for path, description in spirit_command_descriptions.items():
            if any(marker in description.lower() for marker in forbidden_retain_markers):
                raise AssertionError(f"{path}: Spirit Command description still claims Retain")


def validate_sprite_sheets() -> None:
    forms = ROOT / "images/characters/shin_getter/forms"
    sheets = sorted(forms.glob("*/sprite_sheet.png"))
    imports = sorted(forms.glob("*/sprite_sheet.png.import"))
    if len(sheets) != 29 or len(imports) != 29:
        raise AssertionError(f"expected 29 sheets/imports, got {len(sheets)}/{len(imports)}")
    frame_pngs = [path for path in forms.glob("*/sprite_*.png") if path.name != "sprite_sheet.png"]
    if frame_pngs:
        raise AssertionError("runtime form directories still contain per-frame PNG files")
    expected_imports = {
        "attack": ("compress/mode=0", None),
        "block": ("compress/mode=1", "compress/lossy_quality=0.75"),
        "cast": ("compress/mode=0", None),
        "dash": ("compress/mode=1", "compress/lossy_quality=0.75"),
        "death": ("compress/mode=1", "compress/lossy_quality=0.6"),
        "fusion": ("compress/mode=1", "compress/lossy_quality=0.75"),
        "idle": ("compress/mode=1", "compress/lossy_quality=0.75"),
        "stoner_sunshine": ("compress/mode=0", None),
    }
    for sidecar in imports:
        text = sidecar.read_text(encoding="utf-8")
        action = (
            "stoner_sunshine"
            if sidecar.parent.name.endswith("_stoner_sunshine")
            else sidecar.parent.name.rsplit("_", 1)[-1]
        )
        if action not in expected_imports:
            raise AssertionError(f"{sidecar.relative_to(ROOT)}: unknown animation action")
        expected_mode, expected_quality = expected_imports[action]
        required = ['"vram_texture": false', expected_mode, "mipmaps/generate=false"]
        if expected_quality is not None:
            required.append(expected_quality)
        if any(fragment not in text for fragment in required):
            raise AssertionError(
                f"{sidecar.relative_to(ROOT)}: expected {action} import policy {required}"
            )
        forbidden = ('"vram_texture": true', "compress/mode=2", "s3tc", "bptc")
        if any(fragment in text.lower() for fragment in forbidden):
            raise AssertionError(f"{sidecar.relative_to(ROOT)}: VRAM compression metadata remains")

    source_root = REPO_ROOT / "art_sources/characters/shin_getter/forms"
    source_frames = list(source_root.glob("*/sprite_*.png"))
    if len(source_frames) != 1130:
        raise AssertionError(f"expected 1130 source frames, got {len(source_frames)}")
    frame_manifest = load_frame_manifest(source_root / "frame_manifest.txt")
    manifest_frame_paths = {
        f"{action}/sprite_{frame_number:06d}.png"
        for action, frame_numbers in frame_manifest.items()
        for frame_number in frame_numbers
    }
    actual_frame_paths = {path.relative_to(source_root).as_posix() for path in source_frames}
    manifest_entry_count = sum(len(frame_numbers) for frame_numbers in frame_manifest.values())
    if manifest_entry_count != 1130 or manifest_frame_paths != actual_frame_paths:
        missing = sorted(actual_frame_paths - manifest_frame_paths)
        extra = sorted(manifest_frame_paths - actual_frame_paths)
        raise AssertionError(
            "PCK forbidden frame set does not exactly match the 1130 source files; "
            f"missing={missing[:3]}, extra={extra[:3]}"
        )
    validator_path = "tools/validate-mod-resources.gd"
    require(
        validator_path,
        "CHARACTER_FRAME_MANIFEST_PATH",
        "_load_character_frame_manifest(pck_path)",
        "EXPECTED_CHARACTER_SOURCE_FRAME_COUNT := 1130",
    )
    reject(validator_path, "FORBIDDEN_CHARACTER_FRAME_COUNTS", "range(1, FORBIDDEN_CHARACTER_FRAME_COUNTS")
    if (ROOT / "art_sources/characters/shin_getter/forms").exists():
        raise AssertionError("source frames remain inside the Godot project import scope")

    imported_cache = ROOT / ".godot/imported"
    stale_frame_imports = list(imported_cache.glob("sprite_[0-9][0-9][0-9][0-9][0-9][0-9].png-*"))
    if stale_frame_imports:
        raise AssertionError(f"Godot cache still contains {len(stale_frame_imports)} per-frame imports")

    for name in ("one", "two", "three", "dragon"):
        require(f"scenes/creature_visuals/shin_getter_{name}_idle_frames.tres", "AtlasTexture", "region = Rect2(")


def main() -> None:
    validate_balance()
    validate_localization()
    validate_sprite_sheets()
    print("B1.1.0 static validation passed")


if __name__ == "__main__":
    main()
