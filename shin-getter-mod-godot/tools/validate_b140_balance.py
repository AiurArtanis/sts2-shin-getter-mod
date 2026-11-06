#!/usr/bin/env python3
"""B1.4.0 source contracts and rules models, not a production runtime test."""

from __future__ import annotations

import json
import re
import unittest
from copy import deepcopy
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATHS = {
    "chosen": "src/Models/Cards/SGC_ChosenOne.cs",
    "power": "src/Models/Powers/SGP_ChosenOne.cs",
    "roar": "src/Models/Cards/SGC_SaintDragonRoar.cs",
    "event": "src/Models/Events/SGE_GetterMandala.cs",
    "hat": "src/Models/Relics/SGR_MusashiClone.cs",
}


def read_sources() -> dict[str, str]:
    return {name: (ROOT / path).read_text(encoding="utf-8-sig") for name, path in PATHS.items()}


def block(text: str, signature: str) -> str:
    start = text.index(signature)
    opening = text.index("{", start)
    depth = 1
    for index in range(opening + 1, len(text)):
        depth += (text[index] == "{") - (text[index] == "}")
        if depth == 0:
            return text[opening + 1:index]
    raise AssertionError(f"Unclosed source block: {signature}")


def require(text: str, *fragments: str) -> None:
    for fragment in fragments:
        assert fragment in text, f"Missing source contract: {fragment}"


def ordered(text: str, *fragments: str) -> None:
    positions = [text.index(fragment) for fragment in fragments]
    assert positions == sorted(positions), f"Unexpected ordering: {fragments}"


def validate_sources(sources: dict[str, str]) -> None:
    chosen, power, roar, event, hat = (sources[key] for key in PATHS)
    require(chosen, "new BlockVar(3m, ValueProp.Unpowered)",
            "new PowerVar<SGP_ChosenOne>(1m)", ": base(2, CardType.Power",
            "DynamicVars.Block.UpgradeValueBy(2m)", "power?.AddBlockPerTransform(DynamicVars.Block.BaseValue)")
    require(power, "DynamicVars.Block.BaseValue += block;", "owner, Amount, owner, null",
            "DynamicVars.Block.BaseValue, ValueProp.Unpowered")
    keywords = roar.split("CanonicalKeywords =>", 1)[1].split(";", 1)[0]
    require(keywords, "CardKeyword.Eternal", "CardKeyword.Exhaust")
    assert "Retain" not in keywords
    require(roar, "new DamageVar(20m, ValueProp.Move)", 'new IntVar("BurnDamage", 5m)',
            "DynamicVars.Damage.UpgradeValueBy(8m)", 'DynamicVars["BurnDamage"].UpgradeValueBy(3m)',
            "PileType.Draw", "PileType.Hand", "PileType.Discard", "card != this",
            "DamageCmd.Attack(totalDamage).FromCard(this)")
    compact_roar = re.sub(r"\s+", "", roar)
    require(compact_roar, 'decimal totalDamage = DynamicVars.Damage.BaseValue'.replace(" ", "")
            + '+getterCards.Count*DynamicVars["BurnDamage"].BaseValue;')
    reward = block(event, "private async Task AddSaintDragonRoar()")
    require(reward, "CreateCard<SGC_SaintDragonRoar>(owner)", "CardPileCmd.Add(card, PileType.Deck)")
    assert "Enchant" not in reward
    require(event, "SelectAndEnchant<SGE_Devolution>", "SelectAndEnchant<SGE_Adaptation>",
            "CardCmd.Enchant<T>(card, amount)")

    require(hat, "public sealed class SGR_MusashiClone", "public bool TriggeredThisCombat",
            "[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]",
            "Status = _triggeredThisCombat ? RelicStatus.Disabled : RelicStatus.Normal",
            "public override bool IsUsedUp => TriggeredThisCombat")
    for forbidden in ("ShouldDieLate", "AfterPreventingDeath", "CreatureCmd.Heal", "HealVar(", "ShinGetterVoiceService"):
        assert forbidden not in hat, f"Stale/unauthorized behavior: {forbidden}"
    prevent = block(hat, "internal decimal PreventFatalHpLoss(")
    require(prevent, "target != Owner.Creature", "|| TriggeredThisCombat", "|| IsMelted",
            "!Owner.IsActiveForHooks", "target.CurrentHp <= 0",
            "amount < target.CurrentHp", "!CombatManager.Instance.IsInProgress",
            "CombatManager.Instance.IsOverOrEnding", "return amount;",
            "ShinGetterCardBase.IsInForm(Owner, form)")
    ordered(prevent, "ShinGetterForm.Getter1", "ShinGetterForm.Getter2", "ShinGetterForm.Getter3",
            "TriggeredThisCombat = true;", "return 0m;")
    assert "await " not in prevent
    after = block(hat, "public override async Task AfterModifyingHpLostAfterOsty()")
    ordered(after, "ShinGetterForm[] forms = _pendingForms;", "_pendingForms = Array.Empty<ShinGetterForm>();",
            "await PowerCmd.Apply<SGP_HotBlood>", "await PowerCmd.Apply<SGP_Acceleration>",
            "await PowerCmd.Apply<SGP_Indomitable>")
    for name in ("HotBlood", "Acceleration", "Indomitable"):
        require(hat, f"new PowerVar<SGP_{name}>(1m)", f"HoverTipFactory.FromPower<SGP_{name}>()")
        require(after, f'DynamicVars["SGP_{name}"].BaseValue')
    for form, power_name in (("Getter1", "HotBlood"), ("Getter2", "Acceleration"), ("Getter3", "Indomitable")):
        case = re.search(rf"case ShinGetterForm\.{form}:\s*(.*?)\s*break;", after, re.DOTALL)
        assert case is not None, f"Missing reward branch: {form}"
        require(case[1], f"await PowerCmd.Apply<SGP_{power_name}>", f'DynamicVars["SGP_{power_name}"].BaseValue')
        assert case[1].count("PowerCmd.Apply<") == 1
    for signature in ("public override Task BeforeCombatStart()", "public override Task AfterCombatEnd("):
        require(block(hat, signature), "TriggeredThisCombat = false;", "_pendingForms = Array.Empty<ShinGetterForm>();")
    require(hat, "[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]",
            "[HarmonyPostfix]", "[HarmonyPriority(Priority.Last - 2)]",
            "ref IEnumerable<AbstractModel> modifiers", "ref decimal __result")
    patch = block(hat, "private static void PreventFatalDamage(")
    ordered(patch, "!phases.HasFlag(HpLossHookPhase.AfterOsty)",
            "target.Player?.GetRelic<SGR_MusashiClone>()", "hat.PreventFatalHpLoss(target, __result)",
            "if (amount == __result)", "__result = amount;", "updatedModifiers = modifiers.ToList();",
            "if (!updatedModifiers.Contains(hat))", "updatedModifiers.Add(hat);", "modifiers = updatedModifiers;")


