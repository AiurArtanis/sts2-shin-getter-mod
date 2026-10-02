#!/usr/bin/env python3
"""Source/resource gate only. Does not build, run Godot, or prove visual timing."""

import hashlib
import json
from pathlib import Path

from PIL import Image

from build_character_sprite_sheets import FRAME_COUNTS, load_frame_manifest, verify_sheet
from import_shining_spark_frames import STAGES, validate_maps, verify_import
from validate_b130_core import block, compact, has, ordered, read, require

ROOT = Path(__file__).resolve().parents[1]
ACTION = "shin_getter_dragon_shining_spark"


def shining_contract(source: str) -> None:
    require("ModifyCardPlayCount" not in source, "Dragon reward must not replay this card")
    play = block(source, "protected override async Task OnPlay(")
    ordered(play, ["if (Owner.Creature.HasPower<SGP_ShinForm>()) "
                   "await PowerCmd.Apply<SGP_Ki>(choiceContext, Owner.Creature, 3m, Owner.Creature, this);",
                   "PowerCmd.Apply<VulnerablePower>", "PowerCmd.Apply<FrailPower>",
                   "NShinGetterShiningSparkSequence.TryCreate(Owner.Creature, cardPlay.Target)",
                   "await sequence.PlayToImpact(", "await DamageCmd.Attack(DynamicVars.Damage.BaseValue)",
                   'if (ki > 0 && Owner.Creature.CombatState is { } combatState)',
                   '.TargetingRandomOpponents(combatState)',
                   "await sequence.Recover();", "finally", "sequence?.Close();"],
            "Shining effects/impact/damage/ki/recovery/cleanup order")
    require("BeforeDamage(" not in play, "Hit FX must not precede the awaited impact phase")
    has(play, "if (sequence != null) followup.WithNoAttackerAnim();",
        "Ki follow-ups must not restart the axe-carrying ordinary animation")
    for fragment in ("new DamageVar(11m, ValueProp.Move)", 'new DynamicVar("KiDamage", 6m)',
                     'base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)',
                     'DynamicVars.Damage.UpgradeValueBy(3m)', 'DynamicVars["KiDamage"].UpgradeValueBy(3m)'):
        has(source, fragment, "Shining values remain 11/14 + 6/9 and 2 energy")


def star_contract(source: str) -> None:
    play = block(source, "protected override async Task OnPlay(")
    ordered(play, ["CardSelectCmd.FromCombatPile", "Math.Min(selected.Sum(SumOriginalCardValues), 50m)",
                   "foreach (var card in selected)", "await CardCmd.Exhaust(choiceContext, card);",
                   "ShinGetterCombatVfx.FlashHotBloodIcon(Owner.Creature);",
                   "if (HasForm(Owner, ShinGetterForm.Getter1))",
                   "await PowerCmd.Apply<SGP_HotBlood>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);",
                   "await PlayLegacyAnimationToImpact(cardPlay.Target);",
                   "await DamageCmd.Attack(DynamicVars.Damage.BaseValue + stackedValue)"],
            "Star selection/exhaust hooks/single Valor/impact/attack order")
    require(play.count("PowerCmd.Apply<SGP_HotBlood>") == 1, "Exactly one Valor application site")
    require("Vigor" not in source, "Old per-exhaust Vigor reward must be removed")
    require("BeforeDamage(" not in play, "Star hit FX must occur after fallback animation")
    has(source, "new DamageVar(22m, ValueProp.Move)", "Star base damage remains 22")
    has(source, "new CardsVar(1)", "Star base exhaust selection remains one")
    has(source, "DynamicVars.Cards.UpgradeValueBy(1m)", "Star upgrade selects two")
    has(source, "base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)", "Star cost remains 3")
    require("dedicated axe clips/weapon anchors are still missing" in source,
            "Missing Star materials must remain explicit, not fake dedicated animation")


