#!/usr/bin/env python3
"""Static acceptance gate for issue#58 event invasion batch 2."""

from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVICE_PATH = ROOT / "src/Events/ShinGetterEventInvasionService.cs"
PATCH_PATH = ROOT / "src/Patches/ShinGetterEventInvasionPatch.cs"
BYRDPIP_REWARD_PATCH_PATH = ROOT / "src/Patches/ShinGetterByrdpipRewardPatch.cs"
RICH_TEXT_PATCH_PATH = ROOT / "src/Patches/RichTextWhitePatch.cs"
RICH_TEXT_EFFECT_PATHS = {
    "white": ROOT / "src/RichTextTags/RichTextWhite.cs",
    "yellow": ROOT / "src/RichTextTags/RichTextYellow.cs",
    "getter_ray": ROOT / "src/RichTextTags/RichTextGetterRay.cs",
}
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
    byrdpip_patch = BYRDPIP_REWARD_PATCH_PATH.read_text(encoding="utf-8")

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
        "owner.Gold >= 35",
        "HasAnyCard<SGC_GetterClaw, SGC_SpiralDrill, SGC_TornadoDrill>(owner)",
        "await PlayerCmd.LoseGold(35, owner, GoldLossType.Spent)",
        "await RewardsCmd.OfferCustom(owner",
        "new PotionReward(ModelDb.Potion<SGR_GetterColdBrew>().ToMutable(), owner)",
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
        "card.SetToFreeThisCombat()",
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

    byrdonis_options = method_body(service, "private static IEnumerable<EventOption> BuildByrdonisNestOptions")
    require(
        byrdonis_options,
        "owner.Deck.Cards.Any(card => card.IsUpgradable)",
        "CreateConditionalOption(",
        '"BYRDONIS_NEST",\n            "MUQING"',
    )
    byrdonis_muqing = method_body(service, "private static async Task ByrdonisNestMuqing")
    require(
        byrdonis_muqing,
        "await LoseHp(owner, 6)",
        "int upgradeCount = Math.Min(3, candidates.Count)",
        "eventModel.Rng.NextItem(candidates)",
        "candidates.Remove(card)",
        "CardCmd.Upgrade(card, CardPreviewStyle.EventLayout)",
    )
    if byrdonis_muqing.index("await LoseHp(owner, 6)") > byrdonis_muqing.index("CardCmd.Upgrade"):
        raise AssertionError("Byrdonis Muqing must lose HP before upgrading cards.")

    byrdonis_ryoma = method_body(service, "private static Task ByrdonisNestRyoma")
    require(
        byrdonis_ryoma,
        "ModelDb.Relic<ByrdpipRelic>().ToMutable()",
    )
    if "SpecialCardReward" in byrdonis_ryoma or "CreateCard<ByrdSwoop>" in byrdonis_ryoma:
        raise AssertionError("Byrd Swoop must not be granted as an independent combat reward.")

    require(
        byrdpip_patch,
        "[HarmonyPatch(typeof(Byrdpip), nameof(Byrdpip.AfterObtained))]",
        "private static void Prefix(Byrdpip __instance, out bool __state)",
        "__state = HasByrdonisEgg(__instance.Owner)",
        "PileType.Deck.GetPile(player).Cards.Any(card => card is ByrdonisEgg)",
        "CombatManager.Instance.IsInProgress",
        "player.PlayerCombatState?.AllCards.Any(card => card is ByrdonisEgg) == true",
        "private static void Postfix(Byrdpip __instance, bool __state, ref Task __result)",
        "if (__state)",
        "__result = AddByrdSwoopAfterObtained(__result, __instance.Owner)",
        "await originalTask",
        "owner.RunState.CreateCard<ByrdSwoop>(owner)",
        "CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(byrdSwoop, PileType.Deck))",
    )

    legends_options = method_body(
        service, "private static IEnumerable<EventOption> BuildTheLegendsWereTrueOptions"
    )
    require(
        legends_options,
        "owner.Gold >= 35",
        "HasAnyCard<SGC_GetterClaw, SGC_SpiralDrill, SGC_TornadoDrill>(owner)",
    )
    for obsolete_gate in (
        "owner.Creature.CurrentHp",
        "owner.HasOpenPotionSlots",
        "SGC_Insight",
        "SGC_Acceleration",
    ):
        if obsolete_gate in legends_options:
            raise AssertionError(f"Obsolete Legends gate remains: {obsolete_gate}")

    legends_ryoma = method_body(
        service, "private static async Task TheLegendsWereTrueRyoma"
    )
    if "AddCurseToDeck" in legends_ryoma:
        raise AssertionError("Ryoma's legend route must not add a curse.")

    legends_hayato = method_body(
        service, "private static async Task TheLegendsWereTrueHayato"
    )
    require(
        legends_hayato,
        "await PlayerCmd.LoseGold(35, owner, GoldLossType.Spent)",
        "await RewardsCmd.OfferCustom(owner",
        "new PotionReward(ModelDb.Potion<SGR_GetterColdBrew>().ToMutable(), owner)",
    )
    if "PotionCmd.TryToProcure" in legends_hayato:
        raise AssertionError("Hayato's cold brew must use the original potion reward screen.")
    if "LoseHp" in legends_hayato:
        raise AssertionError("Hayato's legend route must not lose HP.")

    trial_setup_start = service.index("List<CardModel> cardsToPlay")
    trial_setup_end = service.index("\n    }", trial_setup_start)
    trial_setup = service[trial_setup_start:trial_setup_end]
    require(trial_setup, "card.SetToFreeThisCombat()", "await CardCmd.AutoPlay(")
    if trial_setup.index("card.SetToFreeThisCombat()") > trial_setup.index("await CardCmd.AutoPlay("):
        raise AssertionError("Trial Spirit cards must become free before they are auto-played.")

    ranwid_dialogue = method_body(service, "private static Task RanwidTheElderRyoma")
    require(
        ranwid_dialogue,
        'PageOptionKey("RANWID_THE_ELDER", "RYOMA", "CHOOSE_RELIC")',
        "SetEventStateMethod.Invoke(",
        "isProceed: true",
    )
    for premature_action in (
        "RelicFactory.PullNextRelicFromFront",
        "RelicSelectCmd",
        "CardPileCmd.RemoveFromDeck",
        "RelicCmd.Obtain",
    ):
        if premature_action in ranwid_dialogue:
            raise AssertionError(f"Ranwid dialogue performs a premature action: {premature_action}")

    ranwid_selection = method_body(
        service, "private static async Task RanwidTheElderChooseRelic"
    )
    if "LoseGold" in ranwid_selection:
        raise AssertionError("Ranwid's route must not spend Gold.")
    require(
        ranwid_selection,
        ".OrderBy(card => card.IsUpgraded)",
        "do\n        {",
        "while (selected == null);",
        "await CardPileCmd.RemoveFromDeck(blueprint)",
        "await RelicCmd.Obtain(selected, owner)",
        'Finish(eventModel, PageKey("RANWID_THE_ELDER", "RYOMA_RESULT"))',
    )
    if ranwid_selection.count("RelicFactory.PullNextRelicFromFront(owner)") != 2:
        raise AssertionError("Ranwid's route must pull exactly two relic choices once.")
    ranwid_order = (
        ranwid_selection.index("RelicFactory.PullNextRelicFromFront"),
        ranwid_selection.index("RelicSelectCmd.FromChooseARelicScreen"),
        ranwid_selection.index("CardPileCmd.RemoveFromDeck"),
        ranwid_selection.index("RelicCmd.Obtain"),
        ranwid_selection.index("Finish(eventModel"),
    )
    if ranwid_order != tuple(sorted(ranwid_order)):
        raise AssertionError("Ranwid must generate, select, remove blueprint, obtain, then finish.")

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


