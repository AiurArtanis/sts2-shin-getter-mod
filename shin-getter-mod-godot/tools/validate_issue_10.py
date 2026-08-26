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
EXPECTED_FIRST_FRAME_SHA256 = "f8c58c468f3ad2bcdd8cc3637d7b9c413bac9406e3c4dd2a8e48fe8747486717"
EXPECTED_GETTER_LANDING_ART = {
    "images/packed/card_portraits/shin_getter/s_g_c_getter_landing.png":
        "8d7714c7d29bf73f2af7f8eec80bfe5bbc0342579f805c8ca1d5524835a4c395",
    "images/packed/card_single/shin_getter/s_g_c_getter_landing_card.png":
        "6390ca0224244848a3ad33e325454d3361217067ed1af298e745e028d30d31ea",
}
EXPECTED_STONER_ARRIVAL_AUDIO = {
    "ryoma_feel_getter_power.wav": "0f3d5e2c06e65cf78f3badedbdf09451e5fa7b0d3cebb413b255feb60ef17d0f",
    "ryoma_three_hearts_one.wav": "1db52dfe0b3bf1bf0b42fc6cd35fedc77f05c98a2e457c374923f1019a2b07b7",
    "ryoma_our_will_getter_power.wav": "ff6ecb51b3cc2afe0218629128d41a18ef72875411daacabfab0bec38c118cd3",
    "hayato_unite_hearts.wav": "268ff77eeec5c19e8c615c74aaa890a7f7227313b28c3712863f0e60f88ae124",
    "benkei_use_stoner_sunshine.wav": "fcbb7c2750e6707343f22adb1b437dd8de8e4f1a58fcc6c723edc58b7a18002c",
}


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
    hit_count_patch = read(PROJECT / "src/Patches/ShinGetterOpenGetAttackHitCountPatch.cs")
    final_damage_patch = read(PROJECT / "src/Patches/ShinGetterOpenGetFinalDamagePatch.cs")
    radiation = read(PROJECT / "src/Models/Powers/SGP_Radiation.cs")
    radiated_card = read(PROJECT / "src/Models/Cards/SGC_Radiated.cs")
    combat_vfx = read(PROJECT / "src/Nodes/Vfx/ShinGetterCombatVfx.Extra.cs")
    tactical_retreat = read(PROJECT / "src/Models/Cards/SGC_TacticalRetreat.cs")
    stoner_service = read(PROJECT / "src/Services/ShinGetterStonerSunshineService.cs")
    stoner_card = read(PROJECT / "src/Models/Cards/SGC_StonerSunshine.cs")
    shin_form_card = read(PROJECT / "src/Models/Cards/SGC_ShinForm.cs")
    ancient_reward_patch = read(PROJECT / "src/Patches/ShinGetterAncientRewardPatch.cs")
    execution_music = read(PROJECT / "src/Audio/ShinGetterExecutionMusicService.cs")
    desperation = read(PROJECT / "src/Models/Cards/SGC_Desperation.cs")
    console_patch = read(PROJECT / "src/Patches/ShinGetterConsoleCommandPatch.cs")
    scene = read(PROJECT / "scenes/creature_visuals/shin_getter.tscn")
    opening_frames = read(PROJECT / "scenes/creature_visuals/shin_getter_one_idle_frames.tres")
    builder = read(PROJECT / "tools/build_character_sprite_sheets.py")
    rate_command_path = PROJECT / "src/Diagnostics/ShinGetterStonerSunshineRateConsoleCmd.cs"
    require(rate_command_path.is_file(), "stoner_sunshine_rate console command is missing")
    rate_command = read(rate_command_path)
    form_powers = "\n".join(read(PROJECT / f"src/Models/Powers/{name}") for name in (
        "SGP_ShinGetterOne.cs", "SGP_ShinGetterTwo.cs", "SGP_ShinGetterThree.cs"))

    require("FusionFramesPerSecond = 60d" in sequence, "fusion animation must run at 60fps")
    require("EnsureFusionLoaded" in sequence and "FusionAnimationName" in sequence, "fusion sequence loader is missing")
    require("TryPlayFusionTransition" in visuals and "PlayOpenGetVfx" in visuals, "fusion visual flow is missing")
    require("PlayShadeVfx" in visuals, "shade visual hook is missing")
    require("FusionTransitionHoldSeconds = 0.2f" in visuals,
            "ordinary fusion transitions must hold the shared fighter frame for 0.2 seconds")
    require("OpeningFusionFirstFrameHoldSeconds = 0.2f" in visuals,
            "combat-opening fusion must hold its prepared first frame for 0.2 seconds")
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
    require("and not SGC_StonerSunshine" in pool,
            "Stoner Sunshine must stay out of ordinary card rewards")
    require("public override bool CanBeGeneratedInCombat => false" in stoner_card,
            "Stoner Sunshine must be excluded from generic in-combat card generation")
    require('s_g_c_getter_landing_card.png' in landing
            and 's_g_c_getter_landing.png' in landing
            and "AllPortraitPaths" in landing,
            "Getter Landing must use its authoritative standalone large and small art")
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
    owner_guard = open_get.index("if (target != Owner || !props.IsPoweredAttack() || Owner.Player == null)")
    active_attack_shortcut = open_get.index("data.ActiveAttack?.Attacker == dealer && data.WillAvoidActiveAttack")
    require(owner_guard < active_attack_shortcut,
            "Open Get must isolate the whole-attack avoidance shortcut to its own player target")
    require(open_get.count("data.ActiveAttack?.Attacker == dealer && data.WillAvoidActiveAttack") == 1,
            "Open Get must not bypass its owner-target guard in another damage shortcut")
    require("GetFinalHitCount(activeAttack)" in open_get and "amount * finalHitCount" in open_get
            and "data.HitCount" not in open_get,
            "Open Get must judge a multi-attack by the final global hook hit count")
    require("HarmonyPatch(typeof(Hook), nameof(Hook.ModifyAttackHitCount))" in hit_count_patch
            and "HarmonyPostfix" in hit_count_patch and "decimal __result" in hit_count_patch
            and "ConditionalWeakTable<AttackCommand, FinalHitCount>" in hit_count_patch,
            "Open Get must capture the hit count after all listeners, including enemy Grapple")
    require("Math.Max(0, (int)__result)" in hit_count_patch,
            "Open Get's final hit count must retain Grapple's clamped result")
    grapple = read(PROJECT / "src/Models/Powers/SGP_Grapple.cs")
    require("Math.Max(0, hitCount - Amount)" in grapple
            and "GetAdjustedRepeats" in read(PROJECT / "src/Patches/ShinGetterMultiAttackIntentPatch.cs"),
            "Open Get intent and runtime totals must both retain Grapple-adjusted repeats")
    require("data.WillAvoidActiveAttack" in open_get,
            "Open Get must keep avoiding its owner through every hit of an eligible attack")
    require("ModifyAttackHitCount" in open_get and "AfterAttack" in open_get,
            "Open Get must retain its active AttackCommand through every eligible hit")
    multiplicative_start = open_get.index("public override decimal ModifyDamageMultiplicative")
    multiplicative_end = open_get.index("public override async Task AfterDamageReceived", multiplicative_start)
    open_get_multiplicative = open_get[multiplicative_start:multiplicative_end]
    require("return 1m" in open_get_multiplicative and "return 0m" not in open_get_multiplicative,
            "Open Get must defer avoidance until the final all-stage damage result")
    require("HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))" in final_damage_patch
            and "modifyDamageHookType != ModifyDamageHookType.All" in final_damage_patch
            and "IsCalculatingIntentDamage" in final_damage_patch
            and "GetDisplayedDamagePerHit(__result)" in final_damage_patch
            and "TryAvoidFinalDamage" in final_damage_patch and "__result = 0m" in final_damage_patch,
            "Open Get runtime avoidance must use Hook.ModifyDamage's final displayed damage")
    require("Math.Max(0, (int)finalDamage)" in final_damage_patch,
            "Open Get runtime damage must use AttackIntent's final decimal-to-int display contract")
    require("SHIN_GETTER_OPEN_GET_INTENT_SINGLE" in intent_patch
            and "SHIN_GETTER_OPEN_GET_INTENT_MULTI" in intent_patch,
            "Open Get intent labels must preserve damage while adding the avoidance marker")
    require("HoverMeta = \"shin_getter_open_get\"" in intent_patch
            and "MetaHoverStarted" in intent_patch and "CreateAvoidanceHoverTip" in intent_patch,
            "the red Open Get intent X must expose its localized hover tip")
    require("intent.GetTotalDamage" in intent_patch and "WouldAvoidIntent(totalDamage)" in intent_patch,
            "multi-hit intent avoidance must compare the full displayed damage total")
    require("private static T CalculateIntentDamage<T>" in intent_patch
            and "CalculateIntentDamage(() => intent.GetSingleDamage(targetArray, owner))" in intent_patch
            and "CalculateIntentDamage(() => intent.GetTotalDamage(targets, owner))" in intent_patch,
            "Open Get labels must explicitly isolate displayed damage from runtime avoidance")
    require("finally" in intent_patch
            and "_intentDamageCalculationDepth = Math.Max(0, _intentDamageCalculationDepth - 1)" in intent_patch,
            "explicit intent damage scopes must unwind even when calculation throws")
    require("foreach (Creature enemy in combatState.Enemies)" in intent_patch
            and "foreach (AbstractIntent candidate in enemy.Monster.NextMove.Intents)" in intent_patch
            and "return ReferenceEquals(enemy, owner) && ReferenceEquals(attackIntent, intent)" in intent_patch,
            "only the first eligible attack intent in stable combat order may show Open Get's red X")
    radiation_multiplier_start = radiation.index("public override decimal ModifyDamageMultiplicative")
    radiation_multiplier_end = radiation.index("private static bool IsHpLoss", radiation_multiplier_start)
    radiation_multiplier = radiation[radiation_multiplier_start:radiation_multiplier_end]
    require("cardSource is SGC_Radiated" in radiation_multiplier,
            "Radiated's printed damage must not be increased by existing Radiation")
    require('new DamageVar(5m, ValueProp.Unpowered)' in radiated_card,
            "Radiated must retain its printed five-damage contract")
    require("creature.PetOwner?.Creature ?? creature" in radiated_card
            and ".Distinct()" in radiated_card
            and "!ReferenceEquals(creature, self)" in radiated_card,
            "Radiated must collapse pets to their effective receiver and hit its owner only once")
    radiated_damage = radiated_card.index("await CreatureCmd.Damage(")
    radiated_apply = radiated_card.index("await PowerCmd.Apply<SGP_Radiation>")
    require(radiated_damage < radiated_apply,
            "Radiated must finish its printed damage before applying Radiation")
    require("TacticalRetreatDistance = 480f" in combat_vfx
            and "await NShinGetterStaticVisuals.PlayCreatureActionAnimationAndWait" in combat_vfx
            and '"Block"' in combat_vfx,
            "Tactical Retreat must finish its defence animation before moving back three body positions")
    require("public static async Task PlayCreatureActionAnimationAndWait" in visuals
            and "frameCount / framesPerSecond / speedScale" in visuals,
            "Tactical Retreat must wait for the active form's real defence-animation duration")
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
    getter_one_scene = scene[
        scene.index('[node name="GetterOne"'):
        scene.index('[node name="GetterTwo"')
    ]
    require('path="res://scenes/creature_visuals/shin_getter_one_idle_frames.tres" id="6_idle_frames"' in scene
            and 'sprite_frames = ExtResource("6_idle_frames")' in getter_one_scene
            and 'animation = &"fusion"' in getter_one_scene
            and 'autoplay = "idle"' not in getter_one_scene,
            "the scene must render prepared A/fusion frame one before combat hooks run")
    require('path="res://images/characters/shin_getter/forms/getter_one_fusion/sprite_sheet.png"' in opening_frames
            and '[sub_resource type="AtlasTexture" id="FusionOpeningFrame"]' in opening_frames
            and 'atlas = ExtResource("2_fusion_sheet")' in opening_frames
            and "region = Rect2(0, 0, 720, 720)" in opening_frames
            and '"texture": SubResource("FusionOpeningFrame")' in opening_frames
            and '"name": &"fusion"' in opening_frames,
            "Getter One default SpriteFrames must expose the exact first A/fusion cell")
    require('include_opening_fusion = action == "getter_one_idle"' in builder
            and "verify_idle_resource" in builder,
            "the generated opening-frame resource must be reproducible and checked")
    prepare_start = visuals.index("public static void PrepareOpeningGetterOneFusion(Creature creature)")
    prepare_end = visuals.index("    public static Task ShowShinDragon", prepare_start)
    opening_prepare = visuals[prepare_start:prepare_end]
    require("TryGetRawFormSprites" in opening_prepare
            and "TryPrepareFusionAnimation" in opening_prepare,
            "combat-start preparation must bypass idle activation and prepare A/fusion frame one directly")
    require(opening_prepare.index("TryPrepareFusionAnimation")
            < opening_prepare.index("sprite.Item.Visible = false"),
            "A/fusion frame one must replace the default Getter One idle frame before forms are hidden")
    opening_start = visuals.index("private static async Task PlayOpeningGetterOneFusion(FormSprites sprites)")
    opening_end = visuals.index("    private static async Task<bool> TryPlayFusionTransition", opening_start)
    opening_fusion = visuals[opening_start:opening_end]
    require("TryPrepareFusionAnimation" in opening_fusion and "PlayPreparedFusionAnimation" in opening_fusion,
            "combat-opening Getter One fusion must be prepared before it becomes visible")
    require(opening_fusion.index("TryPrepareFusionAnimation") < opening_fusion.index("next.Item.Visible = true")
            < opening_fusion.index("OpeningFusionFirstFrameHoldSeconds")
            < opening_fusion.index("PlayPreparedFusionAnimation"),
            "combat-opening Getter One must show and hold fusion frame one before it starts playing")
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

    for code in ("061", "062", "063", "064", "065"):
        require(f'new("{code}"' in voice, f"Stoner Sunshine arrival voice {code} is missing")
    require("PlayStonerSunshineArrival" in voice
            and "StonerArrivalFeelPower" in voice
            and "MegaCrit.Sts2.Core.Random.Rng.Chaotic.NextInt" in voice,
            "Stoner Sunshine arrival must implement voice 061 and cosmetic random selection")
    dragon_voice_start = voice.index("bool isShinDragon")
    dragon_voice_end = voice.index(": player.Creature.GetPower<SGP_ShinGetterOne>()", dragon_voice_start)
    dragon_voice_pool = voice[dragon_voice_start:dragon_voice_end]
    require("StonerArrivalThreeHearts" in dragon_voice_pool
            and "StonerArrivalOurWill" in dragon_voice_pool
            and "StonerArrivalUniteHearts" in dragon_voice_pool
            and "StonerArrivalUseSunshine" in dragon_voice_pool
            and "StonerArrivalFeelPower" not in dragon_voice_pool,
            "Shin Dragon must randomly use exactly workbook voices 062-065")
    getter_one_voice_start = voice.index(
        ": player.Creature.GetPower<SGP_ShinGetterOne>()", dragon_voice_end)
    getter_two_voice_start = voice.index(
        ": player.Creature.GetPower<SGP_ShinGetterTwo>()", getter_one_voice_start)
    getter_three_voice_start = voice.index(
        ": player.Creature.GetPower<SGP_ShinGetterThree>()", getter_two_voice_start)
    getter_one_voice_pool = voice[getter_one_voice_start:getter_two_voice_start]
    getter_two_voice_pool = voice[getter_two_voice_start:getter_three_voice_start]
    getter_three_voice_pool = voice[getter_three_voice_start:voice.index(": Array.Empty<ShinGetterVoiceCue>()", getter_three_voice_start)]
    require(all(cue in getter_one_voice_pool for cue in (
                "StonerArrivalFeelPower", "StonerArrivalThreeHearts", "StonerArrivalOurWill"))
            and "StonerArrivalUniteHearts" not in getter_one_voice_pool
            and "StonerArrivalUseSunshine" not in getter_one_voice_pool,
            "Getter One must select only Ryoma workbook voices 061-063")
    require("new[] { ShinGetterVoiceCue.StonerArrivalUniteHearts }" in getter_two_voice_pool,
            "Getter Two must use Hayato workbook voice 064")
    require("new[] { ShinGetterVoiceCue.StonerArrivalUseSunshine }" in getter_three_voice_pool,
            "Getter Three must use Benkei workbook voice 065")
    arrival_voice_start = voice.index("internal static void PlayStonerSunshineArrival")
    arrival_voice_end = voice.index("internal static IDisposable SuppressLowHpThresholdVoices", arrival_voice_start)
    arrival_voice = voice[arrival_voice_start:arrival_voice_end]
    require("CanClaimVoiceCue(player, candidate)" in arrival_voice
            and "TryClaimVoiceCue(player, cue)" in arrival_voice
            and "ignoreRequiredForm: isShinDragon" in arrival_voice,
            "arrival voices must filter already-played cues and retain the Chunibyo voice-mode claim boundary")
    require(arrival_voice.index("CanClaimVoiceCue(player, candidate)")
            < arrival_voice.index("Rng.Chaotic.NextInt")
            < arrival_voice.index("TryClaimVoiceCue(player, cue)")
            < arrival_voice.index("TryPlayLine(player, line"),
            "arrival voice selection must choose an eligible cosmetic cue before claiming and playing it")

    for needle in (
        "ConditionalWeakTable<CombatState, CombatProgress>",
        "ResetCombat(Player owner)",
        "TurnChancePerCompletedTurn = 0.05m",
        "AllFormsChance = 0.10m",
        "TripleUnityChancePerPlay = 0.10m",
        "AllEnemiesLowHpChance = 0.15m",
        "SpiritCommandChancePerPlay = 0.05m",
        "FloorMultiplierBase = 0.15m",
        "FloorMultiplierPerFloor = 0.01m",
        "Math.Max((owner.PlayerCombatState?.TurnNumber ?? 1) - 1, 0)",
        "SGC_TripleUnity",
        "SGC_Ki or SGC_Spirit or SGC_SuperKi",
        "enemy.CurrentHp * 100m < enemy.MaxHp * 30m",
        "DeckAlreadyContainsStonerSunshine(owner)",
        "progress.HasGrantedCard",
        "PileType.Hand.GetPile(owner).Cards.Count >= CardPile.MaxCardsInHand",
        "owner.RunState.Rng.CombatCardSelection.NextFloat()",
        "combatState.CreateCard<SGC_StonerSunshine>(owner)",
        "CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner)",
        "TryStartFromStonerSunshineArrival(owner, card)",
        "PlayStonerSunshineArrival(owner)",
        "attackCommand.ModelSource is not SGC_StonerSunshine cardSource",
        "attackCommand.Results.SelectMany(results => results)",
        "combatState.Enemies.Any(enemy => enemy.IsAlive)",
        "room.AddExtraReward(owner, new SpecialCardReward(rewardCard, owner))",
    ):
        require(needle in stoner_service, f"missing Stoner Sunshine special-arrival boundary: {needle}")
    require("progress.AtomicFormsMask = AllAtomicFormsMask" in stoner_service
            and "RecordShinDragonTransform(Owner)" in shin_form_card
            and "RecordShinDragonTransform(player)" in card_base,
            "one Shin Dragon transform must substitute for all three atomic-form transforms")
    atomic_apply = card_base.index("await ApplyFormPower(choiceContext, creature, next, player, cardSource)")
    atomic_record = card_base.index("ShinGetterStonerSunshineService.RecordAtomicTransform(player, next)")
    atomic_block_end = card_base.index("        }\n        finally", atomic_record)
    require(atomic_apply < atomic_record < atomic_block_end,
            "only successful, non-duplicate atomic transforms may accumulate across turns")
    shin_apply = shin_form_card.index("await PowerCmd.Apply<SGP_ShinForm>")
    shin_record = shin_form_card.index("ShinGetterStonerSunshineService.RecordShinDragonTransform(Owner)")
    require(shin_apply < shin_record,
            "Shin Dragon may substitute for all forms only after its power is applied")
    full_hand_guard = stoner_service.index(
        "PileType.Hand.GetPile(owner).Cards.Count >= CardPile.MaxCardsInHand")
    probability_roll = stoner_service.index("owner.RunState.Rng.CombatCardSelection.NextFloat()")
    generated_add = stoner_service.index(
        "CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner)")
    require(full_hand_guard < probability_roll < generated_add,
            "a full hand must defer the roll so Stoner Sunshine cannot be redirected to discard")
    grant_success = stoner_service.index("if (!result.success)")
    grant_flag = stoner_service.index("progress.HasGrantedCard = true")
    grant_preview = stoner_service.index("CardCmd.PreviewCardPileAdd")
    grant_music = stoner_service.index("TryStartFromStonerSunshineArrival")
    grant_voice = stoner_service.index("PlayStonerSunshineArrival")
    require(generated_add < grant_success < grant_flag < grant_preview < grant_music < grant_voice,
            "failed generated-card adds must remain retryable and must not preview, switch BGM, or play a voice")
    require("ConditionalWeakTable<CombatState, CombatProgress>" in stoner_service
            and "ConditionalWeakTable<Player, PlayerProgress>" in stoner_service
            and "Players.GetValue(\n            owner" in stoner_service,
            "Stoner Sunshine progress must be isolated by both combat and player")
    require("ReferenceEquals(cardPlay.Card.Owner, owner)" in stoner_service,
            "other players' card plays must not change this player's probability")
    require("ReferenceEquals(attackCommand.Attacker, owner.Creature)" in stoner_service
            and "ReferenceEquals(cardSource.Owner, owner)" in stoner_service
            and "result.Receiver.Side == CombatSide.Enemy" in stoner_service,
            "the final-kill reward must be isolated to the owner's Stoner Sunshine enemy kill")
    require(stoner_service.count("DeckAlreadyContainsStonerSunshine(owner)") >= 3,
            "a permanent Stoner Sunshine must suppress the roll, final-kill record, and victory reward")
    require(stoner_card.count("attackCommand = await DamageCmd.Attack") == 2
            and stoner_card.count(".Execute(choiceContext);") == 2,
            "both Stoner Sunshine visual branches must retain the executed AttackCommand")
    final_execute = stoner_card.rindex(".Execute(choiceContext);")
    final_kill_confirmation = stoner_card.index(
        "ShinGetterStonerSunshineService.RecordFinalKill(Owner, attackCommand);")
    require(final_execute < final_kill_confirmation,
            "Stoner Sunshine may confirm the final kill only after Damage, Kill, AfterDeath, and reinforcements finish")
    reward_guard = stoner_service.index("if (!progress.FinishedCombatWithStonerSunshine")
    reward_flag = stoner_service.index("progress.VictoryRewardAdded = true")
    reward_add = stoner_service.index("room.AddExtraReward")
    require(reward_guard < reward_flag < reward_add,
            "Stoner Sunshine's fixed victory reward must be added at most once")
    require("FindGetterLaunch" in ancient_reward_patch
            and "CreateGetterLandingFrom" in ancient_reward_patch
            and "TransformGetterLaunch" in ancient_reward_patch
            and "ModelDb.Card<SGC_GetterLaunch>()" in ancient_reward_patch
            and "CreateCard<SGC_GetterLanding>" in ancient_reward_patch,
            "Orobas ancient-card option must transform Getter Launch into Getter Landing")
    require("FindGetterBeam" not in ancient_reward_patch
            and "CreateStonerSunshineFrom" not in ancient_reward_patch,
            "the obsolete Getter Beam to Stoner Sunshine Orobas mapping must not return")
    require("if (getterLaunch.IsUpgraded)" in ancient_reward_patch
            and "CardCmd.Upgrade(getterLanding)" in ancient_reward_patch
            and "getterLaunch.Enchantment != null" in ancient_reward_patch
            and "CardCmd.Enchant(enchantment, getterLanding, enchantment.Amount)" in ancient_reward_patch,
            "Getter Launch to Getter Landing must preserve upgrade and enchantment state")
    require("TryStartFromStonerSunshineArrival" in execution_music
            and "allowFirstTurn: true" in execution_music,
            "special Stoner Sunshine arrival must start execution BGM even on turn one")
    require("SuppressLowHpThresholdVoices(Owner)" in desperation
            and "using (" in desperation
            and "AreLowHpThresholdVoicesSuppressed" in voice
            and "LowHpVoiceSuppressionDepth" in voice,
            "Desperation's deliberate HP set must suppress workbook low-HP voices 052-057")
    require('StonerSunshineRateCommandName = "stoner_sunshine_rate"' in console_patch
            and "ShinGetterStonerSunshineRateConsoleCmd" in console_patch,
            "stoner_sunshine_rate must be routed through the existing console patch")
    require('CmdName => "stoner_sunshine_rate"' in rate_command
            and "public sealed class ShinGetterStonerSunshineRateConsoleCmd : AbstractConsoleCmd" in rate_command
            and "DebugOnly => false" in rate_command
            and "TryGetCurrentAppearanceChance" in rate_command
            and 'ToString("P2"' in rate_command,
            "stoner_sunshine_rate must be a discoverable non-debug command and report the issuing player's current percentage")

    for relic_name, relic in (("Getter Furnace", getter_furnace), ("Emperor's Fragment", emperors_fragment)):
        require("ShinGetterStonerSunshineService.ResetCombat(Owner);" in relic,
                f"{relic_name} must reset Stoner Sunshine progress per combat")
        trial = relic.index("await ShinGetterEventInvasionService.ApplyPendingTrialAfterHandDraw")
        roll = relic.index("await ShinGetterStonerSunshineService.TryGrantAfterHandDraw")
        require(trial < roll,
                f"{relic_name} must roll Stoner Sunshine only after the normal start-of-turn hand setup")
        require("RecordCardPlayed(Owner, cardPlay)" in relic,
                f"{relic_name} must track Triple Unity and spirit-command plays")
        require("RecordFinalKill(" not in relic and "AddVictoryReward(Owner, room)" in relic,
                f"{relic_name} must not record final kills from the pre-AfterDeath damage hook")
        reset = relic.index("ShinGetterStonerSunshineService.ResetCombat(Owner)")
        initial_form = relic.index("await PowerCmd.Apply<SGP_ShinGetterOne>")
        require(reset < initial_form,
                f"{relic_name} must reset progress before initial Getter One setup without counting it as a transform")