def negative_contracts(shining: str, star: str) -> None:
    variants = [
        (shining_contract, shining.replace("if (Owner.Creature.HasPower<SGP_ShinForm>())", "if (true)", 1)),
        (shining_contract, shining.replace("Owner.Creature, 3m, Owner.Creature, this", "Owner.Creature, 6m, Owner.Creature, this", 1)),
        (shining_contract, shining.replace("await sequence.PlayToImpact(", "_ = sequence.PlayToImpact(", 1)),
        (star_contract, star.replace("Math.Min(selected.Sum(SumOriginalCardValues), 50m)",
                                    "selected.Sum(SumOriginalCardValues)", 1)),
        (star_contract, star.replace("if (HasForm(Owner, ShinGetterForm.Getter1))", "if (true)", 1)),
    ]
    for validator, variant in variants:
        try:
            validator(variant)
        except AssertionError:
            continue
        raise AssertionError("Negative source variant unexpectedly passed")


def ownership_contract(clock: str) -> None:
    create = block(clock, "public static NShinGetterShiningSparkSequence? TryCreate(")
    has(create, "|| IsControlling(sprite)", "Creation must reject duplicate visual ownership")
    has(create, "sprite.SetMeta(ManualOwnerMeta, sequence.GetInstanceId());", "Ownership token is per play")
    recover = block(clock, "public async Task Recover()")
    has(recover, "if (_closed || !OwnsSprite()) return;", "Late recovery cannot mutate a newer playback")
    stage = block(clock, "private Task Stage(")
    has(stage, "if (_closed) return Task.CompletedTask;", "Closed sequence cannot start a new stage")
    has(stage, "if (!OwnsSprite()", "Each stage checks ownership before its initial update")
    has(stage, "_sprite.Animation != NShinGetterSpriteSequence.ShiningSparkAnimationName",
        "Starting recovery cannot replace a newer action between damage and the next process tick")


def stage_clock_contract(clock: str) -> None:
    for name, index in (("DiscardComplete", 11), ("ChargeStart", 12), ("ChargeHold", 35),
                        ("RushStart", 36), ("DashPeak", 44), ("ImpactStart", 45), ("EndHold", 46)):
        has(clock, f"private const int {name} = {index};", "47-frame disjoint stage boundaries")
    create = block(clock, "public static NShinGetterShiningSparkSequence? TryCreate(")
    has(create, "sprite.Pause();", "Dedicated clock must own playback, not uniform SpriteFrames fps")
    play = block(clock, "public async Task PlayToImpact(")
    ordered(play, ["await Stage(0.4f, u => SetFrame(0, DiscardComplete, u));", "Task voice = intro();",
                   "await Stage(1f,", "SetFrame(ChargeStart, ChargeHold, u);", "await voice;",
                   "CombatManager.Instance.WaitForUnpause()", "_ = release();", "await Stage(0.28f,",
                   "SetFrame(RushStart, DashPeak, u);", "await Stage(0.08f,",
                   "SetFrame(ImpactStart, EndHold, u);"], "Dedicated timing/charge hold/Spark launch")
    recover = block(clock, "public async Task Recover()")
    ordered(recover, ["await Stage(0.35f,", "SetFrame(EndHold, EndHold, 0f);",
                      "_sprite.Position = start.Lerp(_origin, smooth);"], "Recover holds final airborne frame")
    has(clock, "_sprite.Frame = first + Math.Min(last - first, (int)Math.Floor((last - first + 1) * progress));",
        "Each stage frame gets equal duration; do not round endpoint interpolation")
    for forbidden in ("_shell", "ShaderMaterial", "DrawPolyline(points", "Position.Y", "Rotation ="):
        require(forbidden not in clock, "Baked jump/green energy needs no duplicate root jump or shell")


