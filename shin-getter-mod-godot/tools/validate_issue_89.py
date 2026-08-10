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
    "UNREST_SITE": ("BENKEI", "BENKEI_BREATH"),
    "LOST_WISP": ("BENKEI", "HAYATO"),
    "DROWNING_BEACON": ("BENKEI_GLOWWATER", "BENKEI_PRISM"),
    "LUMINOUS_CHOIR": ("RYOMA", "HAYATO"),
    "COLOSSAL_FLOWER": ("HAYATO", "RYOMA"),
    "THE_FUTURE_OF_POTIONS": ("HAYATO",),
    "ABYSSAL_BATHS": ("TRIPLE_REFINING", "TRIPLE_COOLANT"),
    "WATERLOGGED_SCRIPTORIUM": ("HAYATO_ADAPTATION", "HAYATO_INK"),
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
    ("WELLSPRING", "RYOMA"),
    ("ROOM_FULL_OF_CHEESE", "BENKEI"),
    ("BUGSLAYER", "BENKEI"),
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
    if all_cards_body.count("ModelDb.Card<") != 76:
        raise AssertionError("Shin Getter card pool must register exactly 76 cards.")
    require(entry, "ShinGetterMod - loading success! (76 cards)")
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
        "_potionsUsedThisCombat++",
        "ValueProp.Unblockable | ValueProp.Unpowered",
        "PowerCmd.Apply<SGP_Ki>",
    )
    if beacon.index("await CreatureCmd.Damage(") > beacon.index("await PowerCmd.Apply<SGP_Ki>("):
        raise AssertionError("Beacon Prism must pay its HP cost before granting Ki.")
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
        "s_g_c_getter_beam.tres",
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

    citizen = (SRC / "Models/Relics/SGR_GoodCitizenCard.cs").read_text(encoding="utf-8")
    require(citizen, "[SavedProperty]", "FreePurchaseActIndices", "goldSpent == 0")
    if not re.search(r"FreePurchaseActIndices\.Add\(Owner\.RunState\.CurrentActIndex\)", citizen):
        raise AssertionError("Free purchases must persist their act indices.")


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
            for name in ("cards", "relics", "potions", "events")
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

    for name in ("cards", "relics", "potions", "events"):
        reference = set(tables[LANGUAGES[0]][name])
        for language in LANGUAGES[1:]:
            if set(tables[language][name]) != reference:
                raise AssertionError(f"{name}.json key mismatch for {language}")

    for language in LANGUAGES:
        petal = tables[language]["cards"]["S_G_C_PETAL_BREAKTHROUGH.description"]
        if "{Times:diff()}" not in petal or "{Replay" in petal:
            raise AssertionError("Petal Breakthrough localization must use its Times DynamicVar.")
        ticket = tables[language]["cards"]["S_G_C_RESCHEDULE_TICKET.description"]
        if "next turn" in ticket.lower() or "下回合" in ticket or "次のターン" in ticket:
            raise AssertionError("Reschedule Ticket still contains the obsolete next-turn draw effect.")


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
    validate_models_and_pools()
    validate_event_runtime()
    validate_localization()
    validate_resources()
    print("issue#89 static validation passed")


if __name__ == "__main__":
    main()
