#!/usr/bin/env python3
"""Source-backed B1.3.0 core gate; no build, game, or mirrored damage simulation.

Original source is mandatory, read-only evidence, NOT a runtime test substitute.
Checks cover final HP-loss provenance, block/overkill, first-hit-only branching,
death/escape, upgrade/Seal/ShinForm ordering, and Vigor-only counter exclusion.
Run: python tools/validate_b130_core.py --base-game-root E:/Work/SlaytheSpare2
"""

import argparse
from pathlib import Path
import re


ROOT = Path(__file__).resolve().parents[1]
CHECKS = 0


def require(condition: bool, message: str) -> None:
    global CHECKS
    CHECKS += 1
    if not condition:
        raise AssertionError(message)


def read(path: Path) -> str:
    require(path.is_file(), f"Required source missing: {path}")
    return path.read_text(encoding="utf-8-sig")


def compact(source: str) -> str:
    # These inspected contracts contain no comment delimiters in string literals.
    source = re.sub(r"//[^\n]*|/\*.*?\*/", "", source, flags=re.S)
    return re.sub(r"\s+", "", source)


def block(source: str, signature: str) -> str:
    require(source.count(signature) == 1, f"Ambiguous/missing contract: {signature}")
    start = source.index("{", source.index(signature) + len(signature))
    depth = 0
    # Ignore braces inside comments and C# string literals.
    for token in re.finditer(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"|[{}]',
                             source[start:], flags=re.S):
        if token.group() == "{":
            depth += 1
        elif token.group() == "}":
            depth -= 1
            if depth == 0:
                return source[start:start + token.end()]
    raise AssertionError(f"Unclosed contract: {signature}")


def has(source: str, fragment: str, label: str) -> None:
    require(compact(fragment) in compact(source), label)


def ordered(source: str, fragments: list[str], label: str) -> None:
    text = compact(source)
    position = 0
    for fragment in fragments:
        fragment = compact(fragment)
        index = text.find(fragment, position)
        require(index >= 0, f"{label}: {fragment}")
        position = index + len(fragment)