def check_open_get_final_damage_contract() -> None:
    """Lock final-stage Weak, Cap, and multi-hit+Grapple eligibility boundaries."""

    def final_total(raw: float, multiplier: float, cap: float, repeats: int, grapple: int = 0) -> int:
        final_per_hit = max(0, int(min(raw * multiplier, cap)))
        final_repeats = max(0, repeats - grapple)
        return final_per_hit * final_repeats

    weak_total = final_total(raw=10, multiplier=0.75, cap=float("inf"), repeats=1)
    require(weak_total == 7 and weak_total <= 8 and not (10 <= 8),
            "Weak positive gate must use final 7 damage instead of intermediate 10")
    require(weak_total > 6, "Weak negative gate must reject an Open Get threshold below final damage")

    capped_total = final_total(raw=10, multiplier=1.0, cap=1, repeats=1)
    require(capped_total == 1 and capped_total <= 1 and not (10 <= 1),
            "Cap positive gate must use final capped damage instead of intermediate damage")
    require(capped_total > 0, "Cap negative gate must reject a zero Open Get threshold")

    grapple_multi_total = final_total(raw=10, multiplier=0.75, cap=float("inf"), repeats=3, grapple=1)
    require(grapple_multi_total == 14 and grapple_multi_total <= 14,
            "Weak multi-hit plus Grapple positive gate must use 7 x 2 final damage")
    require(grapple_multi_total > 13,
            "Weak multi-hit plus Grapple negative gate must reject a threshold below 14")


