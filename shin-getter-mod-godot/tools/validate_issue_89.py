#!/usr/bin/env python3
"""Static regression gate for issue#89 third-batch event invasions."""

from __future__ import annotations

import json
import re
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
LOC = ROOT / "ShinGetterMod/localization"
LANGUAGES = ("zhs", "eng", "jpn")

CARD_TYPES = (
    "SGC_PetalBreakthrough",
    "SGC_RescheduleTicket",
    "SGC_PressureBreath",
    "SGC_WispCoordinate",
)
RELIC_TYPES = ("SGR_BeaconPrism", "SGR_MycelialSilencer")
POTION_TYPES = ("SGR_LuminescentPulse", "SGR_PhaseCoolant", "SGR_AdaptiveInk")

EVENT_OPTIONS = {
    "WELCOME_TO_WONGOS": ("HAYATO",),
    "TRASH_HEAP": ("BENKEI",),
    "TINKER_TIME": ("HAYATO",),
    "REFLECTIONS": ("TRIPLE_UNITY",),
    "DOORS_OF_LIGHT_AND_DARK": ("RYOMA",),
    "WELLSPRING": ("RYOMA",),
    "ROOM_FULL_OF_CHEESE": ("BENKEI",),
    "BUGSLAYER": ("BENKEI",),
    "RELIC_TRADER": ("HAYATO",),
    "ENDLESS_CONVEYOR": ("HAYATO",),
    "UNREST_SITE": ("RYOMA", "BENKEI_BREATH"),
    "LOST_WISP": ("BENKEI", "HAYATO"),
    "DROWNING_BEACON": ("RYOMA", "BENKEI_PRISM"),
    "LUMINOUS_CHOIR": ("RYOMA", "HAYATO"),
    "COLOSSAL_FLOWER": ("HAYATO", "RYOMA"),
    "THE_FUTURE_OF_POTIONS": ("HAYATO",),
    "ABYSSAL_BATHS": ("TRIPLE_COOLANT",),
    "WATERLOGGED_SCRIPTORIUM": ("HAYATO_ADAPTATION", "HAYATO_INK"),
}

# Audited from the current "事件分支" section of all 48 original-event notes.
# The three deprecated Wood Carvings routes are tracked separately so the
# historical 62-option audit cannot accidentally re-enable discarded designs.
EVENT_NOTE_ROUTE_LABELS = {
    "镜中倒影 影倒中镜": ("三体同心",),
    "长者兰伟德": ("龙马",),
    "这个还是那个？": ("龙马", "隼人"),
    "遗忘之墓": ("三体协同",),
    "遗物交换商": ("皇帝的碎片",),
    "迷失鬼火": ("弁庆", "隼人"),
    "被寄生的自动机械": ("隼人",),
    "蓝宝石种子": ("进化共鸣",),
    "螺旋漩涡": ("隼人",),
    "蘑菇饥渴": ("弁庆",),
    "药水的未来？": ("隼人",),
    "药水快递员": ("龙马",),
    "茶艺大师": ("龙马", "弁庆"),
    "色彩哲学家": (),
    "自助指南": ("龙马",),
    "玩偶室": ("隼人",),
    "脑蛭": ("三体协同",),
    "真理石板": ("隼人",),
    "灵魂嫁接者": ("三体同心",),
    "满屋芝士": ("弁庆",),
    "熔合者": ("龙马", "弁庆"),
    "滑脚木桥": ("龙马", "隼人"),
    "淹水金库": ("隼人",),
    "混沌芳香": ("隼人",),
    "淹水灯塔": ("龙马", "弁庆"),
    "泉水": ("盖塔射线路线",),
    "深渊浴场": ("三体协同",),
    "沉没雕像": ("弁庆",),
    "永恒之石": ("弁庆",),
    "水漫缮写室": ("研究路线", "研究路线"),
    "欢迎来到旺购百货": ("顾客资格",),
    "木雕": ("三体同心",),
    "无尽传送带": ("隼人",),
    "害虫杀手": ("弁庆",),
    "无休之处": ("龙马", "弁庆"),
    "打造时间": ("研究路线",),
    "巨大花卉": ("隼人", "龙马"),
    "审判": ("龙马",),
    "多尼斯异鸟巢": ("弁庆", "龙马"),
    "垃圾堆": ("弁庆",),
    "圆桌茶会": ("龙马",),
    "低语空谷": ("隼人",),
    "冷光合唱团": ("龙马", "隼人"),
    "共生体": ("弁庆",),
    "传说是真的": ("龙马", "隼人"),
    "修禅织网者": ("弁庆",),
    "光与暗的门扉": ("龙马",),
    "人形洞穴之地": ("真盖塔龙",),
}

DEPRECATED_EVENT_NOTE_ROUTE_LABELS = {
    "木雕": ("龙马", "隼人", "弁庆"),
}

ROUTE_LABEL_TRANSLATIONS = {
    "龙马": {"zhs": "龙马", "eng": "Ryoma", "jpn": "竜馬"},
    "隼人": {"zhs": "隼人", "eng": "Hayato", "jpn": "隼人"},
    "弁庆": {"zhs": "弁庆", "eng": "Benkei", "jpn": "弁慶"},
    "三体协同": {"zhs": "三体协同", "eng": "Triple Coordination", "jpn": "三体協同"},
    "三体同心": {"zhs": "三体同心", "eng": "Triple Unity", "jpn": "三体一心"},
    "皇帝的碎片": {"zhs": "皇帝的碎片", "eng": "Emperor's Fragment", "jpn": "皇帝の欠片"},
    "进化共鸣": {"zhs": "进化共鸣", "eng": "Evolution Resonance", "jpn": "進化共鳴"},
    "盖塔射线路线": {"zhs": "盖塔射线路线", "eng": "Getter Ray Route", "jpn": "ゲッター線ルート"},
    "研究路线": {"zhs": "研究路线", "eng": "Research Route", "jpn": "研究ルート"},
    "顾客资格": {"zhs": "顾客资格", "eng": "Customer Credentials", "jpn": "顧客資格"},
    "真盖塔龙": {"zhs": "真盖塔龙", "eng": "Shin Getter Dragon", "jpn": "真ゲッタードラゴン"},
}

