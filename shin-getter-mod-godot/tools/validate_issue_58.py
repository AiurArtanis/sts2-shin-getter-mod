#!/usr/bin/env python3
"""Static acceptance gate for issue #58 event invasion batch 2."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVICE_PATH = ROOT / "src/Events/ShinGetterEventInvasionService.cs"
PATCH_PATH = ROOT / "src/Patches/ShinGetterEventInvasionPatch.cs"
ENCOUNTER_PATH = ROOT / "src/Models/Encounters/SGEncounter_TrialKnightsElite.cs"
LOCALIZATION_ROOT = ROOT / "ShinGetterMod/localization"
LANGUAGES = ("eng", "jpn", "zhs")

EVENT_TYPES = (
    "ByrdonisNest",
    "InfestedAutomaton",
    "TheLegendsWereTrue",
    "Trial",
    "SunkenStatue",
    "SpiralingWhirlpool",
    "RoundTeaParty",
    "RanwidTheElder",
)


def require(text: str, *needles: str) -> None:
    missing = [needle for needle in needles if needle not in text]
    if missing:
        raise AssertionError("Missing static contract(s):\n- " + "\n- ".join(missing))


def method_body(source: str, signature: str) -> str:
    start = source.index(signature)
    brace = source.index("{", start)
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[brace : index + 1]
    raise AssertionError(f"Unbalanced method body: {signature}")


def load_json(path: Path) -> dict[str, object]:
    with path.open("r", encoding="utf-8-sig") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise AssertionError(f"Expected a JSON object: {path}")
    return value


def validate_service() -> None:
    service = SERVICE_PATH.read_text(encoding="utf-8")
    patch = PATCH_PATH.read_text(encoding="utf-8")
    encounter = ENCOUNTER_PATH.read_text(encoding="utf-8")

    for event_type in EVENT_TYPES:
        require(service, f"{event_type} ")

    require(
        service,
        'option.TextKey.StartsWith("TRIAL.pages.MERCHANT.options."',
        'option.TextKey.StartsWith("TRIAL.pages.NOBLE.options."',
        'option.TextKey.StartsWith("TRIAL.pages.NONDESCRIPT.options."',
        "owner.Creature.MaxHp > 4",
        "HasAnyCard<SGC_Jammer, SGC_Insight>(owner)",
        "await LoseHp(owner, 6)",
        "ModelDb.Encounter<ByrdonisElite>().ToMutable()",
        "using ByrdpipRelic = MegaCrit.Sts2.Core.Models.Relics.Byrdpip;",
        "ModelDb.Relic<ByrdpipRelic>().ToMutable()",
        "CardFactory.CreateForReward(owner, 2, powerOptions)",
        "CardFactory.CreateForReward(owner, 2, zeroCostOptions)",
        "CardCreationFlags.NoCardPoolModifications",
        "owner.Creature.CurrentHp > 12",
        "owner.Gold >= 35",
        "owner.HasOpenPotionSlots",
        "await LoseHp(owner, 12)",
        "await PlayerCmd.LoseGold(35, owner, GoldLossType.Spent)",
        "await PotionCmd.TryToProcure<SGR_GetterColdBrew>(owner)",
        "owner.Creature.CurrentHp > 7",
        "owner.Creature,\n            7,",
        "eventModel.DynamicVars.Gold.BaseValue * 1.8m",
        "owner.Creature.CurrentHp > 11",
        "await LoseHp(owner, 11)",
        "RelicFactory.PullNextRelicFromFront(owner).ToMutable()",
        "owner.Deck.Cards.Any(card => card is SGC_SaotomeBlueprint)",
        ".OrderBy(card => card.IsUpgraded)",
        "PendingBattleSetup.ByrdonisNest",
        "PendingBattleSetup.Trial",
        "ReferenceEquals(combatState.Encounter, pending.Encounter)",
        "PendingBattleSetups[owner] = (setup, encounter)",
        "combatState.Encounter is not ByrdonisElite",
        "combatState.Encounter is not SGEncounter_TrialKnightsElite",
        "await CreatureCmd.Stun(byrdonis",
        "ReferenceEquals(combatCard.DeckVersion, deckCard)",
        "await CardCmd.AutoPlay(",
        "SGC_TornadoDrill or SGC_SpiralDrill",
        "card.Enchantment == null",
        "CardCmd.Downgrade(selected)",
        "EnchantmentModel spiral = ModelDb.Enchantment<Spiral>().ToMutable()",
        "card.EnchantInternal(spiral, 1m)",
        "CardsEnchanted.Add(new CardEnchantmentHistoryEntry(card, spiral.Id))",
    )

    spirit_command_start = service.index("private static bool IsTrialSpiritCommand")
    spirit_command_end = service.index(";", spirit_command_start)
    require(
        service[spirit_command_start : spirit_command_end + 1],
        "SGC_Ki",
        "SGC_Spirit",
        "SGC_SuperKi",
        "SGC_FightingSpirit",
        "SGC_Indomitable",
    )

    legends_ryoma = method_body(
        service, "private static async Task TheLegendsWereTrueRyoma"
    )
    if "AddCurseToDeck" in legends_ryoma:
        raise AssertionError("Ryoma's legend route must not add a curse.")

    ranwid = method_body(service, "private static async Task RanwidTheElderRyoma")
    if "LoseGold" in ranwid:
        raise AssertionError("Ranwid's route must not spend Gold.")
    require(
        ranwid,
        ".OrderBy(card => card.IsUpgraded)",
        "do\n        {",
        "while (selected == null);",
    )
    if ranwid.count("RelicFactory.PullNextRelicFromFront(owner)") != 2:
        raise AssertionError("Ranwid's route must pull exactly two relic choices once.")

    require(
        patch,
        '[HarmonyPatch(typeof(EventModel), "get_IsShared")]',
        "IsEnteringSinglePlayerEventCombat(__instance)",
    )
    require(
        encounter,
        "RoomType.Elite",
        "EncounterTag.Knights",
        "ModelDb.Monster<SpectralKnight>()",
        "ModelDb.Monster<MagiKnight>()",
        "ModelDb.Affliction<Hexed>().OverlayPath",
    )
    if "HasScene" in encounter:
        raise AssertionError("The custom encounter has no dedicated scene asset.")
    if "FlailKnight" in encounter:
        raise AssertionError("The issue #58 Trial encounter must contain only two knights.")


def validate_localization() -> None:
    event_key_sets: dict[str, set[str]] = {}
    encounter_key_sets: dict[str, set[str]] = {}
    prefix = "SHIN_GETTER_EVENT_INVASION."

    for language in LANGUAGES:
        events = load_json(LOCALIZATION_ROOT / language / "events.json")
        encounters = load_json(LOCALIZATION_ROOT / language / "encounters.json")
        event_key_sets[language] = {key for key in events if key.startswith(prefix)}
        encounter_key_sets[language] = set(encounters)

    expected_events = event_key_sets[LANGUAGES[0]]
    expected_encounters = encounter_key_sets[LANGUAGES[0]]
    for language in LANGUAGES[1:]:
        if event_key_sets[language] != expected_events:
            raise AssertionError(f"Event localization keys differ for {language}.")
        if encounter_key_sets[language] != expected_encounters:
            raise AssertionError(f"Encounter localization keys differ for {language}.")

    for event_name in (
        "BYRDONIS_NEST",
        "INFESTED_AUTOMATON",
        "THE_LEGENDS_WERE_TRUE",
        "TRIAL",
        "SUNKEN_STATUE",
        "SPIRALING_WHIRLPOOL",
        "ROUND_TEA_PARTY",
        "RANWID_THE_ELDER",
    ):
        if not any(key.startswith(f"{prefix}{event_name}.") for key in expected_events):
            raise AssertionError(f"Missing localization family: {event_name}")

    required_encounter_keys = {
        "S_G_ENCOUNTER_TRIAL_KNIGHTS_ELITE.title",
        "S_G_ENCOUNTER_TRIAL_KNIGHTS_ELITE.loss",
    }
    if not required_encounter_keys.issubset(expected_encounters):
        raise AssertionError("Missing Trial encounter localization keys.")


def main() -> None:
    validate_service()
    validate_localization()
    print("issue #58 static validation passed")


if __name__ == "__main__":
    main()