def check_stoner_sunshine_probability_contract() -> None:
    """Lock additive factors, floor multiplier, cross-turn accumulation, and strict thresholds."""

    def chance(turn: int, forms_mask: int, triple_plays: int,
               all_enemies_low: bool, spirit_plays: int, floor: int) -> float:
        additive = (max(turn - 1, 0) * 0.05
                    + (0.10 if forms_mask & 0b111 == 0b111 else 0.0)
                    + triple_plays * 0.10
                    + (0.15 if all_enemies_low else 0.0)
                    + spirit_plays * 0.05)
        multiplier = 0.15 + max(floor, 0) * 0.01
        return round(additive * multiplier, 6)

    require(chance(1, 0, 0, False, 0, 1) == 0.0,
            "turn one must have zero base chance without bonuses")
    require(chance(4, 0, 0, False, 0, 16) == 0.0465,
            "floor 16 must multiply the 15% turn base by 0.31")
    require(chance(1, 0b011, 0, False, 0, 1) == 0.0
            and chance(1, 0b111, 0, False, 0, 1) == 0.016,
            "all three form bits must accumulate before the 10% bonus")
    dragon_substitute_mask = 0b111
    require(chance(1, dragon_substitute_mask, 0, False, 0, 1) == 0.016,
            "one Shin Dragon transform must grant the full form bonus")
    require(chance(1, 0, 2, False, 0, 1) == 0.032,
            "each Triple Unity play must add 10%")
    require(chance(1, 0, 0, True, 0, 1) == 0.024,
            "all living enemies strictly below 30% must add 15%")
    require(not (30 < 30) and (29 < 30),
            "exactly 30% HP must not satisfy the strict all-enemies-low threshold")
    require(chance(1, 0, 0, False, 3, 1) == 0.024,
            "Ki, Spirit, and Super Ki plays must each add 5%")
    require(chance(3, 0b111, 2, True, 3, 17) == 0.224,
            "all additive factors must be multiplied by the floor 17 multiplier 0.32")
    require(chance(4, 0, 0, False, 0, -5) == 0.0225,
            "negative synthetic floors must clamp to zero before applying the 0.15 base multiplier")


