"""issue#238 structural regressions; optional actual CLR probe, never game automation."""
from pathlib import Path
import argparse
import json
import re
import subprocess
import sys
from registered_card_contract import validate_registered_cards, ISSUE238_CARDS

ROOT = Path(__file__).resolve().parents[1]
# Current main before this v1.4.0 continuation. Older b0f76260 predates reviewed main fixes.
BASELINE_COMMIT = "26c8da9a907fd9e12d41bf5bf17d24ca5975bfa8"
ROUTES = {
    "WHISPERING_HOLLOW": ["BENKEI"], "SYMBIOTE": ["BENKEI", "HAYATO"],
    "AROMA_OF_CHAOS": ["HAYATO"], "SAPPHIRE_SEED": ["RYOMA"],
    "HUNGRY_FOR_MUSHROOMS": ["BENKEI"], "BRAIN_LEECH": ["TRIPLE_COORDINATION"],
    "FIELD_OF_MAN_SIZED_HOLES": ["HAYATO"], "DOLL_ROOM": ["HAYATO"],
    "ZEN_WEAVER": ["BENKEI", "HAYATO"], "WATERLOGGED_VAULT": ["RYOMA"],
    "POTION_COURIER": ["RYOMA", "HAYATO"],
    "FORGOTTEN_GRAVE": ["TRIPLE_COORDINATION", "RYOMA"],
    "STONE_OF_ETERNITY": ["BENKEI"], "TABLET_OF_TRUTH": ["HAYATO", "BENKEI"],
    "SELF_HELP_BOOK": ["RYOMA"],
}


def legacy_strings(language):
    source = json.loads(read("tools/issue238-legacy-localization.json"))
    translations = json.loads(read("tools/issue238-legacy-translations.json"))
    result = {}
    for entry in source["entries"]:
        short = entry["key"].removeprefix("SHIN_GETTER_EVENT_INVASION.").replace(".pages.", ".").removesuffix(".description")
        value = entry.get(language)
        if language != "zhs" and short in translations:
            value = translations[short][0 if language == "eng" else 1]
        require(bool(value), f"missing authored {language} prose: {entry['key']}")
        result[entry["key"]] = value
    return result


def read(name):
    return (ROOT / name).read_text(encoding="utf-8-sig")


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def contains(text, *needles):
    for needle in needles:
        require(needle in text, f"missing contract: {needle}")


