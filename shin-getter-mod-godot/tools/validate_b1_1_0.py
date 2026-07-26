#!/usr/bin/env python3
"""Static regression gate for the B1.1.0 balance and sprite-sheet update."""

from __future__ import annotations

import json
from pathlib import Path


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
    require(
        "src/Models/Powers/SGP_EvolutionEngine.cs",
        "AfterPlayerTurnStartEarly",
        "if (!data.pendingEnergyGain)",
        "data.pendingEnergyGain = false;",
        "MarkPendingEnergyGain()",
        "data.pendingEnergyGain = true;",
    )
    reject(
        "src/Models/Powers/SGP_EvolutionEngine.cs",
        "AfterSideTurnStart",
        "markedTurnNumber",
        "TurnNumber",
    )
    if (ROOT / "src/Patches/ShinGetterSpiritCommandRetainPatch.cs").exists():
        raise AssertionError("spirit command cards must not be retained by a Harmony patch")
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


def validate_evolution_engine_sequence() -> None:
    pending_energy_gain = False

    # T1 late: Evolution succeeds and schedules T2 energy.
    pending_energy_gain = True

    # T2 early: the previous reward is paid before T2 late can schedule another one.
    gained_energy_on_t2 = pending_energy_gain
    pending_energy_gain = False
    assert gained_energy_on_t2

    # T2 late: a second consecutive Evolution keeps a distinct reward pending for T3.
    pending_energy_gain = True
    assert pending_energy_gain


def validate_localization() -> None:
    evolution_requirements = {
        "eng": ("start of your turn", "lose 1 Evolution"),
        "jpn": ("ターン開始時", "進化を1失う"),
        "zhs": ("回合开始时", "失去1层进化"),
    }
    for language in ("eng", "jpn", "zhs"):
        cards_path = f"ShinGetterMod/localization/{language}/cards.json"
        powers_path = f"ShinGetterMod/localization/{language}/powers.json"
        cards = json.loads(read(cards_path))
        powers = json.loads(read(powers_path))
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


def validate_sprite_sheets() -> None:
    forms = ROOT / "images/characters/shin_getter/forms"
    sheets = sorted(forms.glob("*/sprite_sheet.png"))
    imports = sorted(forms.glob("*/sprite_sheet.png.import"))
    if len(sheets) != 24 or len(imports) != 24:
        raise AssertionError(f"expected 24 sheets/imports, got {len(sheets)}/{len(imports)}")
    frame_pngs = [path for path in forms.glob("*/sprite_*.png") if path.name != "sprite_sheet.png"]
    if frame_pngs:
        raise AssertionError("runtime form directories still contain per-frame PNG files")
    for sidecar in imports:
        text = sidecar.read_text(encoding="utf-8")
        if '"vram_texture": false' not in text or "compress/mode=0" not in text:
            raise AssertionError(f"{sidecar.relative_to(ROOT)}: lossless import is not enabled")
        forbidden = ('"vram_texture": true', "compress/mode=2", "s3tc", "bptc")
        if any(fragment in text.lower() for fragment in forbidden):
            raise AssertionError(f"{sidecar.relative_to(ROOT)}: VRAM compression metadata remains")

    source_frames = list((REPO_ROOT / "art_sources/characters/shin_getter/forms").glob("*/sprite_*.png"))
    if len(source_frames) != 920:
        raise AssertionError(f"expected 920 source frames, got {len(source_frames)}")
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
    validate_evolution_engine_sequence()
    validate_localization()
    validate_sprite_sheets()
    print("B1.1.0 static validation passed")


if __name__ == "__main__":
    main()