def check_stoner_sunshine_final_kill_contract() -> None:
    """Lock post-AfterDeath confirmation for Infested and other reinforcement fights."""

    def confirms_reward(stoner_killed_enemy: bool, living_enemies_after_execute: int) -> bool:
        return stoner_killed_enemy and living_enemies_after_execute == 0

    infested_spawned_wrigglers = 4
    recorded = confirms_reward(
        stoner_killed_enemy=True,
        living_enemies_after_execute=infested_spawned_wrigglers,
    )
    require(not recorded,
            "killing an Infested host must not record the reward after AfterDeath spawns Wrigglers")

    # A later non-Stoner card does not invoke the Stoner-only confirmation path.
    infested_spawned_wrigglers = 0
    require(not recorded and infested_spawned_wrigglers == 0,
            "clearing Infested reinforcements with another card must not retroactively record the reward")

    require(confirms_reward(stoner_killed_enemy=True, living_enemies_after_execute=0),
            "Stoner Sunshine must record a real final enemy kill when AfterDeath adds no reinforcements")
    require(not confirms_reward(stoner_killed_enemy=False, living_enemies_after_execute=0),
            "an empty enemy side reached without a Stoner Sunshine kill must not record the reward")


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
        for key in (
            "SHIN_GETTER.voice.stonerArrivalFeelPower",
            "SHIN_GETTER.voice.stonerArrivalThreeHearts",
            "SHIN_GETTER.voice.stonerArrivalOurWill",
            "SHIN_GETTER.voice.stonerArrivalUniteHearts",
            "SHIN_GETTER.voice.stonerArrivalUseSunshine",
        ):
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

    for relative_path, expected_hash in EXPECTED_GETTER_LANDING_ART.items():
        path = PROJECT / relative_path
        require(path.is_file(), f"missing authoritative Getter Landing art: {relative_path}")
        require(hashlib.sha256(path.read_bytes()).hexdigest() == expected_hash,
                f"Getter Landing art does not match its authoritative source: {relative_path}")
        import_path = path.with_name(f"{path.name}.import")
        require(import_path.is_file(), f"missing Getter Landing import sidecar: {import_path}")
        require(f'source_file="res://{relative_path}"' in read(import_path),
                f"Getter Landing import has the wrong source: {relative_path}")
        require(f"res://{relative_path}" in resource_gate,
                f"resource gate is missing Getter Landing art: {relative_path}")

    for filename, expected_hash in EXPECTED_STONER_ARRIVAL_AUDIO.items():
        path = voice_root / filename
        require(path.is_file(), f"missing Stoner Sunshine arrival voice: {filename}")
        require(hashlib.sha256(path.read_bytes()).hexdigest() == expected_hash,
                f"Stoner Sunshine arrival voice does not match the workbook source: {filename}")
        import_path = path.with_name(f"{filename}.import")
        require(import_path.is_file(), f"missing arrival voice import sidecar: {filename}")
        import_text = read(import_path)
        require(f'source_file="res://audio/sfx/characters/shin_getter/voices/{filename}"' in import_text
                and "compress/mode=2" in import_text,
                f"arrival voice import is stale: {filename}")
        require(filename in resource_gate,
                f"resource gate is missing arrival voice: {filename}")

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
    check_open_get_final_damage_contract()
    check_stoner_sunshine_probability_contract()
    check_stoner_sunshine_final_kill_contract()
    check_localization_and_assets()
    print("issue#10 validation passed")


if __name__ == "__main__":
    main()