class HatModel:
    """Explicit rules oracle; C# hook binding still needs main-thread validation."""

    def __init__(self, used: bool = False):
        self.used = used
        self.pending: tuple[str, ...] = ()

    def prevent(self, hp: int, loss: Decimal, forms: tuple[str, ...], *, owner=True, active=True,
                ending=False, melted=False, active_hooks=True):
        if not owner or self.used or hp <= 0 or loss < hp or not active or ending or melted or not active_hooks:
            return loss
        self.pending = tuple(form for form in ("one", "two", "three") if form in forms)
        self.used = True
        return Decimal(0)

    def reward(self):
        result, self.pending = self.pending, ()
        return result

    def reset(self):
        self.used, self.pending = False, ()


class BalanceTests(unittest.TestCase):
    def test_production_contracts(self):
        validate_sources(read_sources())

    def test_chosen_mixed_stacks(self):
        source = read_sources()["chosen"]
        base = int(re.search(r"new BlockVar\((\d+)m", source)[1])
        delta = int(re.search(r"Block.UpgradeValueBy\((\d+)m", source)[1])
        for basic, upgraded in ((1, 0), (0, 1), (1, 1), (2, 3)):
            self.assertEqual((basic + upgraded, 3 * basic + 5 * upgraded),
                             (basic + upgraded, base * basic + (base + delta) * upgraded))

    def test_roar_totals(self):
        source = read_sources()["roar"]
        base = int(re.search(r"new DamageVar\((\d+)m", source)[1])
        delta = int(re.search(r"Damage.UpgradeValueBy\((\d+)m", source)[1])
        per_card = int(re.search(r'IntVar\("BurnDamage", (\d+)m', source)[1])
        per_card_delta = int(re.search(r'\["BurnDamage"\].UpgradeValueBy\((\d+)m', source)[1])
        for count in (0, 1, 3, 20):
            self.assertEqual(base + per_card * count, (20, 25, 35, 120)[(0, 1, 3, 20).index(count)])
            self.assertEqual(base + delta + (per_card + per_card_delta) * count,
                             (28, 36, 52, 188)[(0, 1, 3, 20).index(count)])

    def test_four_form_rewards(self):
        for forms in (("one",), ("two",), ("three",), ("one", "two", "three")):
            with self.subTest(forms=forms):
                hat = HatModel()
                self.assertEqual(hat.prevent(10, Decimal(10), forms), 0)
                self.assertEqual(hat.reward(), forms)
                self.assertEqual(hat.reward(), ())
                self.assertEqual(hat.prevent(10, Decimal(99), forms), 99)

    def test_nonfatal_and_fractional_damage(self):
        for loss in (Decimal(0), Decimal("0.9"), Decimal("9.9")):
            hat = HatModel()
            self.assertEqual(hat.prevent(10, loss, ("one",)), loss)
            self.assertFalse(hat.used)
        for loss in (Decimal(10), Decimal("10.1"), Decimal(99999)):
            self.assertEqual(HatModel().prevent(10, loss, ("one",)), 0)

    def test_owner_combat_and_alive_guards(self):
        for kwargs in ({"owner": False}, {"active": False}, {"ending": True},
                       {"melted": True}, {"active_hooks": False}):
            hat = HatModel()
            self.assertEqual(hat.prevent(10, Decimal(10), ("two",), **kwargs), 10)
            self.assertFalse(hat.used)
        hat = HatModel()
        self.assertEqual(hat.prevent(0, Decimal(10), ("three",)), 10)
        self.assertFalse(hat.used)

    def test_prevented_damage_does_not_spend_hat(self):
        # The oracle receives the final post-mitigation loss, not raw attack damage.
        hat = HatModel()
        self.assertEqual(hat.prevent(10, Decimal(0), ("three",)), 0)
        self.assertFalse(hat.used)

    def test_reserved_before_rewards_and_next_hit(self):
        hat = HatModel()
        self.assertEqual(hat.prevent(7, Decimal(20), ("one", "two", "three")), 0)
        self.assertTrue(hat.used)
        self.assertEqual(hat.prevent(7, Decimal(20), ("three",)), 20)
        self.assertEqual(hat.reward(), ("one", "two", "three"))
        self.assertEqual(hat.prevent(7, Decimal(20), ("three",)), 20)

    def test_captured_form_and_save_flag(self):
        hat = HatModel()
        hat.prevent(1, Decimal(1), ("one",))
        self.assertEqual(hat.pending, ("one",))
        restored = HatModel(used=hat.used)
        self.assertEqual(restored.prevent(1, Decimal(1), ("two",)), 1)
        self.assertEqual(hat.reward(), ("one",))
        hat.reset()
        self.assertEqual(hat.prevent(1, Decimal(1), ("two",)), 0)
        self.assertEqual(hat.reward(), ("two",))

    def test_localization_and_existing_keyword_semantics(self):
        for lang, title in (("zhs", "武藏的帽子"), ("eng", "Musashi's Hat"), ("jpn", "武蔵の帽子")):
            folder = ROOT / "ShinGetterMod/localization" / lang
            relics = json.loads((folder / "relics.json").read_text(encoding="utf-8"))
            powers = json.loads((folder / "powers.json").read_text(encoding="utf-8"))
            events = json.loads((folder / "events.json").read_text(encoding="utf-8"))
            self.assertEqual(relics["S_G_R_MUSASHI_CLONE.title"], title)
            description = relics["S_G_R_MUSASHI_CLONE.description"]
            for name in ("HOT_BLOOD", "ACCELERATION", "INDOMITABLE"):
                self.assertIn(powers[f"S_G_P_{name}.title"], description)
            reward = events["S_G_E_GETTER_MANDALA.pages.INITIAL.options.SAINT_DRAGON.description"]
            for stale in ("Corrupted", "腐化", "蝕み"):
                self.assertNotIn(stale, reward)
        migration = (ROOT / "src/Patches/ShinGetterSaintDragonMigrationPatch.cs").read_text(encoding="utf-8-sig")
        self.assertNotIn("RemoveEnchantment", migration)

    def test_mutations_are_rejected(self):
        sources = read_sources()
        mutants = (
            ("chosen", "new BlockVar(3m", "new BlockVar(4m"),
            ("chosen", "UpgradeValueBy(2m)", "UpgradeValueBy(3m)"),
            ("chosen", "SGP_ChosenOne>(1m)", "SGP_ChosenOne>(2m)"),
            ("chosen", ": base(2, CardType.Power", ": base(1, CardType.Power"),
            ("roar", "CardKeyword.Eternal", "CardKeyword.Retain"),
            ("roar", "CardKeyword.Exhaust, ", ""),
            ("roar", "new DamageVar(20m", "new DamageVar(15m"),
            ("roar", "Damage.UpgradeValueBy(8m)", "Damage.UpgradeValueBy(5m)"),
            ("roar", 'IntVar("BurnDamage", 5m)', 'IntVar("BurnDamage", 6m)'),
            ("roar", "PileType.Discard", "PileType.Exhaust"),
            ("roar", "+ getterCards.Count *", "- getterCards.Count *"),
            ("roar", "+ getterCards.Count *", "+ getterCards.Count +"),
            ("roar", "DamageCmd.Attack(totalDamage)", "DamageCmd.Attack(DynamicVars.Damage.BaseValue)"),
            ("event", "CardModel card = owner.RunState.CreateCard<SGC_SaintDragonRoar>(owner);",
             "CardModel card = owner.RunState.CreateCard<SGC_SaintDragonRoar>(owner); CardCmd.Enchant<Corrupted>(card, 1m);"),
            ("event", "CardCmd.Enchant<T>(card, amount)", "Task.CompletedTask"),
            ("hat", "target != Owner.Creature", "target == Owner.Creature"),
            ("hat", "|| TriggeredThisCombat", "|| false"),
            ("hat", "|| IsMelted", "|| false"),
            ("hat", "!Owner.IsActiveForHooks", "false"),
            ("hat", "target.CurrentHp <= 0", "target.CurrentHp < 0"),
            ("hat", "amount < target.CurrentHp", "amount <= target.CurrentHp"),
            ("hat", "!CombatManager.Instance.IsInProgress", "false"),
            ("hat", "CombatManager.Instance.IsOverOrEnding", "false"),
            ("hat", "TriggeredThisCombat = true;", "TriggeredThisCombat = false;"),
            ("hat", "_pendingForms = Array.Empty<ShinGetterForm>();", "// no clear"),
            ("hat", "new PowerVar<SGP_HotBlood>(1m)", "new PowerVar<SGP_HotBlood>(2m)"),
            ("hat", "new PowerVar<SGP_Acceleration>(1m)", "new PowerVar<SGP_Acceleration>(2m)"),
            ("hat", "new PowerVar<SGP_Indomitable>(1m)", "new PowerVar<SGP_Indomitable>(2m)"),
            ("hat", "ShinGetterCardBase.IsInForm(Owner, form)", "form == ShinGetterForm.Getter3"),
            ("hat", "case ShinGetterForm.Getter1:", "case ShinGetterForm.Getter3:"),
            ("hat", "case ShinGetterForm.Getter2:", "case ShinGetterForm.Getter1:"),
            ("hat", "[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]", ""),
            ("hat", "[HarmonyPostfix]", "[HarmonyPrefix]"),
            ("hat", "Priority.Last - 2", "Priority.Last - 1"),
            ("hat", "!phases.HasFlag(HpLossHookPhase.AfterOsty)", "false"),
            ("hat", "modifiers = updatedModifiers;", "modifiers = new[] { hat };"),
        )
        for name, old, new in mutants:
            with self.subTest(name=name, mutation=old):
                self.assertIn(old, sources[name])
                changed = deepcopy(sources)
                changed[name] = changed[name].replace(old, new)
                with self.assertRaises((AssertionError, ValueError)):
                    validate_sources(changed)
        print(f"Rejected {len(mutants)} source mutations")


if __name__ == "__main__":
    unittest.main(verbosity=2)