RUNTIME_ROUTE_LABELS = {
    ("TEA_MASTER", "RYOMA"): "龙马",
    ("TEA_MASTER", "BENKEI"): "弁庆",
    ("SLIPPERY_BRIDGE", "RYOMA"): "龙马",
    ("SLIPPERY_BRIDGE", "HAYATO"): "隼人",
    ("SPIRIT_GRAFTER", "TRIPLE_UNITY"): "三体同心",
    ("WOOD_CARVINGS", "TRIPLE_CARVING"): "三体同心",
    ("THIS_OR_THAT", "RYOMA"): "龙马",
    ("THIS_OR_THAT", "HAYATO"): "隼人",
    ("AMALGAMATOR", "RYOMA"): "龙马",
    ("AMALGAMATOR", "BENKEI"): "弁庆",
    ("BYRDONIS_NEST", "BENKEI"): "弁庆",
    ("BYRDONIS_NEST", "RYOMA"): "龙马",
    ("INFESTED_AUTOMATON", "HAYATO"): "隼人",
    ("THE_LEGENDS_WERE_TRUE", "RYOMA"): "龙马",
    ("THE_LEGENDS_WERE_TRUE", "HAYATO"): "隼人",
    ("TRIAL", "RYOMA"): "龙马",
    ("SUNKEN_STATUE", "BENKEI"): "弁庆",
    ("SPIRALING_WHIRLPOOL", "HAYATO"): "隼人",
    ("ROUND_TEA_PARTY", "RYOMA"): "龙马",
    ("RANWID_THE_ELDER", "RYOMA"): "龙马",
    ("WELCOME_TO_WONGOS", "HAYATO"): "顾客资格",
    ("TRASH_HEAP", "BENKEI"): "弁庆",
    ("TINKER_TIME", "HAYATO"): "研究路线",
    ("REFLECTIONS", "TRIPLE_UNITY"): "三体同心",
    ("DOORS_OF_LIGHT_AND_DARK", "RYOMA"): "龙马",
    ("WELLSPRING", "RYOMA"): "盖塔射线路线",
    ("ROOM_FULL_OF_CHEESE", "BENKEI"): "弁庆",
    ("BUGSLAYER", "BENKEI"): "弁庆",
    ("RELIC_TRADER", "HAYATO"): "皇帝的碎片",
    ("ENDLESS_CONVEYOR", "HAYATO"): "隼人",
    ("UNREST_SITE", "RYOMA"): "龙马",
    ("UNREST_SITE", "BENKEI_BREATH"): "弁庆",
    ("LOST_WISP", "BENKEI"): "弁庆",
    ("LOST_WISP", "HAYATO"): "隼人",
    ("DROWNING_BEACON", "RYOMA"): "龙马",
    ("DROWNING_BEACON", "BENKEI_PRISM"): "弁庆",
    ("LUMINOUS_CHOIR", "RYOMA"): "龙马",
    ("LUMINOUS_CHOIR", "HAYATO"): "隼人",
    ("COLOSSAL_FLOWER", "HAYATO"): "隼人",
    ("COLOSSAL_FLOWER", "RYOMA"): "龙马",
    ("THE_FUTURE_OF_POTIONS", "HAYATO"): "隼人",
    ("ABYSSAL_BATHS", "TRIPLE_COOLANT"): "三体协同",
    ("WATERLOGGED_SCRIPTORIUM", "HAYATO_ADAPTATION"): "研究路线",
    ("WATERLOGGED_SCRIPTORIUM", "HAYATO_INK"): "研究路线",
}

RESULT_ROUTES = {
    event: tuple(route for route in routes if not (
        (event == "TINKER_TIME" and route == "HAYATO")
        or (event == "ENDLESS_CONVEYOR" and route == "HAYATO")
    ))
    for event, routes in EVENT_OPTIONS.items()
}

SELECTION_ROUTES = {
    ("REFLECTIONS", "TRIPLE_UNITY"),
    ("DOORS_OF_LIGHT_AND_DARK", "RYOMA"),
    ("ROOM_FULL_OF_CHEESE", "BENKEI"),
    ("BUGSLAYER", "BENKEI"),
    ("UNREST_SITE", "RYOMA"),
    ("LUMINOUS_CHOIR", "RYOMA"),
    ("LUMINOUS_CHOIR", "HAYATO"),
}

CARD_REGIONS = {
    "s_g_c_petal_breakthrough": (506, 1346, 250, 190),
    "s_g_c_reschedule_ticket": (758, 1346, 250, 190),
    "s_g_c_pressure_breath": (1010, 1346, 250, 190),
    "s_g_c_wisp_coordinate": (1262, 1346, 250, 190),
}

ITEM_REGIONS = {
    "s_g_r_beacon_prism": (256, 256, 128, 128),
    "s_g_r_mycelial_silencer": (384, 256, 128, 128),
    "s_g_r_luminescent_pulse": (512, 256, 128, 128),
    "s_g_r_phase_coolant": (640, 256, 128, 128),
    "s_g_r_adaptive_ink": (0, 384, 128, 128),
}


def require(text: str, *needles: str) -> None:
    for needle in needles:
        if needle not in text:
            raise AssertionError(f"Missing required issue#89 assertion: {needle}")


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