def validate_mod() -> None:
    shift = read(ROOT / "src/Models/Cards/SGC_ShiftStrike.cs")
    ki = read(ROOT / "src/Models/Cards/SGC_Ki.cs")
    spirit = read(ROOT / "src/Models/Powers/SGP_FightingSpirit.cs")
    play = block(shift, "protected override async Task OnPlay(")
    has(shift, "new DamageVar(6m, ValueProp.Move)", "Shift printed damage must be 6")
    has(shift, "new() { CardTag.Strike }", "Shift must keep Strike tag")
    has(shift, "base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)",
        "Shift cost/type/target must remain unchanged")
    attack = ('DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this)'
              '.Targeting(target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);')
    ordered(play, [
        'ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");',
        "var target = cardPlay.Target;",
        "var combatState = Owner.Creature.CombatState;",
        "if (IsUpgraded) await Transform(choiceContext, Owner, this);",
        "var firstAttack = await " + attack,
        "var firstResults = firstAttack.Results.Take(1).SelectMany(results => results)"
        ".Where(result => result.Receiver == target).ToArray();",
        "if (firstResults.Length == 0 || firstResults.Sum(result => result.UnblockedDamage) >= 6) return;",
        "await Transform(choiceContext, Owner, this);",
        "if (firstResults.Any(result => result.WasTargetKilled)"
        " || combatState == null || !combatState.ContainsCreature(target) || !target.IsHittable) return;",
        "await " + attack,
    ], "Shift first-hit/fixed-threshold/transform/death/escape order")
    require(compact(play).count(compact(attack)) == 2, "Exactly two direct attack sites")
    require(play.count("Transform(choiceContext, Owner, this)") == 2,
            "No unconditional trailing transform")
    require(compact(play).endswith(compact("await " + attack + "}")),
            "Follow-up must end play without recursive result processing")
    for forbidden in ("PowerCmd", "CurrentHp", "TotalDamage", "BlockedDamage", "OverkillDamage",
                      "OnPlay(", "WithHitCount", "TargetingRandom", "for (", "while ("):
        require(forbidden not in play, f"Shift must not use {forbidden}")
    require(compact(block(shift, "protected override void OnUpgrade()")) == "{}",
            "Shift upgrade must not change damage/threshold")

    for fragment in ("SpiritRequirement => 1", "new[] { CardKeyword.Exhaust }",
                     "new PowerVar<SGP_Ki>(1m)", "new PowerVar<VigorPower>(2m)",
                     "base(1, CardType.Skill, CardRarity.Common, TargetType.Self)"):
        has(ki, fragment, f"Ki preserved contract: {fragment}")
    require(compact(block(ki, "protected override void OnUpgrade()")) ==
            compact('{ DynamicVars["VigorPower"].UpgradeValueBy(2m); }'),
            "Ki upgrade must grant 4 Vigor, with no other upgrade changes")
    has(ki, 'PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, '
        'DynamicVars["VigorPower"].BaseValue, Owner.Creature, this)', "Ki uses its upgraded PowerVar")

    before = block(spirit, "public override async Task BeforeDamageReceived(")
    has(before, "if (target == Owner && dealer != null && props.IsPoweredAttack() && Amount > 0)",
        "Counter remains before incoming powered damage")
    has(before, "await CreatureCmd.Damage(choiceContext, dealer, Amount, "
        "ValueProp.Move | ValueProp.SkipHurtAnim | CounterDamage, Owner, null);",
        "Counter keeps stack damage/dealer and adds only the source tag")
    require("DamageCmd.Attack" not in spirit and "ValueProp.Unpowered" not in spirit,
            "Counter must not consume Vigor or disable all powered modifiers")
    late = block(spirit, "public override decimal ModifyHpLostAfterOstyLate(")
    require(compact(late) == compact("{ if (target == Owner && dealer?.IsDead == true "
            "&& props.IsPoweredAttack()) return 0m; return amount; }"),
            "Counter kill must still cancel incoming HP loss")
    has(spirit, "internal const ValueProp CounterDamage = (ValueProp)(1 << 30);",
        "Counter source tag must be explicit and stable")
    has(spirit, "[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.ModifyDamageAdditive))]",
        "Patch only Vigor additive damage, not global Hook damage")
    has(spirit, "[HarmonyPostfix]", "Vigor patch must be a postfix")
    postfix = block(spirit, "private static void Postfix(")
    require(compact(postfix) == compact("{ if (__instance.Owner == dealer && cardSource == null "
            "&& props.HasFlag(SGP_FightingSpirit.CounterDamage)) { __result = 0m; } }"),
            "Vigor patch must change only tagged owner damage, never stack state")
    has(spirit, "VigorPower __instance", "Postfix binds the actual Vigor instance")
    has(spirit, "ref decimal __result", "Postfix changes only additive return value")
    # The tag must not leak into other attacks/cards or an unrelated patch.
    for path in (ROOT / "src").rglob("*.cs"):
        if path.name != "SGP_FightingSpirit.cs":
            require("SGP_FightingSpirit.CounterDamage" not in path.read_text(encoding="utf-8-sig"),
                    f"Counter tag leaked outside its source: {path}")
    entry = read(ROOT / "src/Entry.cs")
    ordered(entry, ["typeof(HarmonyPatch)", "new PatchClassProcessor(harmony, type).Patch();"],
            "Existing initialization must discover the same-file patch")
    card = read(ROOT / "src/Models/Cards/SGC_FightingSpirit.cs")
    has(card, "new DamageVar(5m, ValueProp.Move)", "Fighting Spirit base stacks remain 5")
    has(card, "DynamicVars.Damage.UpgradeValueBy(3m)", "Fighting Spirit upgraded stacks remain 8")
    base = read(ROOT / "src/Models/Cards/ShinGetterCardBase.cs")
    transform = block(base, "public static async Task Transform(")
    ordered(transform, ["if (!CanTransformInCombat(player))", "return;",
                        "creature.GetPower<SGP_Seal>()", "seal.FlashBlockedTransform();", "return;",
                        "creature.GetPower<SGP_ShinForm>()",
                        "await TriggerShinFormTransform(choiceContext, creature, cardSource, playVoice);"],
            "Existing Transform remains the Seal/ShinForm authority")
    has(block(base, "protected static bool HasForm("),
        "return GetCurrentForms(player).Contains(form);", "HasForm contract stays shared")
    tracking = read(ROOT / "src/Patches/VigorPowerSetAmountPatch.cs")
    has(tracking, "if (delta <= 0) return;", "Vigor tracking reset requires actual stack decrease")
    hot_blood = read(ROOT / "src/Models/Powers/SGP_HotBlood.cs")
    has(block(hot_blood, "public override decimal ModifyDamageMultiplicative("),
        "if (cardSource == null) return 1m;", "Counter must remain outside HotBlood card-only bonus")


