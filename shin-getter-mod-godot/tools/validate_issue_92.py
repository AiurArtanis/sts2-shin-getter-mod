#!/usr/bin/env python3
"""Static regression gate for issue#92 / B1.2.0."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


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
        'new DynamicVar("WaneMultiplier", 2m)',
        ": base(3, CardType.Attack",
        "PowerCmd.Apply<SGP_Wane>",
        "PowerCmd.Apply<SGP_FinalGetterBeam>",
        'DynamicVars["WaneMultiplier"].BaseValue',
        'DynamicVars["WaneMultiplier"].UpgradeValueBy(1m)',
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
    card_path = "src/Models/Cards/SGC_StonerSunshine.cs"
    require(
        card_path,
        "if (HasForm(Owner, ShinGetterForm.Getter1))",
        "TryPlayCardVoiceAtCustomTiming",
        "TryPlayStonerSunshineAnimation(",
        "await ShinGetterCombatVfx.PlayStonerSunshine(",
        "const int impactFrame = 71",
        "recoveryDurationSeconds",
        ".WithNoAttackerAnim()",
        "ShinGetterCombatVfx.PlayEnergyBall(",
    )
    card_text = read(card_path)
    if card_text.count('TryPlayCreatureActionAnimation(Owner.Creature, "Cast")') != 1:
        raise AssertionError("only Getter Two/Three may explicitly play the shared Cast animation")
    if card_text.count(".WithNoAttackerAnim()") != 2:
        raise AssertionError("Stoner Sunshine attacks must not replay the explicit Cast animation")
    if ".WithAttackerAnim(" in card_text:
        raise AssertionError("Stoner Sunshine contains a duplicate command-driven attacker animation")
    if card_text.index("await ShinGetterCombatVfx.PlayStonerSunshine(") > card_text.index("attackCommand = await DamageCmd.Attack"):
        raise AssertionError("Stoner Sunshine damage must wait until the dedicated animation reaches impact")
    if ".BeforeDamage(() => ShinGetterCombatVfx.PlayStonerSunshine(" in card_text:
        raise AssertionError("Stoner Sunshine cannot use the engine's pre-hit callback for its delayed impact")
    require(
        "src/Models/Cards/ShinGetterCardBase.cs",
        '"SGC_StonerSunshine",',
    )
    require(
        "src/Audio/ShinGetterVoiceService.cs",
        "card is SGC_StonerSunshine",
        "SGC_StonerSunshine => Lines[ShinGetterVoiceCue.StonerSunshine]",
        "ShinGetterCardBase.IsInForm(card.Owner, ShinGetterForm.Getter1)",
    )
    require(
        "src/Nodes/Combat/NShinGetterSpriteSequence.cs",
        'StonerSunshineAnimationName = "stoner_sunshine"',
        "GetterOneStonerSunshineFrameDirectory",
        "ShinDragonStonerSunshineFrameDirectory",
        "StonerSunshineMaxFrames = 90",
        "StonerSunshineFramesPerSecond = 30d",
    )
    sequence_text = read("src/Nodes/Combat/NShinGetterSpriteSequence.cs")
    if sequence_text.count("StonerSunshineAnimationName,") != 3:
        raise AssertionError("Stoner Sunshine must be registered only for Getter One, Shin Dragon, and cache cleanup")
    require(
        "src/Nodes/Combat/NShinGetterSpriteAnimationStateMachine.cs",
        '"StonerSunshine" => NShinGetterSpriteSequence.StonerSunshineAnimationName',
    )
    require(
        "src/Nodes/Combat/NShinGetterStaticVisuals.cs",
        "TryPlayStonerSunshineAnimation(",
        "baseDuration / Math.Max(0.1f, sequenceDurationSeconds)",
        '"StonerSunshine"',
        'formAnimation.Sprite, "Cast"',
    )
    require(
        "src/Nodes/Vfx/ShinGetterCombatVfx.cs",
        "PlayStonerSunshine(",
        "const int frameCount = 90",
        "const int chargeStartFrame = 30",
        "const int lightningStartFrame = 45",
        "const int launchFrame = 63",
        "const int impactFrame = 71",
        "new Vector2(56f, -118f)",
        "new Vector2(150f, -95f)",
        "chargeStartDelay",
        ".SetEase(Tween.EaseType.InOut)",
        ".SetTrans(Tween.TransitionType.Sine)",
        "firstGrowthDuration",
        "secondGrowthDuration",
        "CreateSolarCoronaRay(",
        "AddSolarGradientLayers(",
        "CreateSolarGloss(",
        "CreateSolarLightning(",
        "const int layerCount = 22",
        "flightDurationSeconds",
    )
    reject(
        "src/Nodes/Vfx/ShinGetterCombatVfx.cs",
        'ownerOrigin + Vector2.Up * 150f, 0.42f',
        "airborneOffset",
    )

    for action in (
        "getter_one_stoner_sunshine",
        "shin_getter_dragon_stoner_sunshine",
    ):
        source_dir = ROOT.parent / "art_sources/characters/shin_getter/forms" / action
        source_frames = sorted(source_dir.glob("sprite_*.png"))
        if len(source_frames) != 90:
            raise AssertionError(f"{action}: expected 90 source frames, got {len(source_frames)}")
        sheet_path = ROOT / "images/characters/shin_getter/forms" / action / "sprite_sheet.png"
        with Image.open(sheet_path) as sheet:
            if sheet.size != (7200, 6480):
                raise AssertionError(f"{action}: expected 7200x6480 sheet, got {sheet.size}")
        require(
            f"images/characters/shin_getter/forms/{action}/sprite_sheet.png.import",
            '"vram_texture": false',
            "compress/mode=0",
            "mipmaps/generate=false",
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
        if "{WaneMultiplier:diff()}" not in beam_description:
            raise AssertionError(f"{language}: Final Getter Beam multiplier is not dynamic")
        if "{SGP_FinalGetterBeam:diff()}" in beam_description:
            raise AssertionError(f"{language}: stale Final Getter Beam power variable")
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
    big_icon = ROOT / "images/powers/s_g_p_final_getter_beam.png"
    legacy_icon = ROOT / "images/powers/s_g_p_final_getter_beam_strength_down.png"
    if not big_icon.is_file():
        raise AssertionError("Final Getter Beam big flash icon is missing")
    if big_icon.read_bytes() != legacy_icon.read_bytes():
        raise AssertionError("Final Getter Beam big flash does not use its status icon")
    require(
        "images/powers/s_g_p_final_getter_beam.png.import",
        'source_file="res://images/powers/s_g_p_final_getter_beam.png"',
        "compress/mode=0",
        "mipmaps/generate=false",
    )


def main() -> None:
    validate_final_getter_beam()
    validate_stoner_sunshine()
    validate_localization()
    validate_power_icon()
    print("issue#92 static validation passed")


if __name__ == "__main__":
    main()
