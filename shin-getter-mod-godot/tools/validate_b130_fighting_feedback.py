#!/usr/bin/env python3
"""Read-only source regression for B1.3.0 Fighting Spirit feedback.

Run with python -B to avoid writing import caches. No build, Godot, game,
packaging, or damage simulation is performed. JSON migration is owned by the
parent task: S_G_C_FIGHTING_SPIRIT.description must use {CounterDamage:diff()}.
Original contracts establish timing, not a reproduction of reported gameplay.
"""

import argparse
from pathlib import Path
import unittest

from validate_b130_core import block, compact, has, ordered, require, validate_original


ROOT = Path(__file__).resolve().parents[1]
BASE_ROOT = Path("E:/Work/SlaytheSpare2")


def source(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def card_contract(text: str) -> None:
    has(text, 'new DynamicVar("CounterDamage", 5m)', "Static base stacks must be 5")
    has(block(text, "protected override void OnUpgrade()"),
        'DynamicVars["CounterDamage"].UpgradeValueBy(3m);', "Upgraded stacks must be 8")
    has(block(text, "protected override async Task OnPlay("),
        'await PowerCmd.Apply<SGP_FightingSpirit>(choiceContext, Owner.Creature, '
        'DynamicVars["CounterDamage"].BaseValue, Owner.Creature, this);',
        "Apply the printed base stacks, not a preview value")
    for forbidden in ("DamageVar", "DynamicVars.Damage", "PreviewValue", "ValueProp"):
        require(forbidden not in compact(text), f"Static card must not use {forbidden}")
    for preserved in ("SpiritRequirement => 2", "base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)",
                      "HoverTipFactory.FromPower<SGP_FightingSpirit>()"):
        has(text, preserved, f"Preserved card contract: {preserved}")


def counter_contract(text: str) -> None:
    has(text, "StackType => PowerStackType.Counter;", "Use the actual Counter enum")
    has(text, "protected override object InitInternalData() => new Data();",
        "Guard and kill evidence must be mutable-instance data")
    before = block(text, "public override async Task BeforeDamageReceived(")
    has(before, "if (target != Owner || dealer == null || dealer.Side == Owner.Side "
        "|| dealer.IsDead || Owner.IsDead || !props.IsPoweredAttack() || Amount <= 0 "
        "|| props.HasFlag(CounterDamage)) return;", "Only live hostile powered hits can counter")
    ordered(before, ["if (data.IsCountering) return;", "data.CanceledAttacker = null;",
                     "data.CanceledCardSource = null;", "data.IsCountering = true;", "try",
                     "await CreatureCmd.Damage(choiceContext, dealer, Amount, "
                     "ValueProp.Move | ValueProp.SkipHurtAnim | CounterDamage, Owner, null, null);",
                     "if (dealer.IsDead)", "data.CanceledAttacker = dealer;",
                     "data.CanceledProps = props;", "data.CanceledCardSource = cardSource;",
                     "finally", "data.IsCountering = false;"],
            "Counter completes with cumulative Amount before recording a kill; guard always unwinds")
    require(compact(before).count("CreatureCmd.Damage(") == 1, "Exactly one cumulative counter call")
    for forbidden in ("DamageCmd.Attack", "ValueProp.Unpowered", "PowerCmd.", "SetAmount(",
                      "ModifyDamage(", "LoseHpInternal(", "for(", "while("):
        require(forbidden not in compact(text), f"Counter must not use {forbidden}")


def cancellation_contract(text: str) -> None:
    matches = block(text, "internal bool CancelsIncomingDamage(")
    require(compact(matches) == compact("{ Data data = GetInternalData<Data>(); "
            "return target == Owner && dealer != null && dealer.IsDead "
            "&& props.IsPoweredAttack() && !props.HasFlag(CounterDamage) "
            "&& data.CanceledAttacker == dealer && data.CanceledProps == props "
            "&& data.CanceledCardSource == cardSource; }"),
            "Cancellation requires this counter's dead attacker and exact incoming source")
    for signature in ("public override decimal ModifyHpLostBeforeOsty(",
                      "public override decimal ModifyHpLostAfterOstyLate("):
        require(compact(block(text, signature)) == compact("{ return "
                "CancelsIncomingDamage(target, props, dealer, cardSource) ? 0m : amount; }"),
                "Cancel before redirection and at late HP loss; nonlethal hits remain unchanged")
    has(text, "[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]",
        "Final result fallback must bind the actual HP-loss boundary")
    has(text, "[HarmonyPostfix] [HarmonyPriority(Priority.Last)] private static void CancelCounteredHit(",
        "Fallback runs after HP-loss listeners")
    require(compact(block(text, "private static void CancelCounteredHit(")) == compact(
            "{ if (target.GetPower<SGP_FightingSpirit>()?.CancelsIncomingDamage("
            "target, props, dealer, cardSource) == true) __result = 0m; }"),
            "Fallback only zeros a recorded Fighting Spirit kill on its owner")


def vigor_contract(text: str) -> None:
    has(text, "internal const ValueProp CounterDamage = (ValueProp)(1 << 30);",
        "Keep the existing nonoverlapping counter tag")
    has(text, "[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.ModifyDamageAdditive))]",
        "Vigor exclusion remains limited to its additive method")
    require(compact(block(text, "private static void Postfix(")) == compact(
            "{ if (__instance.Owner == dealer && cardSource == null "
            "&& props.HasFlag(SGP_FightingSpirit.CounterDamage)) { __result = 0m; } }"),
            "Do not suppress normal Vigor damage or change its stacks")
    has(text, "VigorPower __instance", "Bind the real Vigor instance")
    has(text, "ref decimal __result", "Patch only the damage return value")


class FightingFeedbackTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.card = source(ROOT / "src/Models/Cards/SGC_FightingSpirit.cs")
        cls.power = source(ROOT / "src/Models/Powers/SGP_FightingSpirit.cs")
        cls.core = BASE_ROOT / "src/Core"

    def test_original_core_regression(self) -> None:
        # Reuse the unchanged gate's ORIGINAL checks, not its obsolete mod snapshots.
        validate_original(BASE_ROOT)

    def test_static_card_base_and_upgrade(self) -> None:
        card_contract(self.card)
        dynamic = source(self.core / "Localization/DynamicVars/DynamicVar.cs")
        self.assertEqual(compact(block(dynamic, "public virtual void UpdateCardPreview(")), "{}")
        damage = source(self.core / "Localization/DynamicVars/DamageVar.cs")
        has(block(damage, "public override void UpdateCardPreview("), "Hook.ModifyDamage(",
            "ORIGINAL DamageVar explains the Vigor-dependent preview")

    def test_multiple_cards_merge_one_amount(self) -> None:
        enum = block(source(self.core / "Entities/Powers/PowerStackType.cs"), "public enum PowerStackType")
        self.assertEqual(compact(enum), "{None,Counter,Single}")
        model = source(self.core / "Models/PowerModel.cs")
        has(model, "public virtual PowerInstanceType InstanceType => PowerInstanceType.None;",
            "Default power has one instance, independent of stack-label enum")
        power_cmd = source(self.core / "Commands/PowerCmd.cs")
        apply = block(power_cmd, "public static async Task<T?> Apply<T>(")
        ordered(apply, ["FindExistingInstanceForStacking(", "ModifyAmount(choiceContext, power, amount,"],
                "ORIGINAL subsequent cards increment the existing power")
        has(block(power_cmd, "public static PowerModel? FindExistingInstanceForStacking("),
            "PowerInstanceType.None => target.GetPower(basePower.Id)", "Lookup uses the power ID")
        ordered(block(power_cmd, "public static async Task<int> ModifyAmount("),
                ["int newAmount = power.Amount + (int)modifiedOffset;", "power.SetAmount(newAmount, silent);"],
                "ORIGINAL stack amount is additive, not overwritten")
        self.assertNotIn("InstanceType", compact(self.power))
        counter_contract(self.power)

    def test_current_hit_not_precomputed_hp_loss(self) -> None:
        cmd = source(self.core / "Commands/CreatureCmd.cs")
        beta = "Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)" in cmd
        damage = block(cmd, "public static async Task<IEnumerable<DamageResult>> Damage(PlayerChoiceContext "
                       + ("choiceContext, IEnumerable<Creature>? targets, decimal amount, ValueProp props, "
                          "Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)" if beta else
                          "choiceContext, IEnumerable<Creature> targets, decimal amount, ValueProp props, "
                          "Creature? dealer, CardModel? cardSource)"))
        ordered(damage, ["if (dealer != null && dealer.IsDead)", "Hook.ModifyDamage(",
                         "await Hook.BeforeDamageReceived(", "creature.DamageBlockInternal(",
                         "HpLossHookPhase.BeforeOsty", "Hook.ModifyUnblockedDamageTarget(",
                         "HpLossHookPhase.AfterOsty", "unblockedDamageTarget.LoseHpInternal("],
                "ORIGINAL counter is awaited before block, redirection, and HP loss")
        self.assertEqual(damage.count("dealer.IsDead"), 1,
                         "Review engine drift: entry-only death guard is the current-hit gap")
        hooks = source(self.core / "Hooks/Hook.cs")
        ordered(block(hooks, "public static decimal ModifyHpLost("),
                ["decimal num = amount;", "item.ModifyHpLostBeforeOsty(",
                 "item2.ModifyHpLostBeforeOstyLate(", "item3.ModifyHpLostAfterOsty(",
                 "item4.ModifyHpLostAfterOstyLate(", "return num;"],
                "ORIGINAL late result still precedes LoseHpInternal, but other listeners can run later")
        cancellation_contract(self.power)

    def test_first_and_subsequent_multihits(self) -> None:
        attack = source(self.core / "Commands/Builders/AttackCommand.cs")
        execute = block(attack, "public async Task<AttackCommand> Execute(")
        ordered(execute, ["await Hook.BeforeAttack(", "for (int i = 0; (decimal)i < attackCount; i++)",
                         "if (Attacker.IsDead) { break; }", "AddResultsInternal(await CreatureCmd.Damage(",
                         "await Hook.AfterAttack("],
                "ORIGINAL each hit awaits damage; a counter kill stops subsequent hits")
        counter_contract(self.power)

    def test_cancellation_and_nonlethal_scope(self) -> None:
        cancellation_contract(self.power)

    def test_counter_recursion_and_exception_cleanup(self) -> None:
        counter_contract(self.power)
        thorns = source(self.core / "Models/Powers/ThornsPower.cs")
        has(thorns, "ValueProp.Unpowered | ValueProp.SkipHurtAnim", "ORIGINAL Thorns cannot reenter counter")

    def test_strength_weak_vulnerable_and_vigor_unchanged(self) -> None:
        vigor_contract(self.power)
        # ORIGINAL powered-modifier and consumption contracts are checked above.
        # CreatureCmd.Damage has no BeforeAttack/AfterAttack, so this call cannot
        # consume Vigor. A tagged powered hit still uses Strength/Weak/Vulnerable.
        self.assertNotIn("DamageCmd.Attack", self.power)
        self.assertNotIn("ValueProp.Unpowered", self.power)

    def test_patch_registration(self) -> None:
        entry = source(ROOT / "src/Entry.cs")
        ordered(entry, ["typeof(HarmonyPatch)", "new PatchClassProcessor(harmony, type).Patch();"],
                "Existing initialization discovers both patches in the allowed Power file")

    def test_negative_source_regressions(self) -> None:
        cases = [
            (card_contract, self.card, 'new DynamicVar("CounterDamage", 5m)', 'new DamageVar(5m, ValueProp.Move)'),
            (card_contract, self.card, 'UpgradeValueBy(3m)', 'UpgradeValueBy(2m)'),
            (counter_contract, self.power, 'dealer,\n                Amount,', 'dealer,\n                5m,'),
            (counter_contract, self.power, 'if (data.IsCountering)', 'if (false)'),
            (counter_contract, self.power, '|| props.HasFlag(CounterDamage)', ''),
            (counter_contract, self.power, 'data.IsCountering = false;', 'data.IsCountering = true;'),
            (cancellation_contract, self.power, '&& dealer.IsDead', '&& dealer.IsAlive'),
            (cancellation_contract, self.power, '&& data.CanceledAttacker == dealer', ''),
            (cancellation_contract, self.power, '&& data.CanceledCardSource == cardSource', ''),
            (cancellation_contract, self.power, 'nameof(Hook.ModifyHpLost)', 'nameof(Hook.ModifyDamage)'),
            (vigor_contract, self.power, '&& props.HasFlag(SGP_FightingSpirit.CounterDamage)', ''),
        ]
        for validator, text, old, new in cases:
            with self.subTest(regression=old):
                self.assertIn(old, text)
                with self.assertRaises(AssertionError):
                    validator(text.replace(old, new, 1))


def main() -> None:
    global BASE_ROOT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-game-root", type=Path, default=BASE_ROOT)
    BASE_ROOT = parser.parse_args().base_game_root
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(FightingFeedbackTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    print(f"Original source (read-only): {BASE_ROOT.resolve()}")
    print("Static contracts only; Harmony binding, actual damage, combat cleanup, and gameplay NOT executed.")
    print("Parent localization migration: S_G_C_FIGHTING_SPIRIT.description -> {CounterDamage:diff()} (5/8).")
    raise SystemExit(0 if result.wasSuccessful() else 1)


if __name__ == "__main__":
    main()