def validate_original(root: Path) -> None:
    core = root / "src/Core"
    creature = read(core / "Entities/Creatures/Creature.cs")
    loss = block(creature, "public DamageResult LoseHpInternal(")
    ordered(loss, ["int currentHp = CurrentHp;", "CurrentHp = Math.Max(CurrentHp - num, 0);",
                   "UnblockedDamage = currentHp - CurrentHp,", "WasTargetKilled = flag,",
                   "OverkillDamage = (flag ? Math.Max(num - currentHp, 0) : 0)"],
            "ORIGINAL LoseHpInternal: actual capped HP loss, separate overkill")
    has(block(creature, "public decimal DamageBlockInternal("),
        "props.HasFlag(ValueProp.Unblockable) ? 0m : Math.Min(Block, amount)",
        "ORIGINAL block is resolved separately from HP loss")
    ordered(block(creature, "public bool IsHittable"),
            ["if (IsDead)", "return false;", "Hook.ShouldAllowHitting(CombatState, this)"],
            "ORIGINAL hittability includes death and Hook policy")
    result = read(core / "Entities/Creatures/DamageResult.cs")
    for fragment in ("int UnblockedDamage { get; init; }", "int OverkillDamage { get; init; }",
                     "int TotalDamage => BlockedDamage + UnblockedDamage"):
        has(result, fragment, f"ORIGINAL result boundary: {fragment}")
    attack = read(core / "Commands/Builders/AttackCommand.cs")
    has(attack, "public async Task<AttackCommand> Execute(PlayerChoiceContext? choiceContext)",
        "ORIGINAL Execute returns command, not a paper-damage scalar")
    has(attack, "public IEnumerable<List<DamageResult>> Results => _results;",
        "ORIGINAL Results are grouped by hit")
    has(block(attack, "public void AddResultsInternal("), "_results.Add(results.ToList());",
        "ORIGINAL per-hit results are stored without recalculation")
    execute = block(attack, "public async Task<AttackCommand> Execute(")
    ordered(execute, ["AddResultsInternal(await CreatureCmd.Damage(",
                     "await Hook.AfterAttack(", "return this;"], "ORIGINAL results returned after hooks")
    cmd = read(core / "Commands/CreatureCmd.cs")
    damage = block(cmd, "public static async Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext "
                   "choiceContext, IEnumerable<Creature> targets, decimal amount, ValueProp props, "
                   "Creature? dealer, CardModel? cardSource)")
    ordered(damage, ["Hook.ModifyDamage(", "await Hook.BeforeDamageReceived(",
                    "creature.DamageBlockInternal(modifiedAmount, props)",
                    "HpLossHookPhase.BeforeOsty", "Hook.ModifyUnblockedDamageTarget(",
                    "HpLossHookPhase.AfterOsty", "unblockedDamageTarget.LoseHpInternal(",
                    "results.AddRange(damageResults);", "return results;"],
            "ORIGINAL final HP-loss pipeline (including redirected receivers)")
    require("Hook.BeforeAttack(" not in damage and "Hook.AfterAttack(" not in damage,
            "ORIGINAL CreatureCmd.Damage does not trigger Vigor attack consumption")
    state = read(core / "Combat/CombatState.cs")
    membership = block(state, "public bool ContainsCreature(")
    has(membership, "_allies.Contains(creature)", "ORIGINAL membership checks allies")
    has(membership, "_enemies.Contains(creature)", "ORIGINAL membership checks enemies")
    has(block(state, "public void CreatureEscaped("), "RemoveCreature(creature);",
        "ORIGINAL escape removes the target from combat")
    remove = block(state, "public void RemoveCreature(")
    has(remove, "_enemies.Remove(creature);", "ORIGINAL removal invalidates membership even if HP > 0")
    has(remove, "creature.CombatState = null;", "ORIGINAL removal may detach CombatState")

    values = read(core / "ValueProps/ValueProp.cs")
    enum = block(values, "public enum ValueProp")
    flags = dict(re.findall(r"(\w+)\s*=\s*(0x[0-9a-fA-F]+|\d+)", enum))
    require(flags == {"Unblockable": "2", "Unpowered": "4", "Move": "8", "SkipHurtAnim": "0x10"},
            "ORIGINAL flag layout changed; review the private source tag before use")
    require(all(int(value, 0) & (1 << 30) == 0 for value in flags.values()),
            "Counter source tag must not overlap an ORIGINAL ValueProp")
    extensions = read(core / "ValueProps/ValuePropExtensions.cs")
    powered = block(extensions, "public static bool IsPoweredAttack(")
    require(compact(powered) == compact("{ if (props.HasFlag(ValueProp.Move)) "
            "{ return !props.HasFlag(ValueProp.Unpowered); } return false; }"),
            "ORIGINAL powered checks must ignore the private tag")
    vigor = read(core / "Models/Powers/VigorPower.cs")
    additive = block(vigor, "public override decimal ModifyDamageAdditive(")
    has(additive, "if (!props.IsPoweredAttack()) { return 0m; }",
        "ORIGINAL Unpowered suppresses Vigor additive damage")
    has(additive, "return base.Amount;", "ORIGINAL Vigor normally adds its current stacks")
    for forbidden in ("SetAmount", "PowerCmd", "ModifyAmount", "Decrement"):
        require(forbidden not in additive, f"ORIGINAL Vigor additive must not mutate stacks: {forbidden}")
    after = block(vigor, "public override async Task AfterAttack(")
    has(after, "if (command == internalData.commandToModify)", "ORIGINAL consumption is command-scoped")
    has(after, "await PowerCmd.ModifyAmount(choiceContext, this, "
        "-internalData.amountWhenAttackStarted, null, null);", "ORIGINAL Vigor consumption occurs AfterAttack")
    for name, method, neutral in (("StrengthPower", "ModifyDamageAdditive", "0m"),
                                  ("WeakPower", "ModifyDamageMultiplicative", "1m"),
                                  ("VulnerablePower", "ModifyDamageMultiplicative", "1m")):
        source = read(core / f"Models/Powers/{name}.cs")
        has(block(source, f"public override decimal {method}("),
            f"if (!props.IsPoweredAttack()) {{ return {neutral}; }}",
            f"ORIGINAL Unpowered also suppresses {name}; private tag must preserve it")
    hooks = read(core / "Hooks/Hook.cs")
    ordered(block(hooks, "private static decimal ModifyDamageInternal("),
            ["item.ModifyDamageAdditive(target, num, props, dealer, cardSource)", "num += num2;",
             "item2.ModifyDamageMultiplicative(target, num, props, dealer, cardSource)", "num *= num3;",
             "item3.ModifyDamageCap(target, props, dealer, cardSource)"],
            "ORIGINAL modifiers: remove only Vigor additive, preserve multiply/cap")
    has(block(hooks, "public static decimal ModifyHpLost("),
        "item4.ModifyHpLostAfterOstyLate(target, num, props, dealer, cardSource)",
        "ORIGINAL late hook provides counter-kill incoming-damage cancellation")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-game-root", type=Path, default=Path("E:/Work/SlaytheSpare2"))
    args = parser.parse_args()
    validate_original(args.base_game_root)
    validate_mod()
    print(f"B1.3.0 core static gate passed ({CHECKS} source checks)")
    print(f"Original source boundary (read-only): {args.base_game_root.resolve()}")
    print("Static contracts only: build, Harmony runtime binding, and gameplay NOT executed.")


if __name__ == "__main__":
    main()