def read_json(path: Path) -> dict[str, str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise AssertionError(f"Localization root must be an object: {path}")
    return data


def read_png_size(path: Path) -> tuple[int, int]:
    header = path.read_bytes()[:24]
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise AssertionError(f"Invalid PNG atlas: {path}")
    return struct.unpack(">II", header[16:24])


def localization_stem(model: str) -> str:
    prefix, name = model.split("_", 1)
    snake_name = re.sub(r"(?<!^)(?=[A-Z])", "_", name).upper()
    expanded_prefix = "_".join(prefix)
    return f"{expanded_prefix}_{snake_name}"


def validate_models_and_pools() -> None:
    card_pool = (SRC / "Models/CardPools/ShinGetterCardPool.cs").read_text(encoding="utf-8")
    entry = (SRC / "Entry.cs").read_text(encoding="utf-8")
    relic_pool = (SRC / "Models/RelicPools/ShinGetterRelicPool.cs").read_text(encoding="utf-8")
    potion_pool = (SRC / "Models/PotionPools/ShinGetterPotionPool.cs").read_text(encoding="utf-8")
    all_cards_body = card_pool.split("GenerateAllCards", 1)[1].split("FilterThroughEpochs", 1)[0]
    if all_cards_body.count("ModelDb.Card<") != 77:
        raise AssertionError("Shin Getter card pool must register exactly 77 cards.")
    require(entry, "ShinGetterMod - loading success! (77 cards)")
    if "loading success! (72 cards)" in entry:
        raise AssertionError("Stale pre-issue#89 card count remains in initialization log.")
    for model in CARD_TYPES:
        require(card_pool, f"ModelDb.Card<{model}>()", f"not {model}")
        source = (SRC / f"Models/Cards/{model}.cs").read_text(encoding="utf-8")
        require(source, "CardRarity.Event")
    for model in RELIC_TYPES:
        require(relic_pool, f"ModelDb.Relic<{model}>()")
        require((SRC / f"Models/Relics/{model}.cs").read_text(encoding="utf-8"), "RelicRarity.Event")
    for model in POTION_TYPES:
        require(potion_pool, f"ModelDb.Potion<{model}>()")
    unlocked_body = potion_pool.split("GetUnlockedPotions", 1)[1].split("private static", 1)[0]
    if any(model in unlocked_body for model in POTION_TYPES):
        raise AssertionError("Event Potions must not enter GetUnlockedPotions().")

    beacon = (SRC / "Models/Relics/SGR_BeaconPrism.cs").read_text(encoding="utf-8")
    require(
        beacon,
        "public override bool ShowCounter => CombatManager.Instance.IsInProgress",
        "public override int DisplayAmount => AvailableThisTurn ? 1 : 0",
        "[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]",
        "Status = value ? RelicStatus.Active : RelicStatus.Disabled",
        "InvokeDisplayAmountChanged()",
        "participants.Contains(Owner.Creature)",
        "Owner.PlayerCombatState?.Phase != PlayerTurnPhase.Play",
        "oldPileType != PileType.Draw",
        "card.Pile?.Type != PileType.Hand",
        "card.Owner != Owner",
        "AvailableThisTurn = false",
        "await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, Owner)",
        "card.Pool is ShinGetterCardPool",
        "CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare",
        "SGC_Strike or SGC_Defend or SGC_GetterBeam or SGC_GetterLaunch",
        "SHIN_GETTER_BEACON_PRISM_COLOR.title",
        "SHIN_GETTER_BEACON_PRISM_COLOR.description",
    )
    if any(obsolete in beacon for obsolete in (
        "AfterPotionUsed", "_potionsUsedThisCombat", "CreatureCmd.Damage", "PowerCmd.Apply<SGP_Ki>"
    )):
        raise AssertionError("Beacon Prism still contains its obsolete Potion/HP/Ki behavior.")
    consume = beacon.index("AvailableThisTurn = false")
    draw = beacon.index("await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, Owner)")
    if consume > draw:
        raise AssertionError("Beacon Prism must consume its once-per-turn trigger before the bonus draw.")
    availability_guard = beacon.index("if (!AvailableThisTurn")
    color_guard = beacon.index("|| HasGetterLineColor(card)")
    if availability_guard > color_guard or color_guard > consume:
        raise AssertionError("Beacon Prism must finish its owner/phase/color gates before consuming the trigger.")
    ki = (SRC / "Models/Powers/SGP_Ki.cs").read_text(encoding="utf-8")
    require(
        ki,
        "ShouldReduceDamage(props, cardSource)",
        "cardSource?.Type == CardType.Status",
        "!props.HasFlag(ValueProp.Unblockable)",
    )
    ki_modifier = ki.split("public override decimal ModifyDamageAdditive(", 1)[1].split(
        "private static bool ShouldReduceDamage", 1
    )[0]
    require(ki_modifier, "if (!ShouldReduceDamage(props, cardSource))", "return -Amount;")
    if ki_modifier.index("if (!ShouldReduceDamage(props, cardSource))") > ki_modifier.index("return -Amount;"):
        raise AssertionError("Ki must apply its source gate before reducing damage.")
    ki_gate = ki.split("private static bool ShouldReduceDamage", 1)[1].split(
        "public override async Task AfterDamageReceived", 1
    )[0]
    require(
        ki_gate,
        "if (!props.HasFlag(ValueProp.Unpowered))",
        "cardSource?.Type == CardType.Status",
        "!props.HasFlag(ValueProp.Unblockable)",
    )
    ki_after_damage = ki.split("public override async Task AfterDamageReceived", 1)[1]
    require(ki_after_damage, "result.UnblockedDamage <= 0", "PowerCmd.Decrement(this)")
    if "props.HasFlag(ValueProp.Unpowered)" in ki_after_damage:
        raise AssertionError("Unpowered HP loss must still consume Ki after real damage is taken.")

    petal = (SRC / "Models/Cards/SGC_PetalBreakthrough.cs").read_text(encoding="utf-8")
    require(
        petal,
        'new IntVar("Times", 1m)',
        "StaticHoverTip.ReplayDynamic",
        'HoverTipFactory.Static(StaticHoverTip.ReplayDynamic, DynamicVars["Times"])',
        "CaptureVigorForManualAttack(ValueProp.Move)",
        "ConsumeCapturedVigor(choiceContext, vigorToConsume)",
        "ConsumeForCardDamage(choiceContext, this, ValueProp.Move)",
        "GetEnchantedReplayCount() < 1",
        'BaseReplayCount += DynamicVars["Times"].IntValue',
        'DynamicVars["Times"].UpgradeValueBy(1m)',
        "CardCmd.Preview(selected)",
    )
    if 'DynamicVars["Replay"]' in petal or 'new IntVar("Replay"' in petal:
        raise AssertionError("Petal Breakthrough must use the ReplayDynamic {Times} contract.")
    pressure = (SRC / "Models/Cards/SGC_PressureBreath.cs").read_text(encoding="utf-8")
    require(pressure, "CombatManager.Instance.History.Entries", "HappenedThisTurn", "UnblockedDamage > 0")
    ticket = (SRC / "Models/Cards/SGC_RescheduleTicket.cs").read_text(encoding="utf-8")
    require(ticket, "CardPilePosition.Bottom", "Draw(choiceContext, 1, Owner)", "drawn.Type == targetType")
    wisp = (SRC / "Models/Cards/SGC_WispCoordinate.cs").read_text(encoding="utf-8")
    require(wisp, ".Take(DynamicVars.Cards.IntValue)", "PileType.Hand", "AsEnumerable().Reverse()")
    for event_card in (pressure, ticket, wisp):
        require(event_card, "CardKeyword.Exhaust")

    getter_claw = (SRC / "Models/Cards/SGC_GetterClaw.cs").read_text(encoding="utf-8")
    claw_exhaust_hook = method_body(getter_claw, "public override async Task AfterCardExhausted")
    require(
        claw_exhaust_hook,
        "PileType.Draw or PileType.Discard or PileType.Play",
        "await CardPileCmd.Add(this, PileType.Hand)",
    )
    if "causedByEthereal ||" in claw_exhaust_hook:
        raise AssertionError("Getter Claw must return for Ethereal exhausts too.")


def validate_event_runtime() -> None:
    service = (SRC / "Events/ShinGetterEventInvasionService.Issue89.cs").read_text(encoding="utf-8")
    switch = (SRC / "Events/ShinGetterEventInvasionService.cs").read_text(encoding="utf-8")
    icon_patch = (SRC / "Patches/ShinGetterEventInvasionPatch.cs").read_text(encoding="utf-8")
    for event in EVENT_OPTIONS:
        method_name = "".join(part.title() for part in event.lower().split("_"))
        if event == "THE_FUTURE_OF_POTIONS":
            require(switch, "BuildTheFutureOfPotionsOptions")
        else:
            require(switch, f"Build{method_name}Options")

    require(
        service,
        "Issue89States.GetOrCreateValue(eventModel)",
        "BuildTinkerTimeOptions(",
        "IReadOnlyList<EventOption> options)",
        '"TINKER_TIME.pages.CHOOSE_CARD_TYPE.options."',
        "if (!isCardTypePage)",
        "state.HasRerolledTinkerTypes = true",
        "state.HasTakenRescheduleTicket = true",
        "EndlessConveyorGenerateOptionsMethod.Invoke",
        "ColossalFlowerDigCountField.SetValue(eventModel, 2)",
        "ApplySpiralEnchantment(rush, 2m)",
        "FreePurchaseActIndices.Count",
        "Math.Min(3",
        "PotionCmd.Discard(potion)",
        "OfferPotion<SGR_LuminescentPulse>",
        "OfferPotion<SGR_PhaseCoolant>",
        "OfferPotion<SGR_AdaptiveInk>",
    )
    require(switch, "BuildTinkerTimeOptions(tinkerTime, options)")
    for resource in (
        "s_g_r_good_citizen_card.tres",
        "s_g_c_saotome_blueprint.tres",
        "s_g_p_evolution.tres",
        "s_g_r_emperors_fragment.tres",
        "s_g_r_research_notes.tres",
    ):
        require(icon_patch, resource)
    for route in (
        ".WELCOME_TO_WONGOS.pages.INITIAL.options.HAYATO",
        ".TINKER_TIME.pages.INITIAL.options.HAYATO",
        ".WELLSPRING.pages.INITIAL.options.RYOMA",
        ".RELIC_TRADER.pages.INITIAL.options.HAYATO",
        ".WATERLOGGED_SCRIPTORIUM.pages.INITIAL.options.HAYATO",
    ):
        require(icon_patch, route)
    if "RollDish" in service:
        raise AssertionError("The Reschedule Ticket route must not reroll the current conveyor dish.")

    role_registry = (SRC / "Models/Cards/ShinGetterCardRoleRegistry.cs").read_text(
        encoding="utf-8"
    )
    require(
        role_registry,
        "internal static bool Has(CardModel card, ShinGetterCardRole role)",
        "types.Contains(card.GetType())",
        "cards.Any(card => Has(card, role))",
    )

    wellspring_options = method_body(
        service, "private static IEnumerable<EventOption> BuildWellspringOptions"
    )
    require(
        wellspring_options,
        "owner.Creature.MaxHp > 3",
        "HasRole(owner, ShinGetterCardRole.GetterRay)",
        "WellspringRyoma(eventModel)",
        "HoverTipFactory.FromCardWithCardHoverTips<SGC_EvolutionResonance>()",
    )
    if "IsRemovable" in wellspring_options:
        raise AssertionError("Wellspring must not require a removable card.")
    wellspring_reward = method_body(service, "private static async Task WellspringRyoma")
    require(
        wellspring_reward,
        "await LoseMaxHp(owner, 3)",
        "await AddEventCard<SGC_EvolutionResonance>(owner)",
        'Finish(eventModel, PageKey("WELLSPRING", "RYOMA"))',
    )
    for obsolete in ("CardSelectCmd", "RemoveFromDeck", "OfferPotion", "SGC_Radiated"):
        if obsolete in wellspring_reward:
            raise AssertionError(f"Obsolete Wellspring reward remains: {obsolete}")
    require(icon_patch, "private const string EvolutionIcon", "return EvolutionIcon")

    bugslayer_reward = method_body(service, "private static async Task BugslayerBenkei")
    require(
        bugslayer_reward,
        "await LoseHp(owner, 5)",
        "IsBugslayerRushCandidate",
        "CardCmd.Upgrade(rush, CardPreviewStyle.EventLayout)",
        "ApplySpiralEnchantment(rush, 2m)",
        'Finish(eventModel, PageKey("BUGSLAYER", "BENKEI"))',
    )
    for obsolete in ("Exterminate", "Squash", "FromChooseACardScreen", "CardPileCmd.Add"):
        if obsolete in bugslayer_reward:
            raise AssertionError(f"Obsolete Bugslayer event-card reward remains: {obsolete}")

    unrest_options = method_body(
        service, "private static IEnumerable<EventOption> BuildUnrestSiteOptions"
    )
    require(
        unrest_options,
        "owner.Deck.Cards.Any(card => card is SGC_Ki)",
        "UnrestSiteRyoma(eventModel)",
        '"RYOMA"',
        "HoverTipFactory.FromCardWithCardHoverTips<SGC_HotBlood>()",
        "UnrestSiteBenkeiBreath(eventModel)",
    )
    for obsolete in ("UnrestSiteBenkei(eventModel)", "owner.Creature.MaxHp > 5"):
        if obsolete in unrest_options:
            raise AssertionError(f"Obsolete Unrest Site rest route remains: {obsolete}")
    unrest_reward = method_body(service, "private static async Task UnrestSiteRyoma")
    require(
        unrest_reward,
        'new CardSelectorPrefs(SelectionKey("UNREST_SITE", "RYOMA"), 1)',
        "card => card is SGC_Ki",
        "owner.RunState.CreateCard<SGC_HotBlood>(owner)",
        "if (ki.IsUpgraded)",
        "CardCmd.Upgrade(hotBlood)",
        "await CardCmd.Transform(ki, hotBlood)",
        'Finish(eventModel, PageKey("UNREST_SITE", "RYOMA"))',
    )
    for obsolete in ("LoseMaxHp", "CreatureCmd.Heal", "RelicCmd.Obtain"):
        if obsolete in unrest_reward:
            raise AssertionError(f"Obsolete Unrest Site cost or reward remains: {obsolete}")

    beacon_options = method_body(
        service, "private static IEnumerable<EventOption> BuildDrowningBeaconOptions"
    )
    require(
        beacon_options,
        "owner.Deck.Cards.Any(IsEvolutionUpgradeCandidate)",
        "DrowningBeaconRyoma(eventModel)",
        '"RYOMA"',
        "DrowningBeaconPrism(eventModel)",
        '"BENKEI_PRISM"',
    )
    for obsolete in ("BENKEI_GLOWWATER", "GlowwaterPotion", "CurrentHp > 12"):
        if obsolete in beacon_options:
            raise AssertionError(f"Obsolete Drowning Beacon route remains: {obsolete}")
    beacon_reward = method_body(service, "private static Task DrowningBeaconRyoma")
    require(
        beacon_reward,
        "owner.Deck.Cards.Where(IsEvolutionUpgradeCandidate).ToList()",
        "eventModel.Rng.NextItem(candidates)",
        "CardCmd.Upgrade(selected, CardPreviewStyle.EventLayout)",
        'Finish(eventModel, PageKey("DROWNING_BEACON", "RYOMA"))',
    )
    for obsolete in ("LoseHp", "OfferPotion", "RelicCmd.Obtain", "GlowwaterPotion"):
        if obsolete in beacon_reward:
            raise AssertionError(f"Obsolete Drowning Beacon reward remains: {obsolete}")
    evolution_candidate = method_body(
        service, "private static bool IsEvolutionUpgradeCandidate"
    )
    require(
        evolution_candidate,
        "card.IsUpgradable",
        "ShinGetterCardRoleRegistry.Has(card, ShinGetterCardRole.Evolution)",
    )

    future_options = method_body(
        service, "private static IEnumerable<EventOption> BuildTheFutureOfPotionsOptions"
    )
    require(
        future_options,
        "owner.Potions.Any()",
        "HasRole(owner, ShinGetterCardRole.ResearchEvolution)",
        "owner.GetRelic<SGR_ResearchNotes>() != null",
    )
    if "owner.Gold" in future_options:
        raise AssertionError("The Future of Potions route must not require Gold.")
    future_reward = method_body(
        service, "private static async Task TheFutureOfPotionsHayato"
    )
    require(
        future_reward,
        "await PotionCmd.Discard(potion)",
        "await OfferPotion<SGR_LuminescentPulse>(owner)",
        'Finish(eventModel, PageKey("THE_FUTURE_OF_POTIONS", "HAYATO"))',
    )
    if "LoseGold" in future_reward:
        raise AssertionError("The Future of Potions route must not spend Gold.")

    abyssal_options = method_body(
        service, "private static IEnumerable<EventOption> BuildAbyssalBathsOptions"
    )
    require(
        abyssal_options,
        "bool hasEvolution = HasRole(owner, ShinGetterCardRole.Evolution)",
        "hasEvolution,",
        "AbyssalBathsTripleCoolant(eventModel)",
        '"ABYSSAL_BATHS"',
        '"TRIPLE_COOLANT"',
        "HoverTipFactory.FromPotion(ModelDb.Potion<SGR_PhaseCoolant>())",
    )
    if abyssal_options.count("CreateConditionalOption(") != 1:
        raise AssertionError("Abyssal Baths must expose exactly one Shin Getter route.")
    for obsolete in ("CurrentHp", "TRIPLE_REFINING", "SGC_Radiated"):
        if obsolete in abyssal_options:
            raise AssertionError(f"Obsolete Abyssal Baths option contract remains: {obsolete}")

    abyssal_reward = method_body(
        service, "private static async Task AbyssalBathsTripleCoolant"
    )
    require(
        abyssal_reward,
        "await CreatureCmd.GainMaxHp(owner.Creature, 2m)",
        "await OfferPotion<SGR_PhaseCoolant>(owner)",
        'Finish(eventModel, PageKey("ABYSSAL_BATHS", "TRIPLE_COOLANT"))',
    )
    for obsolete in ("LoseHp", "AddEventCard", "SetState", "SGC_Radiated"):
        if obsolete in abyssal_reward:
            raise AssertionError(f"Obsolete Abyssal Baths reward remains: {obsolete}")
    reward_order = (
        abyssal_reward.index("CreatureCmd.GainMaxHp"),
        abyssal_reward.index("OfferPotion<SGR_PhaseCoolant>"),
        abyssal_reward.index("Finish(eventModel"),
    )
    if reward_order != tuple(sorted(reward_order)):
        raise AssertionError("Abyssal Baths must gain Max HP, offer coolant, then finish.")
    if "AbyssalBathsTripleRefining" in service:
        raise AssertionError("The discarded reverse-refining route must not remain in runtime code.")

    citizen = (SRC / "Models/Relics/SGR_GoodCitizenCard.cs").read_text(encoding="utf-8")
    require(citizen, "[SavedProperty]", "FreePurchaseActIndices", "goldSpent == 0")
    if not re.search(r"FreePurchaseActIndices\.Add\(Owner\.RunState\.CurrentActIndex\)", citizen):
        raise AssertionError("Free purchases must persist their act indices.")


def validate_route_label_audit() -> None:
    if len(EVENT_NOTE_ROUTE_LABELS) != 48:
        raise AssertionError("The original-event route audit must cover exactly 48 notes.")
    active_count = sum(len(labels) for labels in EVENT_NOTE_ROUTE_LABELS.values())
    deprecated_count = sum(len(labels) for labels in DEPRECATED_EVENT_NOTE_ROUTE_LABELS.values())
    if active_count != 59 or active_count + deprecated_count != 62:
        raise AssertionError(
            "The note audit must contain 59 current routes and 3 explicitly deprecated routes."
        )
    if EVENT_NOTE_ROUTE_LABELS["色彩哲学家"]:
        raise AssertionError("Colorful Philosophers must remain documented with no invasion route.")
    if DEPRECATED_EVENT_NOTE_ROUTE_LABELS != {"木雕": ("龙马", "隼人", "弁庆")}:
        raise AssertionError("Only the three discarded Wood Carvings pilot routes are deprecated.")
    active_labels = {
        label for labels in EVENT_NOTE_ROUTE_LABELS.values() for label in labels
    }
    if active_labels - set(ROUTE_LABEL_TRANSLATIONS):
        raise AssertionError("Every audited route label must have a three-language translation.")
    if len(RUNTIME_ROUTE_LABELS) != 44:
        raise AssertionError("The current runtime must expose exactly 44 Shin Getter routes.")
    if set(RUNTIME_ROUTE_LABELS.values()) - active_labels:
        raise AssertionError("Runtime route labels must come from the current event notes.")


def expected_event_keys() -> set[str]:
    prefix = "SHIN_GETTER_EVENT_INVASION"
    keys: set[str] = {
        f"{prefix}.WELCOME_TO_WONGOS.pages.INITIAL.options.TRANSACTION_SEALED.title",
        f"{prefix}.WELCOME_TO_WONGOS.pages.INITIAL.options.TRANSACTION_SEALED.description",
    }
    for event, routes in EVENT_OPTIONS.items():
        for route in routes:
            base = f"{prefix}.{event}.pages.INITIAL.options.{route}"
            keys.update((f"{base}.title", f"{base}.description"))
            locked = f"{base}_LOCKED"
            keys.update((f"{locked}.title", f"{locked}.description"))
        for route in RESULT_ROUTES[event]:
            keys.add(f"{prefix}.{event}.pages.{route}.description")
    for event, route in SELECTION_ROUTES:
        keys.add(f"{prefix}.{event}.pages.{route}.selectionPrompt")
    keys.update(
        {
            f"{prefix}.RELIC_TRADER.pages.{page}.{suffix}"
            for page in ("CHOOSE_OWNED", "CHOOSE_REPLACEMENT")
            for suffix in (
                "description",
                "options.RELIC.title",
                "options.RELIC.description",
                "options.BACK.title",
                "options.BACK.description",
            )
        }
    )
    keys.update(
        {
            f"{prefix}.THE_FUTURE_OF_POTIONS.pages.CHOOSE_POTION.description",
            f"{prefix}.THE_FUTURE_OF_POTIONS.pages.CHOOSE_POTION.options.POTION.title",
            f"{prefix}.THE_FUTURE_OF_POTIONS.pages.CHOOSE_POTION.options.POTION.description",
        }
    )
    return keys


def validate_localization() -> None:
    expected_cards = {localization_stem(model) for model in CARD_TYPES}
    expected_relics = {localization_stem(model) for model in RELIC_TYPES}
    expected_potions = {localization_stem(model) for model in POTION_TYPES}
    event_keys = expected_event_keys()

    tables: dict[str, dict[str, dict[str, str]]] = {}
    for language in LANGUAGES:
        tables[language] = {
            name: read_json(LOC / language / f"{name}.json")
            for name in ("cards", "relics", "potions", "events", "static_hover_tips")
        }
        for card in expected_cards:
            require("\n".join(tables[language]["cards"]), f"{card}.title", f"{card}.description")
        for relic in expected_relics:
            require("\n".join(tables[language]["relics"]), f"{relic}.title", f"{relic}.description")
        for potion in expected_potions:
            require("\n".join(tables[language]["potions"]), f"{potion}.title", f"{potion}.description")
        missing = event_keys - set(tables[language]["events"])
        if missing:
            raise AssertionError(f"Missing {language} issue#89 event keys: {sorted(missing)}")

    for name in ("cards", "relics", "potions", "events", "static_hover_tips"):
        reference = set(tables[LANGUAGES[0]][name])
        for language in LANGUAGES[1:]:
            if set(tables[language][name]) != reference:
                raise AssertionError(f"{name}.json key mismatch for {language}")

    route_title_pattern = re.compile(
        r"^SHIN_GETTER_EVENT_INVASION\.([A-Z0-9_]+)\.pages\.INITIAL\.options\."
        r"([A-Z0-9_]+)\.title$"
    )
    expected_route_title_keys = {
        f"SHIN_GETTER_EVENT_INVASION.{event}.pages.INITIAL.options.{route}.title"
        for event, route in RUNTIME_ROUTE_LABELS
    }
    separators = {"zhs": "：", "eng": ": ", "jpn": "："}
    for language in LANGUAGES:
        events = tables[language]["events"]
        actual_route_title_keys = set()
        for key in events:
            match = route_title_pattern.fullmatch(key)
            if match is None:
                continue
            route = match.group(2)
            if route != "TRANSACTION_SEALED" and not route.endswith("_LOCKED"):
                actual_route_title_keys.add(key)
        if actual_route_title_keys != expected_route_title_keys:
            missing = sorted(expected_route_title_keys - actual_route_title_keys)
            extra = sorted(actual_route_title_keys - expected_route_title_keys)
            raise AssertionError(
                f"Unexpected {language} runtime route-title set; missing={missing}, extra={extra}"
            )

        for (event, route), authoritative_label in RUNTIME_ROUTE_LABELS.items():
            base = f"SHIN_GETTER_EVENT_INVASION.{event}.pages.INITIAL.options.{route}"
            expected_prefix = (
                ROUTE_LABEL_TRANSLATIONS[authoritative_label][language]
                + separators[language]
            )
            title = events[f"{base}.title"]
            if not title.startswith(expected_prefix) or title == expected_prefix:
                raise AssertionError(
                    f"Route title must use 'route: content' for {language}: {base}.title"
                )
            locked_key = f"{base}_LOCKED.title"
            if locked_key in events and not events[locked_key].startswith(expected_prefix):
                raise AssertionError(
                    f"Locked route title must retain its route label for {language}: {locked_key}"
                )

    reopened_exact = {
        "zhs": {
            "DROWNING_BEACON.pages.INITIAL.options.RYOMA.description":
                "随机升级1张具备[getter_ray]进化[/getter_ray]定位且可升级的卡牌。",
            "DROWNING_BEACON.pages.INITIAL.options.RYOMA_LOCKED.description":
                "需要1张具备[getter_ray]进化[/getter_ray]定位且可升级的卡牌。",
            "WELLSPRING.pages.INITIAL.options.RYOMA.description":
                "失去[red]3[/red]点最大生命，将1张[gold]进化共鸣[/gold]加入牌组。",
            "WELLSPRING.pages.INITIAL.options.RYOMA_LOCKED.description":
                "需要多于3点最大生命和盖塔射线卡牌。",
            "BUGSLAYER.pages.INITIAL.options.BENKEI.description":
                "失去[red]5[/red]点生命，升级并附魔1张盖塔冲撞。",
            "UNREST_SITE.pages.INITIAL.options.RYOMA.description":
                "选择1张[gold]气势[/gold]，将其变化为[gold]热血[/gold]。",
            "UNREST_SITE.pages.INITIAL.options.RYOMA_LOCKED.description":
                "需要至少1张[gold]气势[/gold]。",
            "UNREST_SITE.pages.RYOMA.selectionPrompt": "选择1张气势变化为热血。",
            "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.HAYATO.description":
                "选择消耗1瓶药水，获得[gold]荧光脉冲剂[/gold]。",
            "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.HAYATO_LOCKED.description":
                "需要至少1瓶药水，以及研究或进化卡牌或研究笔记。",
        },
        "eng": {
            "DROWNING_BEACON.pages.INITIAL.options.RYOMA.description":
                "Upgrade 1 random upgradable card with the [getter_ray]Evolution[/getter_ray] role.",
            "DROWNING_BEACON.pages.INITIAL.options.RYOMA_LOCKED.description":
                "Requires an upgradable card with the [getter_ray]Evolution[/getter_ray] role.",
            "WELLSPRING.pages.INITIAL.options.RYOMA.description":
                "Lose [red]3[/red] Max HP. Add 1 [gold]Evolution Resonance[/gold] to your deck.",
            "WELLSPRING.pages.INITIAL.options.RYOMA_LOCKED.description":
                "Requires more than 3 Max HP and a Getter Ray card.",
            "BUGSLAYER.pages.INITIAL.options.BENKEI.description":
                "Lose [red]5[/red] HP. Upgrade and enchant 1 Getter Rush.",
            "UNREST_SITE.pages.INITIAL.options.RYOMA.description":
                "Choose 1 [gold]Spirit[/gold] and transform it into [gold]Valor[/gold].",
            "UNREST_SITE.pages.INITIAL.options.RYOMA_LOCKED.description":
                "Requires at least 1 [gold]Spirit[/gold].",
            "UNREST_SITE.pages.RYOMA.selectionPrompt":
                "Choose 1 Spirit to transform into Valor.",
            "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.HAYATO.description":
                "Choose and consume 1 Potion to obtain [gold]Luminescent Pulse[/gold].",
            "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.HAYATO_LOCKED.description":
                "Requires at least 1 Potion and a Research or Evolution card or Research Notes.",
        },
        "jpn": {
            "DROWNING_BEACON.pages.INITIAL.options.RYOMA.description":
                "[getter_ray]進化[/getter_ray]の役割を持つアップグレード可能なカード1枚をランダムにアップグレードする。",
            "DROWNING_BEACON.pages.INITIAL.options.RYOMA_LOCKED.description":
                "[getter_ray]進化[/getter_ray]の役割を持つアップグレード可能なカードが必要。",
            "WELLSPRING.pages.INITIAL.options.RYOMA.description":
                "最大HPを[red]3[/red]失い、[gold]進化共鳴[/gold]1枚をデッキに加える。",
            "WELLSPRING.pages.INITIAL.options.RYOMA_LOCKED.description":
                "4以上の最大HPとゲッター線カードが必要。",
            "BUGSLAYER.pages.INITIAL.options.BENKEI.description":
                "HPを[red]5[/red]失い、ゲッターラッシュ1枚をアップグレードしてエンチャントする。",
            "UNREST_SITE.pages.INITIAL.options.RYOMA.description":
                "[gold]気合[/gold]1枚を選び、[gold]熱血[/gold]へ変化させる。",
            "UNREST_SITE.pages.INITIAL.options.RYOMA_LOCKED.description":
                "[gold]気合[/gold]が1枚以上必要。",
            "UNREST_SITE.pages.RYOMA.selectionPrompt":
                "熱血へ変化させる気合を1枚選ぶ。",
            "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.HAYATO.description":
                "ポーション1本を選んで消費し、[gold]蛍光パルス剤[/gold]を得る。",
            "THE_FUTURE_OF_POTIONS.pages.INITIAL.options.HAYATO_LOCKED.description":
                "ポーション1本以上と、研究・進化カードまたは研究ノートが必要。",
        },
    }
    reopened_result_fragments = {
        "zhs": {
            "DROWNING_BEACON.pages.RYOMA.description": ("青绿与冷白", "原来这玩意儿还能这么用"),
            "WELLSPRING.pages.RYOMA.description": ("进化标记", "一张共鸣记录"),
            "BUGSLAYER.pages.BENKEI.description": ("方法有两种", "名字你们慢慢想"),
            "LUMINOUS_CHOIR.pages.RYOMA.description": ("灼热孢子逆着光扑回", "下回唱小声点"),
            "LUMINOUS_CHOIR.pages.HAYATO.description": ("细菌丝钉在控制台", "每一回合，先让它静一次"),
            "UNREST_SITE.pages.RYOMA.description": ("气势记录推入读槽", "这才叫磨练"),
            "THE_FUTURE_OF_POTIONS.pages.HAYATO.description": ("又把好东西换成一堆参数", "那就别留到下次"),
        },
        "eng": {
            "DROWNING_BEACON.pages.RYOMA.description": ("Teal and cold white collide", "this stuff can do that too"),
            "WELLSPRING.pages.RYOMA.description": ("Evolution sigil", "One Resonance record"),
            "BUGSLAYER.pages.BENKEI.description": ("there are two methods", "Take your time naming the method"),
            "LUMINOUS_CHOIR.pages.RYOMA.description": ("burning spores surge back", "Keep it down next time"),
            "LUMINOUS_CHOIR.pages.HAYATO.description": ("pins a thin strand of mycelium", "Once each turn, silence it first"),
            "UNREST_SITE.pages.RYOMA.description": ("slots a Spirit record", "Now that's training"),
            "THE_FUTURE_OF_POTIONS.pages.HAYATO.description": ("pile of parameters", "don't save it for next time"),
        },
        "jpn": {
            "DROWNING_BEACON.pages.RYOMA.description": ("青緑と冷たい白", "こんな使い方もできる"),
            "WELLSPRING.pages.RYOMA.description": ("進化の印", "共鳴の記録が1枚"),
            "BUGSLAYER.pages.BENKEI.description": ("方法は二つある", "名前はゆっくり考えろ"),
            "LUMINOUS_CHOIR.pages.RYOMA.description": ("燃える胞子が光を逆流", "もっと小さな声で歌え"),
            "LUMINOUS_CHOIR.pages.HAYATO.description": ("細い菌糸を一本だけ操作盤", "毎ターン、最初に一度だけ黙らせる"),
            "UNREST_SITE.pages.RYOMA.description": ("気合の記録を読み取り口", "これが鍛錬ってもんだ"),
            "THE_FUTURE_OF_POTIONS.pages.HAYATO.description": ("数値の山", "次まで取っておくな"),
        },
    }
    event_prefix = "SHIN_GETTER_EVENT_INVASION."
    obsolete_keys = (
        "DROWNING_BEACON.pages.INITIAL.options.BENKEI_GLOWWATER.title",
        "DROWNING_BEACON.pages.INITIAL.options.BENKEI_GLOWWATER.description",
        "DROWNING_BEACON.pages.INITIAL.options.BENKEI_GLOWWATER_LOCKED.title",
        "DROWNING_BEACON.pages.INITIAL.options.BENKEI_GLOWWATER_LOCKED.description",
        "DROWNING_BEACON.pages.BENKEI_GLOWWATER.description",
        "UNREST_SITE.pages.INITIAL.options.BENKEI.title",
        "UNREST_SITE.pages.INITIAL.options.BENKEI.description",
        "UNREST_SITE.pages.INITIAL.options.BENKEI_LOCKED.title",
        "UNREST_SITE.pages.INITIAL.options.BENKEI_LOCKED.description",
        "UNREST_SITE.pages.BENKEI.description",
        "WELLSPRING.pages.RYOMA.selectionPrompt",
    )
    for language in LANGUAGES:
        events = tables[language]["events"]
        for suffix, expected in reopened_exact[language].items():
            key = event_prefix + suffix
            if events.get(key) != expected:
                raise AssertionError(f"Unexpected reopened issue#89 text for {language}: {key}")
        for suffix, fragments in reopened_result_fragments[language].items():
            require(events[event_prefix + suffix], *fragments)
        if any(event_prefix + suffix in events for suffix in obsolete_keys):
            raise AssertionError(f"Obsolete reopened issue#89 localization remains in {language}.")

    for language in LANGUAGES:
        petal = tables[language]["cards"]["S_G_C_PETAL_BREAKTHROUGH.description"]
        if "{Times:diff()}" not in petal or "{Replay" in petal:
            raise AssertionError("Petal Breakthrough localization must use its Times DynamicVar.")
        ticket = tables[language]["cards"]["S_G_C_RESCHEDULE_TICKET.description"]
        if "next turn" in ticket.lower() or "下回合" in ticket or "次のターン" in ticket:
            raise AssertionError("Reschedule Ticket still contains the obsolete next-turn draw effect.")
        event_card_descriptions = (
            ticket,
            tables[language]["cards"]["S_G_C_PRESSURE_BREATH.description"],
            tables[language]["cards"]["S_G_C_WISP_COORDINATE.description"],
        )
        explicit_exhaust_terms = ("[gold]消耗[/gold]", "[gold]Exhaust[/gold]", "[gold]廃棄[/gold]")
        if any(term in description for description in event_card_descriptions for term in explicit_exhaust_terms):
            raise AssertionError(
                f"Event-card rules text must not duplicate the automatic Exhaust keyword in {language}."
            )

    beacon_localization = {
        "zhs": {
            "description": ("每回合", "出牌阶段", "[gold]不同颜色[/gold]", "额外抽1张牌"),
            "flavor": ("淹没的灯塔", "不同海色", "盖塔航线"),
            "tip": ("真盖塔奖励池卡牌", "初始攻击", "状态卡", "先古卡", "事件卡", "诅咒卡", "抽牌堆"),
        },
        "eng": {
            "description": ("first time each turn", "play phase", "[gold]different color[/gold]", "draw 1 additional card"),
            "flavor": ("drowned beacon", "every color of the sea", "Getter's course"),
            "tip": ("reward pool", "Strike", "Status cards", "Ancient cards", "Event cards", "Curse cards", "draw pile"),
        },
        "jpn": {
            "description": ("各ターン", "プレイフェーズ", "[gold]異なる色[/gold]", "追加で1枚引く"),
            "flavor": ("沈んだ灯台", "海の異なる色", "ゲッターの航路"),
            "tip": ("報酬プール", "ストライク", "状態カード", "古代カード", "イベントカード", "呪いカード", "山札"),
        },
    }
    for language in LANGUAGES:
        relics = tables[language]["relics"]
        tips = tables[language]["static_hover_tips"]
        expected = beacon_localization[language]
        require(relics["S_G_R_BEACON_PRISM.description"], *expected["description"])
        require(relics["S_G_R_BEACON_PRISM.flavor"], *expected["flavor"])
        require(tips["SHIN_GETTER_BEACON_PRISM_COLOR.description"], *expected["tip"])
        require(tips, "SHIN_GETTER_BEACON_PRISM_COLOR.title")
        obsolete_fragments = (
            "第N瓶", "Nth Potion", "N本目", "不可阻挡伤害", "unblocked damage",
            "ブロック不能ダメージ", "[gold]气力[/gold]", "[gold]Ki[/gold]", "[gold]気力[/gold]",
        )
        if any(fragment in relics["S_G_R_BEACON_PRISM.description"] for fragment in obsolete_fragments):
            raise AssertionError(f"Beacon Prism still has obsolete Potion text in {language}.")

    abyssal_expected = {
        "zhs": {
            "option": "获得2点最大生命并回复2点生命；获得[gold]相位冷却液[/gold]。",
            "locked": "需要进化卡牌。",
            "result": (
                "池水开始往装甲缝里钻，隼人接过操控，把回路切到冷却支线。"
                "龙马的声音从通讯里压过水声：[red]“别把整池都带回来。”[/red]\n\n"
                "水面冒出一瓶带着冷凝雾的液体，装甲也在热流里短暂增生。"
                "隼人压住回流，没有让池水的冲击继续灌进回路，直到仪表归零才将药水收入箱内。\n\n"
                "[white]“辐射留不留，轮到我们自己决定。”[/white]"
            ),
        },
        "eng": {
            "option": "Gain 2 Max HP and heal 2 HP. Obtain [gold]Phase Coolant[/gold].",
            "locked": "Requires an Evolution card.",
            "result": (
                "Bathwater begins seeping into the gaps in the armor. Hayato takes the controls "
                "and switches the circuit to the cooling bypass. Ryoma's voice cuts through the "
                "rushing water over comms: [red]“Don't bring the whole bath back with us.”[/red]\n\n"
                "A bottle of liquid veiled in condensation rises from the surface, while the armor "
                "briefly grows within the heat. Hayato contains the backflow and keeps the force of "
                "the water from flooding the circuit any further. Only after the gauges return to "
                "zero does he store the potion.\n\n"
                "[white]“Whether we keep the radiation is our decision now.”[/white]"
            ),
        },
        "jpn": {
            "option": "最大HPを2得てHPを2回復し、[gold]位相冷却液[/gold]を得る。",
            "locked": "進化カードが必要。",
            "result": (
                "湯が装甲の隙間へ入り込み始める。隼人が操縦を引き継ぎ、回路を冷却用のバイパスへ切り替えた。"
                "水音を押しのけて、通信から竜馬の声が響く：[red]「浴槽ごと持ち帰るなよ。」[/red]\n\n"
                "水面に冷気をまとう液体の瓶が浮かび、装甲も熱流の中で一時的に増生する。"
                "隼人は逆流を抑え、水圧が回路へそれ以上流れ込まないようにした。"
                "計器がゼロへ戻るのを待ってから、薬を格納庫へ収めた。\n\n"
                "[white]「放射を残すかどうかは、俺たち自身で決める。」[/white]"
            ),
        },
    }
    abyssal_base = "SHIN_GETTER_EVENT_INVASION.ABYSSAL_BATHS"
    for language in LANGUAGES:
        events = tables[language]["events"]
        if any("TRIPLE_REFINING" in key for key in events):
            raise AssertionError(f"Discarded reverse-refining localization remains in {language}.")
        expected = abyssal_expected[language]
        actual = {
            "option": events[
                f"{abyssal_base}.pages.INITIAL.options.TRIPLE_COOLANT.description"
            ],
            "locked": events[
                f"{abyssal_base}.pages.INITIAL.options.TRIPLE_COOLANT_LOCKED.description"
            ],
            "result": events[f"{abyssal_base}.pages.TRIPLE_COOLANT.description"],
        }
        if actual != expected:
            raise AssertionError(f"Unexpected authoritative Abyssal Baths text for {language}.")


def validate_resources() -> None:
    if read_png_size(ROOT / "images/atlases/card_atlas_shin_getter_01.png") != (2524, 2524):
        raise AssertionError("Unexpected card atlas dimensions.")
    for atlas in ("sgr_atlas_shin_getter.png", "sgr_outline_atlas_shin_getter.png"):
        if read_png_size(ROOT / f"images/atlases/{atlas}") != (768, 768):
            raise AssertionError(f"Unexpected item atlas dimensions: {atlas}")

    for stem, region in CARD_REGIONS.items():
        path = ROOT / f"images/atlases/card_atlas.sprites/shin_getter/{stem}.tres"
        text = path.read_text(encoding="utf-8")
        require(text, "card_atlas_shin_getter_01.png", f"region = Rect2{region}")

    for stem, region in ITEM_REGIONS.items():
        categories = ("relic",) if stem in ITEM_REGIONS and stem in {
            "s_g_r_beacon_prism", "s_g_r_mycelial_silencer"
        } else ("potion",)
        for category in categories:
            for suffix, atlas in (("", "sgr_atlas_shin_getter.png"), ("_outline", "sgr_outline_atlas_shin_getter.png")):
                folder = f"{category}{suffix}_atlas.sprites"
                path = ROOT / f"images/atlases/{folder}/{stem}.tres"
                text = path.read_text(encoding="utf-8")
                require(text, atlas, f"region = Rect2{region}")

    gate = (ROOT / "tools/validate-mod-resources.gd").read_text(encoding="utf-8")
    for stem in (*CARD_REGIONS, *ITEM_REGIONS):
        require(gate, f"{stem}.tres")
    for language in LANGUAGES:
        require(gate, f"res://ShinGetterMod/localization/{language}/potions.json")

    uid_files = list(ROOT.rglob("*.uid"))
    uid_values = [path.read_text(encoding="utf-8").strip() for path in uid_files]
    if len(uid_values) != len(set(uid_values)):
        raise AssertionError("Duplicate Godot .uid values detected.")
    for model in (*CARD_TYPES, *RELIC_TYPES, *POTION_TYPES):
        path = next(SRC.rglob(f"{model}.cs"))
        if not path.with_suffix(path.suffix + ".uid").is_file():
            raise AssertionError(f"Missing .uid for {model}")


def main() -> None:
    validate_route_label_audit()
    validate_models_and_pools()
    validate_event_runtime()
    validate_localization()
    validate_resources()
    print("issue#89 static validation passed")


if __name__ == "__main__":
    main()
