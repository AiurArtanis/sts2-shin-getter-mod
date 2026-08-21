#!/usr/bin/env python3
"""Focused static validation for issue#10 transform animation resources and wiring."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
REPOSITORY = PROJECT.parent
FRAME_SIZE = 720
FUSION_ACTIONS = ("getter_one_fusion", "getter_two_fusion", "getter_three_fusion")
EXPECTED_FIRST_FRAME_SHA256 = "31ebc1f3e222299e09c7448ee52cf6cd307c8a18ea5f832844a60dffaf9770ee"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def check_fusion_sheets() -> None:
    manifest = read(REPOSITORY / "art_sources/characters/shin_getter/forms/frame_manifest.txt")
    builder = read(PROJECT / "tools/build_character_sprite_sheets.py")
    require("\"getter_one_fusion\": 30" in builder, "getter_one fusion frame count is missing")
    require("\"getter_two_fusion\": 30" in builder, "getter_two fusion frame count is missing")
    require("\"getter_three_fusion\": 30" in builder, "getter_three fusion frame count is missing")
    require("30: 6" in builder, "fusion sheets must use six columns")
    require("sequence contiguous_30=" in manifest, "30-frame source sequence is missing")

    first_hashes: list[str] = []
    for action in FUSION_ACTIONS:
        require(f"action {action}=contiguous_30" in manifest, f"{action} is not in the manifest")
        source = REPOSITORY / "art_sources/characters/shin_getter/forms" / action
        frames = sorted(source.glob("sprite_*.png"))
        require(len(frames) == 30, f"{action} must have exactly 30 source frames")
        for frame in frames:
            with Image.open(frame) as image:
                require(image.size == (FRAME_SIZE, FRAME_SIZE), f"{frame} is not 720x720")
        first_hashes.append(hashlib.sha256(frames[0].read_bytes()).hexdigest())

        sheet = PROJECT / "images/characters/shin_getter/forms" / action / "sprite_sheet.png"
        with Image.open(sheet) as image:
            require(image.size == (FRAME_SIZE * 6, FRAME_SIZE * 5), f"{sheet} must be a 6x5 sheet")

    require(first_hashes == [EXPECTED_FIRST_FRAME_SHA256] * 3, "fusion sources do not share the authoritative fighter frame")


def check_code_wiring() -> None:
    sequence = read(PROJECT / "src/Nodes/Combat/NShinGetterSpriteSequence.cs")
    visuals = read(PROJECT / "src/Nodes/Combat/NShinGetterStaticVisuals.cs")
    open_get = read(PROJECT / "src/Models/Powers/SGP_OpenGet.cs")
    card_base = read(PROJECT / "src/Models/Cards/ShinGetterCardBase.cs")
    choice = read(PROJECT / "src/Nodes/Combat/NShinGetterFormChoice.cs")
    shade = read(PROJECT / "src/Models/Powers/SGP_Shade.cs")
    landing = read(PROJECT / "src/Models/Cards/SGC_GetterLanding.cs")
    pool = read(PROJECT / "src/Models/CardPools/ShinGetterCardPool.cs")
    voice = read(PROJECT / "src/Audio/ShinGetterVoiceService.cs")
    getter_one = read(PROJECT / "src/Models/Powers/SGP_ShinGetterOne.cs")
    getter_furnace = read(PROJECT / "src/Models/Relics/SGR_GetterFurnace.cs")
    emperors_fragment = read(PROJECT / "src/Models/Relics/SGR_EmperorsFragment.cs")
    intent_patch = read(PROJECT / "src/Patches/ShinGetterOpenGetIntentPatch.cs")
    combat_vfx = read(PROJECT / "src/Nodes/Vfx/ShinGetterCombatVfx.Extra.cs")
    tactical_retreat = read(PROJECT / "src/Models/Cards/SGC_TacticalRetreat.cs")
    form_powers = "\n".join(read(PROJECT / f"src/Models/Powers/{name}") for name in (
        "SGP_ShinGetterOne.cs", "SGP_ShinGetterTwo.cs", "SGP_ShinGetterThree.cs"))

    require("FusionFramesPerSecond = 60d" in sequence, "fusion animation must run at 60fps")
    require("EnsureFusionLoaded" in sequence and "FusionAnimationName" in sequence, "fusion sequence loader is missing")
    require("TryPlayFusionTransition" in visuals and "PlayOpenGetVfx" in visuals, "fusion visual flow is missing")
    require("PlayShadeVfx" in visuals, "shade visual hook is missing")
    require("FusionTransitionHoldSeconds = 0.2f" in visuals,
            "ordinary fusion transitions must hold the shared fighter frame for 0.2 seconds")
    require("ShadeAfterimageSpacing = 182f" in visuals and "sprites.All" in visuals,
            "Shade must show its wider afterimages for every Getter form, including Shin Dragon")
    require("ShinDragonOpenGetAlpha = 0.3f" in visuals and "FormVisual shinDragon = sprites.ShinDragon" in visuals,
            "Shin Dragon Open Get must simulate separation with a 30%-to-100% opacity tween")
    require("DisplayAmount => Amount - 1" in open_get, "Open Get must expose a zero starting counter")
    require("dealer?.Side != Owner.Side" in open_get, "Open Get must account for allied damage")
    require("target.CombatState?.CurrentSide != CombatSide.Player" in open_get,
            "Open Get must not accumulate damage during the enemy turn")
    require("WouldAvoidAttack" in open_get and "totalAttackDamage > 0m" in open_get,
            "Open Get must reject zero-damage hits before avoidance")
    require("shade?.WouldPreventCurrentHit(dealer) != true" in open_get,
            "Open Get must not consume itself on a multi-hit already nullified by Shade")
    require("RecordOpenGetAvoidedHit(dealer)" in open_get
            and "WouldPreventCurrentHit" in shade and "RecordOpenGetAvoidedHit" in shade,
            "Open Get and Shade must share the consumed first hit of a multi-hit attack")
    require("GetPower<SGP_ShinForm>() == null" not in open_get,
            "Shin Dragon must remain eligible for Open Get avoidance")
    require("ModifyDamageMultiplicative" in open_get and "AfterDamageGiven" in open_get, "Open Get avoidance/accounting is missing")
    require("AfterEnergyReset" in open_get, "Open Get must expire at turn start")
    require("PlayShadeVfx" in shade, "Shade does not trigger its visual effect")
    require("NShinGetterFormChoice" in landing, "Getter Landing does not show form choices")
    require("GetPower<SGP_OpenGet>() == null" in landing and "PowerCmd.Remove<SGP_OpenGet>" not in landing,
            "replaying Getter Landing must retain Open Get's accumulated damage")
    require("ModelDb.Card<SGC_GetterLanding>()" in pool, "Getter Landing is not registered")
    require("and not SGC_GetterLanding" in pool, "Getter Landing must stay out of reward epochs")
    require("PlayerChoiceSynchronizer" in choice and "PlayerChoiceResult.FromIndex" in choice, "form choice must synchronize in multiplayer")
    require("ShouldSelectLocalForm" in choice and "CardSelectCmd.ShouldSelectLocalCard" not in choice,
            "form choice must not call CardSelectCmd's private selector")
    require("FocusMode = Control.FocusModeEnum.All" in choice, "form choices must accept controller focus")
    require("FocusModeEnum.None" not in choice, "form choices must not opt out of controller focus")
    require("FocusNeighborLeft" in choice and "FocusNeighborRight" in choice,
            "form choices must have circular left/right controller navigation")
    require("buttons[0].TryGrabFocus()" in choice, "form choices must receive initial controller focus")
    require("NPlayerHand.Instance" in choice and "DefaultFocusedControl" in choice and "TryGrabFocus" in choice,
            "form choice must restore the combat hand focus after its temporary controls are released")
    require("GodotObject.IsInstanceValid" in choice and "Node.SignalName.TreeExited" in choice,
            "form choice must restore focus only after valid temporary controls leave the tree")
    require("FormIconSize = 96f" in choice and "CreateGetterOutline" in choice,
            "Getter Landing choices must use enlarged coloured outlines")
    require("FormOutlineRadius = 54" in choice
            and "Position = (outlineSize - Vector2.One * FormIconSize) / 2f" in choice
            and "StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered" in choice,
            "Getter Landing circular outlines and icons must share the same centre")
    require("FormIconSpacing = 156f" in choice and "FormIconHoverScale = 1.25f" in choice,
            "Getter Landing choices must use the widened spacing and 25% hover scale")
    require("PivotOffset = Vector2.One * FormIconSize / 2f" in choice
            and "button.MouseEntered" in choice and "button.MouseExited" in choice,
            "Getter Landing hover scaling must remain centred and reversible")
    require("result.TotalDamage" in open_get and "result.UnblockedDamage" not in open_get,
            "Open Get accumulated-damage contract must include damage absorbed by block")
    require("amount * data.HitCount" in open_get and "data.WillAvoidActiveAttack" in open_get,
            "Open Get must judge and avoid a multi-attack by its total damage")
    require("ModifyAttackHitCount" in open_get and "AfterAttack" in open_get,
            "Open Get must retain its active AttackCommand through every eligible hit")
    require("IsCalculatingIntentDamage" in open_get and "return 1m" in open_get,
            "Open Get must not reduce the displayed attack-intent damage")
    require("SHIN_GETTER_OPEN_GET_INTENT_SINGLE" in intent_patch
            and "SHIN_GETTER_OPEN_GET_INTENT_MULTI" in intent_patch,
            "Open Get intent labels must preserve damage while adding the avoidance marker")
    require("HoverMeta = \"shin_getter_open_get\"" in intent_patch
            and "MetaHoverStarted" in intent_patch and "CreateAvoidanceHoverTip" in intent_patch,
            "the red Open Get intent X must expose its localized hover tip")
    require("intent.GetTotalDamage" in intent_patch and "WouldAvoidIntent(totalDamage)" in intent_patch,
            "multi-hit intent avoidance must compare the full displayed damage total")
    require("TacticalRetreatDistance = 480f" in combat_vfx
            and "TryPlayCreatureActionAnimation(owner, \"Block\")" in combat_vfx,
            "Tactical Retreat must defend and move back three body positions")
    require("Task transformTask = transform()" in combat_vfx
            and "await ownerNode.ToSignal(returnTween" in combat_vfx
            and "await transformTask" in combat_vfx
            and "TacticalRetreatReturnSeconds = 1.5f" in combat_vfx,
            "Tactical Retreat must transform while returning to its origin")
    require("TransformSpeedScale = 0.75f" in tactical_retreat
            and form_powers.count("SGC_TacticalRetreat.TransformSpeedScale") == 3,
            "Tactical Retreat fusion must run at 0.75 speed in every atomic form")
    require("PrepareOpeningGetterOneFusion" in getter_one,
            "combat-start form setup must hide the idle form before opening fusion")
    opening_start = visuals.index("private static async Task PlayOpeningGetterOneFusion(FormSprites sprites)")
    opening_end = visuals.index("    private static async Task<bool> TryPlayFusionTransition", opening_start)
    opening_fusion = visuals[opening_start:opening_end]
    require("TryPrepareFusionAnimation" in opening_fusion and "PlayPreparedFusionAnimation" in opening_fusion,
            "combat-opening Getter One fusion must be prepared before it becomes visible")
    require(opening_fusion.index("TryPrepareFusionAnimation") < opening_fusion.index("next.Item.Visible = true")
            < opening_fusion.index("PlayPreparedFusionAnimation"),
            "combat-opening Getter One must show fusion frame one before it starts playing")
    require("backwards: true" not in opening_fusion and "previous" not in opening_fusion and "forceFusion" not in visuals,
            "combat-opening fusion must never reverse a previous form")
    transition_start = visuals.index("private static async Task<bool> TryPlayFusionTransition")
    transition_end = visuals.index("    private static async Task PlayFusionAnimation", transition_start)
    fighter_transition = visuals[transition_start:transition_end]
    require("backwards: true" in fighter_transition and "backwards: false" in fighter_transition,
            "ordinary atomic form changes must retain reverse-then-forward fusion")
    require(fighter_transition.index("backwards: true") < fighter_transition.index("FusionTransitionHoldSeconds")
            < fighter_transition.index("next.Item.Visible = true") < fighter_transition.index("backwards: false"),
            "ordinary atomic changes must hold the shared fighter frame before forward fusion")
    require("FormTransformCards" in card_base and "if (TriggersFormTransform)" in card_base,
            "cards that transform forms must bypass the ordinary action animation")
    for card_name in ("SGC_ChangeAttack", "SGC_Enable", "SGC_GetterLanding", "SGC_GetterLaunch",
                      "SGC_IronWall", "SGC_Jammer", "SGC_ShiftStrike", "SGC_ShinForm", "SGC_TacticalRetreat"):
        require(f'"{card_name}"' in card_base, f"{card_name} must suppress ordinary action animation")
    require("ShouldDeferToOpenGet" in shade and "amount <= 0m" in shade,
            "Shade must defer to successful Open Get avoidance and ignore zero damage")
    for relic_name, relic in (("Getter Furnace", getter_furnace), ("Emperor's Fragment", emperors_fragment)):
        setup_index = relic.index("await ShinGetterEventInvasionService.ApplyPendingPreCombatSetup(Owner);")
        fusion_index = relic.index("Task openingFusion = NShinGetterStaticVisuals.PlayOpeningGetterOneFusion(Owner.Creature);")
        voice_index = relic.index("ShinGetterVoiceService.PlayPreparedCombatStart(Owner);")
        await_index = relic.index("await openingFusion;")
        require(setup_index < fusion_index < voice_index < await_index,
                f"{relic_name} must start prepared fusion and opening voice together after setup")
    for code in ("058", "059", "060"):
        require(f'new("{code}"' in voice, f"Open Get voice {code} is missing")
    require('new("060", ShinGetterVoiceCue.OpenGetThree, "benkei_open_get.wav"' in voice,
            "Open Get voice 060 must use Benkei's resource")
    require("GetPower<SGP_ShinForm>() != null" in voice,
            "Shin Dragon Open Get must retain Ryoma's voice cue")


def check_localization_and_assets() -> None:
    for locale in ("zhs", "eng", "jpn"):
        root = PROJECT / "ShinGetterMod/localization" / locale
        cards = json.loads(read(root / "cards.json"))
        powers = json.loads(read(root / "powers.json"))
        characters = json.loads(read(root / "characters.json"))
        static_tips = json.loads(read(root / "static_hover_tips.json"))
        require("S_G_C_GETTER_LANDING.title" in cards, f"{locale} Getter Landing title is missing")
        require("S_G_C_GETTER_LANDING.description" in cards, f"{locale} Getter Landing description is missing")
        require("S_G_P_OPEN_GET.title" in powers, f"{locale} Open Get title is missing")
        require("S_G_P_OPEN_GET.description" in powers, f"{locale} Open Get description is missing")
        for key in ("SHIN_GETTER.voice.openGetOne", "SHIN_GETTER.voice.openGetTwo", "SHIN_GETTER.voice.openGetThree"):
            require(key in characters, f"{locale} {key} is missing")
        for key in ("SHIN_GETTER_OPEN_GET_INTENT_SINGLE", "SHIN_GETTER_OPEN_GET_INTENT_MULTI",
                    "SHIN_GETTER_OPEN_GET_INTENT.title", "SHIN_GETTER_OPEN_GET_INTENT.description"):
            require(key in static_tips, f"{locale} {key} is missing")
        require("[color=#ff0000] X[/color]" in static_tips["SHIN_GETTER_OPEN_GET_INTENT_SINGLE"],
                f"{locale} Open Get intent marker must remain red")

    icon = read(PROJECT / "images/atlases/power_atlas.sprites/s_g_p_open_get.tres")
    require("region = Rect2(320, 256, 64, 64)" in icon, "Open Get small icon region is wrong")
    flash_icon = PROJECT / "images/powers/s_g_p_open_get.png"
    require(flash_icon.is_file() and flash_icon.with_name(f"{flash_icon.name}.import").is_file(),
            "Open Get flash requires a standalone imported power icon")
    power_atlas = PROJECT / "images/atlases/power_atlas_shin_getter.png"
    with Image.open(flash_icon) as image, Image.open(power_atlas) as atlas:
        require(image.size == (256, 256), "Open Get flash icon must use the standard 256x256 power image")
        expected = atlas.crop((1280, 1024, 1536, 1280)).convert("RGBA")
        require(image.convert("RGBA").tobytes() == expected.tobytes(),
                "Open Get flash icon must come from the large power atlas, not the small icon atlas")
    resource_gate = read(PROJECT / "tools/validate-mod-resources.gd")
    require("s_g_c_getter_landing.tres" in resource_gate and "s_g_p_open_get.tres" in resource_gate,
            "resource gate does not require issue#10 atlas resources")
    require("res://images/powers/s_g_p_open_get.png" in resource_gate,
            "resource gate must require the Open Get flash icon")
    require("benkei_open_get.wav" in resource_gate and "musashi_open_get.wav" not in resource_gate,
            "resource gate must require only Benkei's issue#10 Open Get voice")
    for filename in ("ryoma_open_get.wav", "hayato_open_get.wav", "benkei_open_get.wav"):
        path = PROJECT / "audio/sfx/characters/shin_getter/voices" / filename
        require(path.is_file(), f"missing voice file: {filename}")
        require(path.with_name(f"{filename}.import").is_file(), f"missing Godot import sidecar: {filename}")
    voice_root = PROJECT / "audio/sfx/characters/shin_getter/voices"
    require(not (voice_root / "musashi_open_get.wav").exists(), "issue#10 must not retain Musashi's Open Get voice")
    require(not (voice_root / "musashi_open_get.wav.import").exists(), "issue#10 must not retain Musashi's Open Get import")
    benkei_import = read(voice_root / "benkei_open_get.wav.import")
    require('source_file="res://audio/sfx/characters/shin_getter/voices/benkei_open_get.wav"' in benkei_import,
            "Benkei Open Get import must point to its renamed source")
    require("benkei_open_get.wav-fd2c371e41ad6859b6a943a944786c09.sample" in benkei_import,
            "Benkei Open Get import must use the renamed source hash")

    for action in FUSION_ACTIONS:
        sheet = PROJECT / "images/characters/shin_getter/forms" / action / "sprite_sheet.png"
        sidecar = sheet.with_name("sprite_sheet.png.import")
        require(sidecar.is_file(), f"missing Godot import sidecar: {sidecar}")
        sidecar_text = read(sidecar)
        require('"vram_texture": false' in sidecar_text, f"{sidecar} must not use VRAM compression")
        require("compress/mode=1" in sidecar_text and "compress/lossy_quality=0.75" in sidecar_text,
                f"{sidecar} has the wrong compression policy")


def main() -> None:
    check_fusion_sheets()
    check_code_wiring()
    check_localization_and_assets()
    print("issue#10 validation passed")


if __name__ == "__main__":
    main()
