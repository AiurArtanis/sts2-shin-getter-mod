#!/usr/bin/env python3
"""Static regression gate for the B1.1.0 balance and sprite-sheet update."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


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
    require("src/Models/Cards/SGC_ChangeAttack.cs", "new DamageVar(7m")
    require("src/Models/Cards/SGC_SeizeFuture.cs", "new BlockVar(7m", "UpgradeValueBy(2m)")
    require("src/Models/Cards/SGC_TripleUnity.cs", "new PowerVar<SGP_TripleUnity>(3m)", "UpgradeValueBy(1m)")
    require("src/Models/Cards/SGC_ChosenOne.cs", "new PowerVar<SGP_ChosenOne>(1m)", "new BlockVar(4m", "UpgradeValueBy(2m)")
    require("src/Models/Cards/SGC_IronWall.cs", "new PowerVar<SGP_IronWall>(7m)", "ShinGetterForm.Getter3")
    require("src/Models/Cards/SGC_LigerAssault.cs", "new PowerVar<SGP_Shade>(1m)", "new PowerVar<BufferPower>(1m)", "PowerCmd.Apply<BufferPower>")
    require("src/Models/Cards/SGC_Enable.cs", "ShinGetterForm.Getter1", "PlayerCmd.EndTurn")
    require("src/Models/Cards/ShinGetterCardBase.cs", "GetPower<SGP_Seal>()", "FlashBlockedTransform()")
    require("src/Models/Powers/SGP_Evolution.cs", "AfterPlayerTurnStartLate", "int evolutionAmount = Amount;")
    reject("src/Models/Powers/SGP_Evolution.cs", "BeforeSideTurnEnd", "ModifyAmount(choiceContext, this")
    require("src/Models/Powers/SGP_EvolutionEngine.cs", "TurnNumber > data.markedTurnNumber", "MarkPendingEnergyGain()")


def validate_localization() -> None:
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
        if language == "eng" and ("start of your turn" not in evolution or "not consumed" not in evolution):
            raise AssertionError(f"{powers_path}: Evolution timing/retention is stale")


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
        if '"vram_texture": true' not in text or "compress/mode=2" not in text:
            raise AssertionError(f"{sidecar.relative_to(ROOT)}: VRAM compression is not enabled")

    source_frames = list((ROOT / "art_sources/characters/shin_getter/forms").glob("*/sprite_*.png"))
    if len(source_frames) != 920:
        raise AssertionError(f"expected 920 source frames, got {len(source_frames)}")

    for name in ("one", "two", "three", "dragon"):
        require(f"scenes/creature_visuals/shin_getter_{name}_idle_frames.tres", "AtlasTexture", "region = Rect2(")


def main() -> None:
    validate_balance()
    validate_localization()
    validate_sprite_sheets()
    print("B1.1.0 static validation passed")


if __name__ == "__main__":
    main()
