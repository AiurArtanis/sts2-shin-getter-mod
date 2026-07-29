#!/usr/bin/env python3
"""Static acceptance gate for issue #58 event invasion batch 2."""

from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVICE_PATH = ROOT / "src/Events/ShinGetterEventInvasionService.cs"
PATCH_PATH = ROOT / "src/Patches/ShinGetterEventInvasionPatch.cs"
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

TAG_PATTERN = re.compile(r"\[(?P<closing>/)?(?P<name>[A-Za-z_]+)(?:[=\s][^\]]*)?\]")
SELF_CLOSING_TAGS = {"lb", "rb"}


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


def extract_tags(text: str) -> tuple[str, ...]:
    return tuple(match.group(0) for match in TAG_PATTERN.finditer(text))


def validate_tag_nesting(key: str, text: str) -> None:
    stack: list[str] = []
    for match in TAG_PATTERN.finditer(text):
        tag_name = match.group("name")
        if tag_name in SELF_CLOSING_TAGS:
            continue
        if match.group("closing"):
            if not stack or stack.pop() != tag_name:
                raise AssertionError(f"Invalid rich-text nesting for {key}: {match.group(0)}")
        else:
            stack.append(tag_name)
    if stack:
        raise AssertionError(f"Unclosed rich-text tags for {key}: {stack}")


def validate_service() -> None:
    service = SERVICE_PATH.read_text(encoding="utf-8")
    patch = PATCH_PATH.read_text(encoding="utf-8")

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
        "combatState.Encounter is not KnightsElite",
        "ModelDb.Encounter<KnightsElite>().ToMutable()",
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
    if "SGEncounter_TrialKnightsElite" in service:
        raise AssertionError("Trial must use the original three-knight elite encounter.")
    custom_encounter = ROOT / "src/Models/Encounters/SGEncounter_TrialKnightsElite.cs"
    if custom_encounter.exists():
        raise AssertionError("The obsolete two-knight encounter must be removed.")


def validate_localization() -> None:
    event_key_sets: dict[str, set[str]] = {}
    event_tables: dict[str, dict[str, object]] = {}
    prefix = "SHIN_GETTER_EVENT_INVASION."

    for language in LANGUAGES:
        events = load_json(LOCALIZATION_ROOT / language / "events.json")
        event_tables[language] = events
        event_key_sets[language] = {key for key in events if key.startswith(prefix)}

    expected_events = event_key_sets[LANGUAGES[0]]
    for language in LANGUAGES[1:]:
        if event_key_sets[language] != expected_events:
            raise AssertionError(f"Event localization keys differ for {language}.")

    rich_text_prefixes = (prefix, "S_G_E_GETTER_MANDALA.")
    rich_text_keys = {
        key
        for key in event_tables[LANGUAGES[0]]
        if key.startswith(rich_text_prefixes)
    }
    for key in rich_text_keys:
        expected_tag_sequence: tuple[str, ...] | None = None
        for language in LANGUAGES:
            text = event_tables[language].get(key)
            if not isinstance(text, str):
                raise AssertionError(f"Missing rich-text localization key for {language}: {key}")
            if "[white]" in text or "[/white]" in text:
                raise AssertionError(f"Use [color=white], not [white], for {language}: {key}")
            if "[cyan]" in text or "[/cyan]" in text:
                raise AssertionError(f"Use a registered color tag for {language}: {key}")
            validate_tag_nesting(f"{language}:{key}", text)
            tag_sequence = extract_tags(text)
            if expected_tag_sequence is None:
                expected_tag_sequence = tag_sequence
            elif tag_sequence != expected_tag_sequence:
                raise AssertionError(f"Rich-text tag ranges differ for {language}: {key}")

    actor_colors = {
        "RYOMA": "[red]",
        "HAYATO": "[color=white]",
        "MUQING": "[yellow]",
    }
    for language in LANGUAGES:
        events = event_tables[language]
        for actor, color_tag in actor_colors.items():
            actor_title_suffix = f".options.{actor}.title"
            for key in expected_events:
                if key.endswith(actor_title_suffix):
                    text = events.get(key)
                    if not isinstance(text, str) or color_tag not in text:
                        raise AssertionError(
                            f"Missing {actor} color in {language}: {key}"
                        )

    triad_keys = (
        f"{prefix}SPIRIT_GRAFTER.pages.INITIAL.options.TRIPLE_UNITY.title",
        f"{prefix}WOOD_CARVINGS.pages.INITIAL.options.TRIPLE_CARVING.title",
        "S_G_E_GETTER_MANDALA.pages.GETTER_G_FUSION.description",
    )
    for language in LANGUAGES:
        for key in triad_keys:
            text = event_tables[language].get(key)
            if not isinstance(text, str):
                raise AssertionError(f"Missing triad rich text for {language}: {key}")
            require(text, "[red]", "[color=white]", "[yellow]")

    rich_text_contracts = {
        f"{prefix}BYRDONIS_NEST.pages.RYOMA.description": (
            "[red]",
            "[jitter]",
            "[/jitter]",
            "[/red]",
        ),
        f"{prefix}SPIRIT_GRAFTER.pages.TRIPLE_UNITY.description": (
            "[getter_ray]",
            "[sine]",
            "[/sine]",
            "[/getter_ray]",
        ),
        f"{prefix}WOOD_CARVINGS.pages.INITIAL.options.TRIPLE_CARVING.title": (
            "[getter_ray]",
            "[/getter_ray]",
        ),
        f"{prefix}TRIAL.pages.RYOMA.options.START_FIGHT.title": (
            "[red]",
            "[b]",
            "[/b]",
            "[/red]",
        ),
        f"{prefix}ROUND_TEA_PARTY.pages.RYOMA.description": (
            "[red]",
            "[jitter]",
            "[/jitter]",
            "[/red]",
        ),
        "S_G_E_GETTER_MANDALA.pages.INITIAL.description": (
            "[sine]",
            "[aqua]",
            "[/aqua]",
            "[/sine]",
        ),
    }
    for language in LANGUAGES:
        for key, required_tags in rich_text_contracts.items():
            text = event_tables[language].get(key)
            if not isinstance(text, str):
                raise AssertionError(f"Missing rich-text contract for {language}: {key}")
            require(text, *required_tags)

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

    trial_fight_key = f"{prefix}TRIAL.pages.RYOMA.options.START_FIGHT.description"
    expected_knights = {
        "eng": ("Flail Knight", "Spectral Knight", "Magi Knight"),
        "jpn": ("フレイルナイト", "スペクトラルナイト", "メイジナイト"),
        "zhs": ("连枷骑士", "幽灵骑士", "魔法骑士"),
    }
    for language, knight_names in expected_knights.items():
        description = event_tables[language].get(trial_fight_key)
        if not isinstance(description, str) or not all(
            name in description for name in knight_names
        ):
            raise AssertionError(
                f"Trial fight description must name all three knights for {language}."
            )


def main() -> None:
    validate_service()
    validate_localization()
    print("issue #58 static validation passed")


if __name__ == "__main__":
    main()