def validate_rich_text_registration() -> None:
    patch = RICH_TEXT_PATCH_PATH.read_text(encoding="utf-8")
    require(
        patch,
        '[HarmonyPatch(typeof(MegaRichTextLabel), "InstallEffectsIfNeeded")]',
        "if (!__instance.BbcodeEnabled)",
        "__instance.CustomEffects.Add(WhiteEffect)",
        "__instance.CustomEffects.Add(YellowEffect)",
        "__instance.CustomEffects.Add(GetterRayEffect)",
    )

    expected_effect_contracts = {
        "white": ('Bbcode => "white"', "charFx.Color = Colors.White"),
        "yellow": ('Bbcode => "yellow"', 'charFx.Color = new Color("FFE600")'),
        "getter_ray": (
            'Bbcode => "getter_ray"',
            'charFx.Color = new Color("44FCC5")',
        ),
    }
    for tag_name, contracts in expected_effect_contracts.items():
        effect = RICH_TEXT_EFFECT_PATHS[tag_name].read_text(encoding="utf-8")
        require(effect, *contracts)


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

    for language, events in event_tables.items():
        for key, value in events.items():
            if isinstance(value, str) and "[color=white]" in value:
                raise AssertionError(
                    f"Use the registered [white] tag for {language}: {key}"
                )

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
        "HAYATO": "[white]",
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
            require(text, "[red]", "[white]", "[yellow]")

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
        f"{prefix}THE_LEGENDS_WERE_TRUE.pages.RYOMA.description": (
            "[red]",
            "[/red]",
            "[white]",
            "[/white]",
            "[yellow]",
            "[/yellow]",
        ),
        f"{prefix}SUNKEN_STATUE.pages.MUQING.description": (
            "[jitter]",
            "[/jitter]",
        ),
        f"{prefix}SPIRALING_WHIRLPOOL.pages.HAYATO.description": (
            "[sine]",
            "[aqua]",
            "[/aqua]",
            "[/sine]",
            "[u]",
            "[/u]",
        ),
        f"{prefix}ROUND_TEA_PARTY.pages.RYOMA.description": (
            "[red]",
            "[jitter]",
            "[/jitter]",
            "[/red]",
        ),
        "S_G_E_GETTER_MANDALA.pages.INITIAL.description": (
            "[sine]",
            "[getter_ray]",
            "[/getter_ray]",
            "[/sine]",
        ),
        "S_G_E_GETTER_MANDALA.pages.PRIMAL_GETTER.description": (
            "[getter_ray]",
            "[b]",
            "[/b]",
            "[/getter_ray]",
        ),
        "S_G_E_GETTER_MANDALA.pages.IGNORE.description": (
            "[getter_ray]",
            "[/getter_ray]",
        ),
    }
    for language in LANGUAGES:
        for key, required_tags in rich_text_contracts.items():
            text = event_tables[language].get(key)
            if not isinstance(text, str):
                raise AssertionError(f"Missing rich-text contract for {language}: {key}")
            require(text, *required_tags)

    exact_localization = {
        "eng": {
            f"{prefix}TRIAL.pages.INITIAL.options.RYOMA.title": "[red]You are all guilty.[/red]",
            f"{prefix}TRIAL.pages.RYOMA.options.START_FIGHT.title": "[red][b]Interrupt the trial.[/b][/red]",
            f"{prefix}THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.HAYATO.title": "[white]Take another route.[/white]",
            f"{prefix}THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.HAYATO.description": "Pay [red]35[/red] Gold. Obtain [gold]Getter Cold Brew[/gold].",
            f"{prefix}BYRDONIS_NEST.pages.INITIAL.options.MUQING_LOCKED.description": "Requires at least 1 upgradable card.",
            f"{prefix}BYRDONIS_NEST.pages.INITIAL.options.MUQING.description": "Lose [red]6[/red] HP. Randomly upgrade 3 cards.",
            f"{prefix}SPIRALING_WHIRLPOOL.pages.INITIAL.options.HAYATO.title": "[white]Unmake one completed record and reverse-engineer its structure.[/white]",
            f"{prefix}SUNKEN_STATUE.pages.INITIAL.options.MUQING.description": "Lose [red]7[/red] Max HP. Gain more Gold.",
            f"{prefix}RANWID_THE_ELDER.pages.RYOMA.options.CHOOSE_RELIC.title": "Continue.",
        },
        "jpn": {
            f"{prefix}TRIAL.pages.INITIAL.options.RYOMA.title": "[red]お前たちは全員有罪だ。[/red]",
            f"{prefix}TRIAL.pages.RYOMA.options.START_FIGHT.title": "[red][b]裁判を中断する。[/b][/red]",
            f"{prefix}THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.HAYATO.title": "[white]別の道を行く。[/white]",
            f"{prefix}THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.HAYATO.description": "[red]35[/red]ゴールドを支払い、[gold]ゲッターコールドブリュー[/gold]を得る。",
            f"{prefix}BYRDONIS_NEST.pages.INITIAL.options.MUQING_LOCKED.description": "アップグレード可能なカードが1枚以上必要。",
            f"{prefix}BYRDONIS_NEST.pages.INITIAL.options.MUQING.description": "HPを[red]6[/red]失い、カードをランダムに3枚アップグレードする。",
            f"{prefix}SPIRALING_WHIRLPOOL.pages.INITIAL.options.HAYATO.title": "[white]完成した記録を一つ解き、その構造を逆算する。[/white]",
            f"{prefix}SUNKEN_STATUE.pages.INITIAL.options.MUQING.description": "最大HPを[red]7[/red]失い、より多くのゴールドを得る。",
            f"{prefix}RANWID_THE_ELDER.pages.RYOMA.options.CHOOSE_RELIC.title": "続ける。",
        },
        "zhs": {
            f"{prefix}TRIAL.pages.INITIAL.options.RYOMA.title": "[red]你们都有罪[/red]",
            f"{prefix}TRIAL.pages.RYOMA.options.START_FIGHT.title": "[red][b]打断审判。[/b][/red]",
            f"{prefix}THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.HAYATO.title": "[white]换条路走。[/white]",
            f"{prefix}THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.HAYATO.description": "支付[red]35[/red]金币，获得[gold]盖塔冷萃[/gold]。",
            f"{prefix}BYRDONIS_NEST.pages.INITIAL.options.MUQING_LOCKED.description": "需要至少1张可升级牌。",
            f"{prefix}BYRDONIS_NEST.pages.INITIAL.options.MUQING.description": "失去[red]6[/red]点生命，随机升级3张牌。",
            f"{prefix}SPIRALING_WHIRLPOOL.pages.INITIAL.options.HAYATO.title": "[white]拆开一段完成记录，反推它的结构。[/white]",
            f"{prefix}SUNKEN_STATUE.pages.INITIAL.options.MUQING.description": "失去[red]7[/red]点最大生命，获得更多的金币。",
            f"{prefix}RANWID_THE_ELDER.pages.RYOMA.options.CHOOSE_RELIC.title": "继续。",
        },
    }
    for language, contracts in exact_localization.items():
        for key, expected_text in contracts.items():
            if event_tables[language].get(key) != expected_text:
                raise AssertionError(f"Unexpected {language} localization for {key}.")

    spiral_key = f"{prefix}SPIRALING_WHIRLPOOL.pages.HAYATO.description"
    expected_spiral_tags = (
        "[sine]", "[aqua]", "[/aqua]", "[/sine]",
        "[sine]", "[aqua]", "[/aqua]", "[/sine]",
        "[u]", "[/u]",
        "[sine]", "[aqua]", "[/aqua]", "[/sine]",
    )
    for language in LANGUAGES:
        spiral_text = event_tables[language].get(spiral_key)
        if not isinstance(spiral_text, str):
            raise AssertionError(f"Missing Spiraling Whirlpool text for {language}.")
        if extract_tags(spiral_text) != expected_spiral_tags:
            raise AssertionError(f"Incorrect aqua/sine/u ranges for {language}: {spiral_key}")
        if "[getter_ray]" in spiral_text or "[/getter_ray]" in spiral_text:
            raise AssertionError(f"Water semantics must use [aqua] for {language}: {spiral_key}")

    sunken_option_key = f"{prefix}SUNKEN_STATUE.pages.INITIAL.options.MUQING.description"
    for language in LANGUAGES:
        sunken_option = event_tables[language].get(sunken_option_key)
        if not isinstance(sunken_option, str) or "1.8" in sunken_option:
            raise AssertionError(f"Sunken Statue option must not expose the multiplier for {language}.")

    getter_mandala_keys = (
        "S_G_E_GETTER_MANDALA.pages.INITIAL.description",
        "S_G_E_GETTER_MANDALA.pages.PRIMAL_GETTER.description",
        "S_G_E_GETTER_MANDALA.pages.IGNORE.description",
    )
    for language in LANGUAGES:
        for key in getter_mandala_keys:
            text = event_tables[language].get(key)
            if not isinstance(text, str):
                raise AssertionError(f"Missing Getter Mandala text for {language}: {key}")
            if "[aqua]" in text or "[/aqua]" in text:
                raise AssertionError(
                    f"Getter semantics must use [getter_ray], not [aqua], for {language}: {key}"
                )

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
    validate_rich_text_registration()
    validate_localization()
    print("issue#58 static validation passed")


if __name__ == "__main__":
    main()
