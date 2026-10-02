#!/usr/bin/env python3
"""Source/resource gate only. Does not build, run Godot, or prove visual timing."""

import hashlib
import json
from pathlib import Path

from build_character_sprite_sheets import FRAME_COUNTS, load_frame_manifest, verify_sheet
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


def resources_and_clock() -> None:
    source_root = ROOT.parent / "art_sources/characters/shin_getter/forms"
    manifest = load_frame_manifest(source_root / "frame_manifest.txt")
    require(FRAME_COUNTS[ACTION] == 34, "Shining source count is 34")
    audit = json.loads(read(ROOT / "tools/b130-shining-material.json"))
    require(audit["frame_count"] == 34, "Audit frame count")
    for frame in audit["frames"]:
        path = source_root / ACTION / Path(frame["path"]).name
        require(hashlib.sha256(path.read_bytes()).hexdigest() == frame["sha256"], "Source frame hash")
        require(not frame["empty"], "No empty source frame")
    verify_sheet(ACTION, source_root / ACTION, ROOT / "images/characters/shin_getter/forms" / ACTION,
                 34, manifest[ACTION])
    clock = read(ROOT / "src/Nodes/Combat/NShinGetterShiningSparkSequence.cs")
    ownership_contract(clock)
    for variant in (
        clock.replace("|| IsControlling(sprite)", "", 1),
        clock.replace("sprite.SetMeta(ManualOwnerMeta, sequence.GetInstanceId());", "", 1),
        clock.replace("if (_closed || !OwnsSprite()) return;", "if (!OwnsSprite()) return;", 1),
    ):
        try:
            ownership_contract(variant)
        except AssertionError:
            continue
        raise AssertionError("Broken ownership source variant unexpectedly passed")
    for name, phase in (("DiscardComplete", "discard_complete"), ("ChargeHold", "charge_hold"),
                        ("DashPeak", "dash_peak"), ("EndHold", "end_hold")):
        index = audit["phase_suggestions_0_based"][phase]["sequence_index"]
        has(clock, f"private const int {name} = {index};", "Runtime phase must match reviewed source")
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
    energy = block(clock, "private void UpdateEnergy()")
    has(energy, "_sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame)",
        "Energy silhouette follows the actual same-phase sprite frame")
    imports = read(ROOT / f"images/characters/shin_getter/forms/{ACTION}/sprite_sheet.png.import")
    for fragment in ('"vram_texture": false', "compress/mode=0", "mipmaps/generate=false"):
        require(fragment in imports, "New action uses lossless, no mipmap, no VRAM")
    base = read(ROOT / "src/Models/Cards/ShinGetterCardBase.cs")
    has(base, '["SGC_ShiningSpark"] = "ShiningSpark"', "Only Dragon requests the new action")
    machine = read(ROOT / "src/Nodes/Combat/NShinGetterSpriteAnimationStateMachine.cs")
    has(machine, '"ShiningSpark" => NShinGetterSpriteSequence.ShiningSparkAnimationName', "Trigger registration")
    sequence = read(ROOT / "src/Nodes/Combat/NShinGetterSpriteSequence.cs")
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
    print("B1.3.0 ultimate source/resource gate passed (8 negative source variants rejected)")
    print("Runtime rendering, Harmony binding, multiplayer and gameplay timing NOT executed.")


if __name__ == "__main__":
    main()