def body(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 0
    for i in range(opening, len(source)):
        if source[i] == "{": depth += 1
        elif source[i] == "}":
            depth -= 1
            if not depth: return source[opening:i + 1]
    raise AssertionError(f"unbalanced method: {signature}")


def persistence_contracts(events):
    state = read("src/Services/ShinGetterPlayerEventState.cs")
    contains(state, 'FieldName = "shin_getter_event_state_v1"', "[JsonRequired]",
             "MaxBytes", "MaxVisits", "ConditionalWeakTable<Player", "ConditionalWeakTable<SerializablePlayer",
             "JsonTypeInfoKind.Object", "SerializablePlayer", "Decode(value)")
    require(state.count("[JsonRequired]") == 8, "every v1 payload/visit field must be required")
    identity = read("src/Events/ShinGetterEventVisitIdentity.cs")
    contains(identity, "{seed}:{owner}:{act}:{coordinate}:{point}:{room}:{model}")
    contains(events, "points.Count - 1, visit, model.Id", "rooms.FindLastIndex", "ReferenceEquals(room, run.BaseRoom)",
             "SHA256.HashData", "card.ToSerializable()", "visit.Candidates.Any(c => !valid.Contains(c))",
             "model.Rng.NextInt(legal.Count)", "legal.RemoveAt(index)", "await SaveFourth(RequireOwner(model))")
    restored = body(events, "internal static bool RestoreFourthCompleted(")
    contains(restored, "IsShinGetter", "model.IsShared", "FourthRouteNames", "Finish(", "return true")
    for token in ("AddFourthCard", "Obtain", "LoseGold", "FourthDamage", "SaveFourth", "NextInt"):
        require(token not in restored, f"completed page must not repeat {token}")
    commit = body(events, "private static async Task<bool> CommitFourth(")
    require(commit.index("await apply()") < commit.index("visit.CompletedRoute = route") < commit.index("await SaveFourth(owner)"),
            "completion must follow all costs/rewards, then verified save")
    contains(commit, "ConfirmedWriteReadbackException", "catch { snapshot.Rollback(); throw; }")
    confirmed = commit[commit.index("catch (ShinGetterEventSaveVerification.ConfirmedWriteReadbackException"):commit.index("catch { snapshot.Rollback()")]
    require("Rollback" not in confirmed and "return false" not in confirmed, "confirmed disk write must not reopen reward")
    save = body(events, "private static async Task SaveFourth(")
    contains(save, "native.Saved += OnSaved", "finally { native.Saved -= OnSaved; }", "manager.CurrentRunSaveTask",
             "LoadRunSave()", "GetTypeInfo<SerializablePlayer>()", "!= expected", "saveProgress: false")
    verifier = read("src/Events/ShinGetterEventSaveVerification.cs")
    contains(verifier, "await write()", "if (!wasSaved())", "readback()", "catch (Exception ex) when (wasSaved())")
    rollback = read("src/Events/ShinGetterEventTransaction.cs")
    contains(rollback, "RestoreRemovedRelicFlag", "RemovedFlag.SetValue(relic, false)", "Props?.Fill", "_saved.Deck",
             "_saved.Rng", "_saved.Odds", "_sharedRelics", "_potions", "_historyValues", "ShinGetterPlayerEventState.Restore")
    require("SyncWithSerializedPlayer" not in rollback, "rollback must not replace complete Player or replay pickup hooks")


def content_contracts():
    pool = read("src/Models/CardPools/ShinGetterCardPool.cs")
    validate_registered_cards(pool)
    contains(read("src/Entry.cs"), "loading success! (82 cards)")
    for model in ISSUE238_CARDS:
        require((ROOT / f"src/Models/Cards/{model}.cs.uid").is_file(), f"missing model UID: {model}")
    changes = read("src/Services/ShinGetterCombatCardChanges.cs")
    contains(changes, "ConditionalWeakTable<Player", "c.Owner == owner", "c.Pile?.IsCombatPile == true",
             "state.Upgrades.TryAdd", "Enchantments.TryAdd", "RemovedExhaust", "Changes.Remove(owner)")
    require("DeckVersion." not in changes, "combat edits must not modify permanent DeckVersion")
    contains(read("src/Patches/ShinGetterCombatCardChangesPatch.cs"), "finally", "runState.Players", "Restore(player)")
    tablet = read("src/Models/Cards/SGC_TabletOfTruth.cs")
    contains(tablet, "base(2, CardType.Power", "HealVar(6m)", "UpgradeValueBy(3m)", "ReferenceEquals(card, this)")
    require(tablet.index('decimal heal =') < tablet.index("upgrade: false") < tablet.index("CreatureCmd.Heal"),
            "Tablet heal must use pre-downgrade value")
    missile = read("src/Models/Cards/SGC_DrillMissile.cs")
    contains(missile, "DamageVar(25m", "UpgradeValueBy(5m)", "GetPower<SGP_ShinForm>() != null", "Apply<SGP_Shade>", "Plays.Remove(this)",
             "Pile?.IsCombatPile != true", "TransformTo<SGC_BrokenDrill>")
    require("ShinGetterForm.Getter2" not in missile and "await Transform(" not in missile, "retired Getter 2 transform bonus")
    wrapper = read("src/Patches/ShinGetterDrillMissilePatch.cs")
    contains(wrapper, "nameof(CardModel.OnPlayWrapper)", "await original;", "await missile.ReplacePlayedCombatInstance()",
             "BeginPlaySequence", "finally { annotations.EndPlaySequence(); }")
    require(wrapper.index("await original;", wrapper.index("private static async Task Complete(Task")) < wrapper.index("await missile.ReplacePlayedCombatInstance()"),
            "Drill replacement must await complete replay/result/after-play wrapper")
    broken = read("src/Models/Cards/SGC_BrokenDrill.cs")
    contains(broken, "base(-1, CardType.Status", "MaxUpgradeLevel => 0", "CardKeyword.Unplayable",
             "ReferenceEquals(card, this)", "AfterCardDrawn(PlayerChoiceContext choiceContext", "AfterCardExhausted(PlayerChoiceContext choiceContext",
             "Pile?.Type == PileType.Hand", "PowerVar<SGP_Wane>(2m)", "PowerVar<SGP_Ki>(1m)",
             "Apply<SGP_Wane>(choiceContext", "Apply<SGP_Ki>(choiceContext")
    require("AfterCardChangedPiles" not in broken and "ThrowingPlayerChoiceContext" not in broken,
            "Broken Drill must use actual draw/exhaust context, not every hand entry")
    require("CardKeyword.Exhaust" not in broken and "CardKeyword.Ethereal" not in broken, "Broken Drill keywords drift")
    annotations = read("src/Models/Cards/SGC_Annotations.cs")
    contains(annotations, "base(1, CardType.Skill", "CardKeyword.Exhaust", 'DynamicVar("Enchantment", 2m)',
             "UpgradeValueBy(1m)", "CanEnchant(card)", "CardType.Skill when card.GainsBlock",
             "card.Owner == Owner", "Cancelable = true", "if (_canceled) return", "PrepareManualPlay",
             "ConsumeManualCancellation", "HasPreparedTarget", "committedSelectionScreenPrompt",
             "previousAmount", "PowerCmd.Apply<VigorPower>")
    require("GainEnergy" not in annotations and "CardPileCmd.Add(this, PileType.Hand)" not in annotations,
            "committed annotations must never refund/return after native hooks")
    preparation = read("src/Patches/ShinGetterAnnotationsPreparationPatch.cs")
    contains(preparation, "typeof(PlayCardAction)", '"MoveNext"', "AsyncStateMachineAttribute", "PrepareResources",
             "PlayPreparedCard", "new GameActionPlayerChoiceContext(action)", "ActionChoiceContext.SetValue(action, context)",
             "card.CanPlay(out _, out _)", "card.IsValidTarget(action.Target)", "action.Cancel();", "return (0, 0)",
             "annotations.ConsumeManualCancellation()", "return Task.CompletedTask")
    preparing = body(preparation, "private static async Task<(int, int)> PrepareAnnotationsResources(")
    require(preparing.index("await annotations.PrepareManualPlay(context)") < preparing.index("card.CanPlay(")
            < preparing.index("await card.SpendResources()"), "manual choice must precede native recheck and cost")
    require("Hook.AfterCardPlayed" not in preparation and "Hook.BeforeCardPlayed" not in preparation,
            "do not replace global card hooks")
    upgrades = body(changes, "internal static void ChangeUpgradeLevels(")
    require(upgrades.index("CardCmd.Downgrade(card)") < upgrades.index("state.RemovedExhaust.Contains(card)")
            < upgrades.index("CardCmd.RemoveKeyword(card, CardKeyword.Exhaust)"),
            "Ember combat keyword must be reapplied after native upgrade reset")
    ember = read("src/Models/Relics/SGR_Ember.cs")
    contains(ember, "AfterCardDrawnEarly", "card.Owner != Owner", "PileType.Hand", "CardType.Attack or CardType.Skill",
             "CardKeyword.Exhaust", "RememberRemovedExhaust", "CardCmd.RemoveKeyword", "_used = false")
    require("AfterCardExhausted" not in ember and "GainEnergy" not in ember, "retired Ember return/increased-cost effect")
    filter_code = read("src/Models/Relics/SGR_SymbioticFilter.cs")
    contains(filter_code, "AfterCardChangedPiles", "CardType.Status", "card.Owner != Owner", "oldPileType == PileType.Hand", "_used = false")
    contains(filter_code, "new BlockingPlayerChoiceContext()")
    contains(filter_code, "AfterCardEnteredCombat", "NewHandInstances", "ReferenceEqualityComparer.Instance",
             "entries.Remove(card)", "oldPileType == PileType.Hand && !newHandInstance")
    require("ThrowingPlayerChoiceContext" not in filter_code, "nested Exhaust hooks may request native choices")
    require(filter_code.index("_used = true") < filter_code.index("await CardCmd.Exhaust") < filter_code.index("PowerCmd.Apply<SGP_Evolution>"), "Filter nested hook re-entry")
    catalyst = read("src/Models/Potions/SGR_MobilityCatalyst.cs")
    contains(catalyst, "PotionRarity.Event", "PotionUsage.CombatOnly", "GetPower<SGP_OpenGet>() == null", "Apply<SGP_OpenGet>")
    require(catalyst.index("Apply<SGP_OpenGet>") < catalyst.index("GainEnergy(1m") < catalyst.index("Draw(choiceContext, 2"), "Catalyst settlement order")
    require("Transform(" not in catalyst, "Catalyst must not change form")
    potion_pool = read("src/Models/PotionPools/ShinGetterPotionPool.cs")
    contains(potion_pool, "ModelDb.Potion<SGR_MobilityCatalyst>()")
    require("MobilityCatalyst" not in body(potion_pool, "public override IEnumerable<PotionModel> GetUnlockedPotions("), "Catalyst in ordinary potion pool")
    for folder, atlas in (("potion_atlas.sprites", "issue238_item_atlas.png"), ("potion_outline_atlas.sprites", "issue238_item_outline_atlas.png")):
        path = f"images/atlases/{folder}/s_g_r_mobility_catalyst.tres"
        contains(read(path), atlas, "region = Rect2(384, 0, 128, 128)")
        contains(read("tools/validate-mod-resources.gd"), "res://" + path)
    card_base = read("src/Models/Cards/ShinGetterEventCardBase.cs")
    contains(card_base, "BetaPortraitPath => PortraitPath")
    require("SGC_RescheduleTicket" not in card_base and "override string PortraitPath" not in card_base,
            "event cards must use their own native resources")
    relic_base = read("src/Models/Relics/ShinGetterEventRelic.cs")
    contains(relic_base, "RelicRarity.Event")
    require("SGR_ResearchNotes" not in relic_base and "override string" not in relic_base,
            "event relics must use their own native resources")


def november_contracts(events):
    source = json.loads(read("tools/issue238-localization.json"))
    authority = source["authority"]
    require(authority["date"] == "2026-11-01" and len(authority["routes"]) == 20, "Nov 1 source scope")
    routes = {(r["event"], r["route"]): r for r in source["routes"]}
    require(set(routes) == {(e, r) for e, names in ROUTES.items() for r in names}, "Nov 1 route identity")
    for expected in authority["routes"]:
        route = routes[(expected["event"], expected["route"])]
        require(route["description"][0] == expected["description"] and route["result"][0] == expected["result"],
                f"Nov 1 Chinese source drift: {expected['event']}/{expected['route']}")
        require(re.fullmatch(r"[a-f0-9]{64}", expected["input_sha256"]), "missing Nov 1 input provenance")
    contains(events, '("SELF_HELP_BOOK", _) => !HasCard<SGC_Annotations>(owner) && owner.Creature.CurrentHp > 3',
             '("SELF_HELP_BOOK", _) => 3', "await FourthDamage(owner, 3);", "FourthDevolution",
             "ModelDb.Enchantment<SGE_Devolution>()")
    devolution = events[events.index("private static bool FourthDevolution("):events.index("private static bool FourthSoulsPower(")]
    contains(devolution, "CardType.Attack", "SGE_Devolution", "CanEnchant(card)")
    brain = routes[("BRAIN_LEECH", "TRIPLE_COORDINATION")]
    require(brain["display_route"] == "RYOMA" and brain["title"][0] == "龙马", "brain route display/save alias")
    contains(read("src/Patches/ShinGetterEventInvasionPatch.cs"),
             '.BRAIN_LEECH.pages.INITIAL.options.TRIPLE_COORDINATION', '!isBrainRyoma &&')
    holes = routes[("FIELD_OF_MAN_SIZED_HOLES", "HAYATO")]["result"][0]
    contains(holes, "二号机单独", "报废的测距探针")
    require("三号机" not in holes and "一号机" not in holes, "retired simultaneous Getter scene")
    stone = routes[("STONE_OF_ETERNITY", "BENKEI")]["result"][0]
    contains(stone, "整台机体", "石角终于卡进", "三处驾驶舱")
    require("先过去" not in stone and "药水" not in stone, "retired separate-pilot/potion scene")
    whisper = routes[("WHISPERING_HOLLOW", "BENKEI")]
    contains(whisper["result"][0], "真盖塔波塞冬", "选中的遗物")
    for route in source["routes"]:
        for field in ("description", "result", "locked"):
            for value in route[field]:
                require("三重旋风" not in value and "钻头导弹" not in value, "retired item names")
    for lang in source["languages"]:
        loc = json.loads(read(f"ShinGetterMod/localization/{lang}/events.json"))
        title = loc["SHIN_GETTER_EVENT_INVASION.BRAIN_LEECH.pages.INITIAL.options.TRIPLE_COORDINATION.title"]
        require(title.startswith("[red]"), "brain route must show Ryoma's color")
        for suffix in ("RYOMA_HP_LOCKED", "RYOMA_OWNED_LOCKED"):
            require(f"SHIN_GETTER_EVENT_INVASION.SELF_HELP_BOOK.pages.INITIAL.options.{suffix}.description" in loc,
                    "self-help lock reason missing")


def prose_contracts():
    fixture = json.loads(read("tools/issue238-legacy-localization.json"))["entries"]
    require(len(fixture) == len({e["key"] for e in fixture}) == 46 and len({e["source"] for e in fixture}) == 32,
            "legacy scope must remain 32 notes / 46 distinct pages")
    for entry in fixture:
        require(re.fullmatch(r"[a-f0-9]{64}", entry["scene_sha256"]), "missing current-scene provenance")
        require(".options." not in entry["key"] and entry["key"].endswith(".description"), "prose sync changed option mechanics")
    for language in ("zhs", "eng", "jpn"):
        updated = legacy_strings(language)
        for table in ("events", "cards", "relics", "potions"):
            current = json.loads(read(f"ShinGetterMod/localization/{language}/{table}.json"))
            baseline = json.loads(subprocess.check_output(["git", "show", f"{BASELINE_COMMIT}:shin-getter-mod-godot/ShinGetterMod/localization/{language}/{table}.json"], cwd=ROOT))
            for key, value in baseline.items():
                expected = updated[key] if table == "events" and key in updated else value
                require(current.get(key) == expected, f"unapproved legacy text/mechanics change: {language}:{key}")
    for tool in ("sync_issue238_localization.py", "sync_issue238_legacy_text.py"):
        subprocess.run([sys.executable, str(ROOT / "tools" / tool), "--check"], check=True)
    prose = read("src/Events/ShinGetterEventReturnProse.cs")
    contains(prose, "ConditionalWeakTable<EventModel", "ConditionalWeakTable<LocString", "copy.AddVariablesFrom(description)",
             "Compositions.TryGetValue(description", "Clear(model)", "composition.Original.GetRawText()", "composition.Prose.GetRawText()")
    legacy_code = read("src/Events/ShinGetterEventInvasionService.Issue89.cs")
    contains(legacy_code, '"TINKER_TIME.pages.CHOOSE_CARD_TYPE.description"', '"ENDLESS_CONVEYOR.pages.INITIAL.description"')
    tinker = body(legacy_code, "private static async Task TinkerTimeHayato(")
    require(tinker.count("TinkerChooseCardTypeMethod.Invoke") == 1, "return prose introduced a second native RNG call")
    require("NextInt" not in tinker and "TakeRandom" not in tinker, "return prose must not draw RNG")
    # New UI must have completion/focus/cleanup and must not pay before returning a choice.
    choice = read("src/Nodes/Events/NShinGetterEventItemChoice.cs")
    contains(choice, "FocusModeEnum.All", "FocusNeighborTop", "FocusNeighborBottom", "GrabFocus", "TrySetResult(-1)",
             "finally", "stack.Remove(screen)", "SignalPlayerChoiceEnded")


def main():
    events = read("src/Events/ShinGetterEventInvasionService.Issue238.cs")
    routes = ROUTES
    require(len(routes) == 15 and sum(map(len, routes.values())) == 20, "fourth batch route quota")
    require("owner.RunState.Players.Count != 1" in events, "single-player guard missing")
    require("disableOnChosen: false" in events, "cancelable routes must not latch WasChosen")
    require("MarkPreFinished" not in events, "ordinary event must not use Ancient pre-finished save")
    require("CommitFourth" in events and "Rollback" in events, "transaction boundary missing")
    require("FinalHp" in read("src/Models/Relics/SGR_ActivatedSapphire.cs"), "final HP-loss boundary")
    require("AfterOsty" in read("src/Patches/ShinGetterActivatedSapphirePatch.cs"), "final mitigation stage")
    triple = read("src/Models/Cards/SGC_TripleCyclone.cs")
    require("WithHitCount(3)" in triple and "i < 3" in triple, "three independent hit/block settlements")
    require("GetPower<SGP_ShinForm>" in triple, "Wane requires actual dragon, not compatible form")
    require("AfterCardDrawnEarly" in read("src/Models/Relics/SGR_Ember.cs"), "Ember must act on draw")
    require("AfterCardChangedPiles" in read("src/Models/Relics/SGR_SymbioticFilter.cs"), "all hand entries")
    for lang in ("zhs", "eng", "jpn"):
        loc = json.loads(read(f"ShinGetterMod/localization/{lang}/events.json"))
        for event, names in routes.items():
            for route in names:
                prefix = f"SHIN_GETTER_EVENT_INVASION.{event}.pages."
                for key in (f"INITIAL.options.{route}.title", f"INITIAL.options.{route}.description",
                            f"INITIAL.options.{route}_LOCKED.title", f"INITIAL.options.{route}_LOCKED.description",
                            f"{route}_RESULT.description"):
                    require(bool(loc.get(prefix + key)), f"{lang}: missing {prefix + key}")
        require(not any("FIELD_OF_MAN_SIZED_HOLES" in k and "THREE_FORMS" in k for k in loc),
                "retired hole route must not return")
    contains(events, "await FourthDamage(owner, 15); await CreatureCmd.GainMaxHp(owner.Creature, 5)",
             "await CreatureCmd.SetCurrentHp(owner.Creature, hpBefore - 12)", "FourthLockedName", "DecipherCount")
    persistence_contracts(events)
    content_contracts()
    november_contracts(events)
    prose_contracts()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--probe", action="store_true", help="Run actual 109 CLR integration probe; no game scene/window")
    args = parser.parse_args()
    if args.probe:
        subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/issue238-probe/issue238-probe.csproj"), "--verbosity", "quiet"], check=True)
    print("issue#238: 15 events / 20 routes; persistence/content/current-prose structural gates PASS (not game acceptance)")


if __name__ == "__main__":
    main()