def resources_and_clock() -> None:
    source_root = ROOT.parent / "art_sources/characters/shin_getter/forms"
    manifest = load_frame_manifest(source_root / "frame_manifest.txt")
    require(FRAME_COUNTS[ACTION] == 47, "Shining source count is 47")
    verify_import()
    audit = json.loads(read(ROOT / "tools/b130-shining-material.json"))
    require(audit["frame_count"] == 47, "Audit frame count")
    for frame in audit["frames"]:
        path = source_root / ACTION / Path(frame["path"]).name
        require(hashlib.sha256(path.read_bytes()).hexdigest() == frame["sha256"], "Source frame hash")
        require(not frame["empty"], "No empty source frame")
    verify_sheet(ACTION, source_root / ACTION, ROOT / "images/characters/shin_getter/forms" / ACTION,
                 47, manifest[ACTION])
    with Image.open(ROOT / "images/characters/shin_getter/forms" / ACTION / "sprite_sheet.png") as sheet:
        require(sheet.mode == "RGBA" and sheet.size == (5760, 4320), "Tight 8x6 grid")
        for index, frame in enumerate(audit["frames"]):
            x, y = (index % 8)*720, (index // 8)*720
            with Image.open(source_root / ACTION / frame["path"]) as original:
                require(sheet.crop((x, y, x+720, y+720)).tobytes() == original.tobytes(), "Exact RGBA cell bytes")
        require(not any(sheet.crop((5040, 3600, 5760, 4320)).tobytes()), "One all-zero trailing cell")
    clock = read(ROOT / "src/Nodes/Combat/NShinGetterShiningSparkSequence.cs")
    stage_clock_contract(clock)
    ownership_contract(clock)
    for variant in (
        clock.replace("|| IsControlling(sprite)", "", 1),
        clock.replace("sprite.SetMeta(ManualOwnerMeta, sequence.GetInstanceId());", "", 1),
        clock.replace("if (_closed || !OwnsSprite()) return;", "if (!OwnsSprite()) return;", 1),
        clock.replace("_sprite.Animation != NShinGetterSpriteSequence.ShiningSparkAnimationName", "false", 1),
    ):
        try:
            ownership_contract(variant)
        except AssertionError:
            continue
        raise AssertionError("Broken ownership source variant unexpectedly passed")
    for old, new in (("DiscardComplete = 11", "DiscardComplete = 18"),
                     ("ChargeStart = 12", "ChargeStart = 11"),
                     ("ChargeHold = 35", "ChargeHold = 26"),
                     ("RushStart = 36", "RushStart = 35"),
                     ("ImpactStart = 45", "ImpactStart = 44"),
                     ("sprite.Pause();", ""),
                     ("_ = release();", "await release();"),
                     ("SetFrame(EndHold, EndHold, 0f);", ""),
                     ("Math.Floor((last - first + 1) * progress)", "Math.Round((last - first) * progress)")):
        require(old in clock, "Timing mutation must target real code")
        try:
            stage_clock_contract(clock.replace(old, new, 1))
        except AssertionError:
            continue
        raise AssertionError("Broken 47-frame timing variant unexpectedly passed")
    timing = json.loads(read(source_root / ACTION / "stage_timing_map.json"))
    sampled = json.loads(read(source_root / ACTION / "sampled_frame_map.json"))
    validate_maps(timing, sampled)
    for name, first, last, budget in STAGES[:-1]:
        for index in range(first, last+1):
            duration = budget/(last-first+1)
            for fraction in (0.001, 0.5, 0.999):
                progress = ((index-first)+fraction)/(last-first+1)
                actual = first+min(last-first, int((last-first+1)*progress))
                require(actual == index, f"{name}: equal frame hold matches timing map at {fraction}")
            require(abs(sampled[index]["duration_seconds"]-duration) < 1e-9, "Delivered equal hold timing")
    broken_timing = json.loads(json.dumps(timing))
    broken_timing["stages"][1]["first_index_0_based"] = 11
    try:
        validate_maps(broken_timing, sampled)
    except ValueError:
        pass
    else:
        raise AssertionError("Broken stage-map variant unexpectedly passed")
    process = block(clock, "public override void _Process(")
    ordered(process, ["CombatManager.Instance.IsPaused", "_stageTime += (float)delta;", "_stageUpdate(progress);"],
            "Visual clock freezes during combat pause")
    for fragment in ("IsActuallyVisible(_sprite)", "_owner.IsDead", "_sprite.Animation !=", "IsOverOrEnding", "Close();"):
        require(fragment in process, f"Animation interruption boundary: {fragment}")
    end = block(clock, "private void End()")
    ordered(end, ["_closed = true", "_sprite.Position = _origin", "_stageCompletion?.TrySetResult(false)",
                  "_ended.TrySetResult(false)"], "Cleanup restores position and releases all waiters")
    has(clock, "public override void _ExitTree() => End();", "Leaving tree releases tasks")
    has(clock, "_sprite.GetMeta(ManualOwnerMeta).AsUInt64() == GetInstanceId()",
        "Late cleanup must not restore a sprite now owned by another playback")
    ordered(end, ["_sprite.RemoveMeta(ManualOwnerMeta)", "_sprite.Position = _origin"],
            "Release manual ownership before returning to idle")
    visible = block(clock, "private static bool IsActuallyVisible(")
    for fragment in ("sprite.IsVisibleInTree()", "sprite.SelfModulate.A", "node.GetParent()", "item.Modulate.A"):
        require(fragment in visible, "Visibility includes parents and inherited alpha")
    for forbidden in ("CreateTween", "SignalName.Finished", "ownerNode.GlobalPosition =", "Random", "Rng"):
        require(forbidden not in clock, f"No hanging tween/logical displacement/game RNG: {forbidden}")
    has(block(clock, "private void UpdateTailHistory("), "GetFrameTexture(_sprite.Animation, _sprite.Frame)",
        "History snapshots preserve actual baked energy and pose")
    imports = read(ROOT / f"images/characters/shin_getter/forms/{ACTION}/sprite_sheet.png.import")
    for fragment in ('"vram_texture": false', "compress/mode=0", "mipmaps/generate=false"):
        require(fragment in imports, "New action uses lossless, no mipmap, no VRAM")
    base = read(ROOT / "src/Models/Cards/ShinGetterCardBase.cs")
    has(base, '["SGC_ShiningSpark"] = "ShiningSpark"', "Only Dragon requests the new action")
    machine = read(ROOT / "src/Nodes/Combat/NShinGetterSpriteAnimationStateMachine.cs")
    has(machine, '"ShiningSpark" => NShinGetterSpriteSequence.ShiningSparkAnimationName', "Trigger registration")
    sequence = read(ROOT / "src/Nodes/Combat/NShinGetterSpriteSequence.cs")
    has(sequence, "public const int ShiningSparkMaxFrames = 47;", "Runtime loader uses every delivered frame")
    require(sequence.count("ShiningSparkAnimationName,") >= 2, "New action is loaded on demand and released")
    has(block(sequence, "public static void EnsureShinDragonIdleLoaded("),
        "!NShinGetterShiningSparkSequence.IsControlling(sprite)", "Idle loading must preserve manual pause")
    visuals = read(ROOT / "src/Nodes/Combat/NShinGetterStaticVisuals.cs")
    has(visuals, "!NShinGetterShiningSparkSequence.IsControlling(shinDragonAnimation)",
        "Form lookup must preserve manual pause too")


def localization() -> None:
    markers = {"zhs": ("低于6", "热血", "先获得3", "不受"),
               "eng": ("less than 6", "Valor", "Gain 3", "does not increase"),
               "jpn": ("6未満", "熱血", "を3得る", "加算を受けない")}
    for language, values in markers.items():
        cards = json.loads(read(ROOT / f"ShinGetterMod/localization/{language}/cards.json"))
        powers = json.loads(read(ROOT / f"ShinGetterMod/localization/{language}/powers.json"))
        for card, marker in zip(("SHIFT_STRIKE", "STAR_SLASH", "SHINING_SPARK", "FIGHTING_SPIRIT"), values):
            require(marker in cards[f"S_G_C_{card}.description"], f"{language}: new {card} wording")
        require(values[-1] in powers["S_G_P_FIGHTING_SPIRIT.description"], "Counter tooltip agrees")
        star = cards["S_G_C_STAR_SLASH.description"]
        fighting = cards["S_G_C_FIGHTING_SPIRIT.description"]
        require("{CounterDamage:diff()}" in fighting and "{Damage:" not in fighting,
                "Counter card has a static, non-previewed 5/8 variable")
        require("[getter_ray]" in cards["S_G_C_SHINING_SPARK.description"], "Dragon reward uses Getter Ray color")
        require("{Vigor" not in star, "No stale Star Vigor variable")
        require("{Cards:diff()}" in star and "{Damage:diff()}" in star, "Star dynamic vars preserved")


def main() -> None:
    shining = read(ROOT / "src/Models/Cards/SGC_ShiningSpark.cs")
    star = read(ROOT / "src/Models/Cards/SGC_StarSlash.cs")
    shining_contract(shining)
    star_contract(star)
    negative_contracts(shining, star)
    resources_and_clock()
    localization()
    print("B1.3.0 ultimate source/resource gate passed (19 negative variants rejected; 47 exact RGBA cells)")
    print("Runtime rendering, Harmony binding, multiplayer and gameplay timing NOT executed.")


if __name__ == "__main__":
    main()
