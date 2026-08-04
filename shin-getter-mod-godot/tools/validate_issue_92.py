#!/usr/bin/env python3
"""Static regression gate for issue#92 / B1.2.0."""

from __future__ import annotations

import hashlib
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


def validate_final_getter_beam() -> None:
    card_path = "src/Models/Cards/SGC_FinalGetterBeam.cs"
    require(
        card_path,
        "new DamageVar(25m, ValueProp.Move)",
        "new PowerVar<SGP_Wane>(4m)",
        "new PowerVar<SGP_FinalGetterBeam>(2m)",
        ": base(3, CardType.Attack",
        "PowerCmd.Apply<SGP_Wane>",
        "PowerCmd.Apply<SGP_FinalGetterBeam>",
        'DynamicVars["SGP_FinalGetterBeam"].UpgradeValueBy(1m)',
    )
    reject(card_path, "StrengthLoss", "EnergyCost.UpgradeBy(-1)")
    require(
        "src/Models/Powers/SGP_FinalGetterBeam.cs",
        "PowerType.Debuff",
        "PowerStackType.Counter",
    )
    require(
        "src/Models/Powers/SGP_Wane.cs",
        "Owner.GetPower<SGP_FinalGetterBeam>()?.Amount ?? 1",
        "PowerCmd.Apply<SGP_Wane>(choiceContext, Owner, growthAmount",
    )
    require(
        "src/Models/Powers/SGP_FinalGetterBeamStrengthDown.cs",
        "SGP_FinalGetterBeamStrengthDown",
    )


def validate_stoner_sunshine() -> None:
    require(
        "src/Models/Cards/SGC_StonerSunshine.cs",
        "TryPlayCardVoiceAtCustomTiming",
        "QueueNextActionSpeed(Owner.Creature, 0.3f)",
        'TryPlayCreatureActionAnimation(Owner.Creature, "Cast")',
        "PlayStonerSunshine(",
        ".WithNoAttackerAnim()",
    )
    require(
        "src/Models/Cards/ShinGetterCardBase.cs",
        '"SGC_StonerSunshine",',
    )
    require(
        "src/Audio/ShinGetterVoiceService.cs",
        "or SGC_StonerSunshine",
    )
    require(
        "src/Nodes/Vfx/ShinGetterCombatVfx.cs",
        "PlayStonerSunshine(",
        "Vector2.Up * 150f",
        "firstGrowthDuration",
        "secondGrowthDuration",
        "CreateAuraLightning(",
        "flightDurationSeconds",
    )

    expected_assets = {
        "images/packed/card_single/shin_getter/s_g_c_stoner_sunshine_card.png":
            "f24869c6b96cf7edc4d941196ef62c6996bc33371458303a7c136e3878134a76",
        "images/packed/card_portraits/shin_getter/s_g_c_stoner_sunshine.png":
            "6b138237f710fbb4472c1b91944bad669adc022ebb71524f44b942e50f04e1df",
    }
    for relative_path, expected_hash in expected_assets.items():
        actual_hash = hashlib.sha256((ROOT / relative_path).read_bytes()).hexdigest()
        if actual_hash != expected_hash:
            raise AssertionError(
                f"{relative_path}: expected sha256 {expected_hash}, got {actual_hash}"
            )


def validate_localization() -> None:
    for language in ("zhs", "eng", "jpn"):
        cards = json.loads(read(f"ShinGetterMod/localization/{language}/cards.json"))
        powers = json.loads(read(f"ShinGetterMod/localization/{language}/powers.json"))
        characters = json.loads(read(f"ShinGetterMod/localization/{language}/characters.json"))
        beam_description = cards["S_G_C_FINAL_GETTER_BEAM.description"]
        if "{Damage:diff()}" not in beam_description:
            raise AssertionError(f"{language}: Final Getter Beam damage is missing")
        if "{SGP_Wane:diff()}" not in beam_description:
            raise AssertionError(f"{language}: Final Getter Beam Wane is missing")
        if "{SGP_FinalGetterBeam:diff()}" not in beam_description:
            raise AssertionError(f"{language}: Final Getter Beam growth is missing")
        if "S_G_P_FINAL_GETTER_BEAM.description" not in powers:
            raise AssertionError(f"{language}: Final Getter Beam power text is missing")
        if characters["SHIN_GETTER.voice.stonerSunshine"] != (
            "[red]Stoner[/red]\n[yellow]Sunshine[/yellow]"
        ):
            raise AssertionError(f"{language}: Stoner Sunshine subtitle is stale")


def validate_power_icon() -> None:
    require(
        "images/atlases/power_atlas.sprites/s_g_p_final_getter_beam.tres",
        "region = Rect2(128, 256, 64, 64)",
    )


def main() -> None:
    validate_final_getter_beam()
    validate_stoner_sunshine()
    validate_localization()
    validate_power_icon()
    print("issue#92 static validation passed")


if __name__ == "__main__":
    main()
