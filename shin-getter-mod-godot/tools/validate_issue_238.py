"""issue#238 structural regressions; optional actual CLR probe, never game automation."""
from pathlib import Path
import argparse
import json
import re
import shutil
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
    "ZEN_WEAVER": ["BENKEI", "HAYATO"], "SUNKEN_TREASURY": ["RYOMA"],
    "POTION_COURIER": ["RYOMA", "HAYATO"],
    "GRAVE_OF_THE_FORGOTTEN": ["TRIPLE_COORDINATION", "RYOMA"],
    "STONE_OF_ALL_TIME": ["BENKEI"], "TABLET_OF_TRUTH": ["HAYATO", "BENKEI"],
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


def native_event_contracts(events):
    config = json.loads(read("tools/issue238-native-events.json"))
    shell = shutil.which("pwsh")
    require(shell is not None, "pwsh is required for the actual native ModelDb identity gate")
    result = subprocess.run([shell, "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
                             str(ROOT / "tools/read_issue238_native_event_ids.ps1"), "-ProjectRoot", str(ROOT)],
                            check=True, capture_output=True, text=True, encoding="utf-8")
    observed = json.loads(result.stdout)
    require(observed["native_sha256"] == config["native_sha256"] and not observed["game_started"]
            and not observed["mod_dll_loaded"], "native identity probe boundary drift")
    by_type = {row["type"]: row["entry"] for row in observed["rows"]}
    actual = set(by_type.values())
    require(len(by_type) == len(actual) == 15, "native event identity quota")

    def match(label, configured):
        configured = set(configured)
        require(configured == actual, f"{label}: native ID mismatch; unknown={sorted(configured - actual)}, missing={sorted(actual - configured)}")

    # The authority is the actual DLL method, not a second copy of the product strings.
    match("production FourthEvents", re.findall(r'"([A-Z_]+)"', body(events, "private static readonly HashSet<string> FourthEvents")))
    match("Python ROUTES", ROUTES)
    source = json.loads(read("tools/issue238-localization.json"))
    match("authored routes", (r["event"] for r in source["routes"]))
    match("authority metadata", (r["event"] for r in source["authority"]["routes"]))
    for retired in config["retired"]:
        require(retired["replacement"] == by_type[retired["type"]], "cleanup target differs from actual native ID")
        require(f'"{retired["entry"]}"' not in events, "retired ID in production event dispatch")
        # Reject each of the three historical bad configurations against the real DLL.
        bad = actual - {by_type[retired["type"]]} | {retired["entry"]}
        rejected = False
        try:
            match("retired ID negative baseline", bad)
        except AssertionError:
            rejected = True
        require(rejected, "native ID gate accepted a known historical typo")
        for lang in source["languages"]:
            loc = json.loads(read(f"ShinGetterMod/localization/{lang}/events.json"))
            prefix = f'SHIN_GETTER_EVENT_INVASION.{retired["entry"]}.'
            require(not any(k.startswith(prefix) for k in loc), f"{lang}: retired generated keys remain")
    print("issue#238: 15 actual native ModelDb IDs cross-checked; 3 historical bad IDs rejected (not registered-entry/game acceptance)")


def persistence_contracts(events):
    state = read("src/Services/ShinGetterPlayerEventState.cs")
    contains(state, 'FieldName = "shin_getter_event_state_v1"', "[JsonRequired]",
             "MaxBytes", "MaxVisits", "ConditionalWeakTable<Player", "ConditionalWeakTable<SerializablePlayer",
             "JsonTypeInfoKind.Object", "SerializablePlayer", "Decode(value)")
    require(body(state, "internal sealed class Data").count("[JsonRequired]") == 5
            and body(state, "internal sealed class Visit").count("[JsonRequired]") == 3,
            "the original eight v1 payload/visit fields must remain required")
    identity = read("src/Events/ShinGetterEventVisitIdentity.cs")
    contains(identity, "{seed}:{owner}:{act}:{coordinate}:{point}:{room}:{model}")
    contains(events, "points.Count - 1, visit, modelId", "rooms.FindLastIndex", "ReferenceEquals(room, run.BaseRoom)",
             "SHA256.HashData", "card.ToSerializable()", "visit.Candidates.Any(c => !valid.Contains(c))",
             "model.Rng.NextInt(legal.Count)", "legal.RemoveAt(index)", "await SaveFourth(model)")
    restored = body(events, "internal static bool RestoreFourthState(")
    contains(restored, "IsShinGetter", "model.IsShared", "FourthRouteNames", "Finish(", "return true")
    for token in ("AddFourthCard", "Obtain", "LoseGold", "FourthDamage", "SaveFourth", "NextInt"):
        require(token not in restored, f"completed page must not repeat {token}")
    commit = body(events, "private static async Task<bool> CommitFourth(")
    require(commit.index("await apply()") < commit.index("visit.CompletedRoute = route") < commit.index("await SaveFourth(model)"),
            "completion must follow all costs/rewards, then verified save")
    contains(commit, "ConfirmedWriteReadbackException", "catch { snapshot.Rollback(); throw; }")
    confirmed = commit[commit.index("catch (ShinGetterEventSaveVerification.ConfirmedWriteReadbackException"):commit.index("catch { snapshot.Rollback()")]
    require("Rollback" not in confirmed and "return false" not in confirmed, "confirmed disk write must not reopen reward")
    save = body(events, "private static async Task SaveFourth(")
    contains(save, "native.Saved += OnSaved", "finally { native.Saved -= OnSaved; }", "manager.CurrentRunSaveTask",
             "LoadRunSave()", "Capture(readback.SaveData)", "Compare(expected, actualReadback)", "saveProgress: false")
    verifier = read("src/Events/ShinGetterEventSaveVerification.cs")
    contains(verifier, "await write()", "if (!wasSaved())", "readback()", "catch (Exception ex) when (wasSaved())")
    rollback = read("src/Events/ShinGetterEventTransaction.cs")
    contains(rollback, "RestoreRemovedRelicFlag", "RemovedFlag.SetValue(relic, false)", "Props?.Fill", "_saved.Deck",
             "_saved.Rng", "_saved.Odds", "_sharedRelics", "_potions", "_historyValues", "ShinGetterPlayerEventState.Restore")
    require("SyncWithSerializedPlayer" not in rollback, "rollback must not replace complete Player or replay pickup hooks")


def save_boundary_contracts(events, state=None, patches=None, verifier=None, scope=None, reload=None,
                            interaction=None, interaction_patch=None):
    """Source guard for the new entered-room contract, not a production restore verdict."""
    state = read("src/Services/ShinGetterPlayerEventState.cs") if state is None else state
    patches = read("src/Patches/ShinGetterEventInvasionPatch.cs") if patches is None else patches
    verifier = read("src/Events/ShinGetterEventSaveVerification.cs") if verifier is None else verifier
    scope = read("src/Events/ShinGetterEventMapSaveScope.cs") if scope is None else scope
    reload = read("src/Patches/ShinGetterEventSaveScopePatch.cs") if reload is None else reload
    interaction = read("src/Events/ShinGetterEventInteractionCheckpoint.cs") if interaction is None else interaction
    interaction_patch = read("src/Patches/ShinGetterEventInteractionCheckpointPatch.cs") if interaction_patch is None else interaction_patch
    save = body(events, "private static async Task SaveFourth(EventModel model)")
    contains(save, "DebugOnlyGetState(), owner.RunState", "room.ModelId != model.Id",
             "FourthVisitKey(model) == null", "ReferenceEquals(owner.RunState.BaseRoom, room)",
             "EventRoom checkpoint = new(room.CanonicalEvent);", "checkpoint.MarkPreFinished();",
             "visit.RoomCheckpoint = new()",
             "Extra = previousCheckpoint?.Extra", "BeginCheckpoint(owner.RunState, checkpoint)",
             "manager.SaveRun(checkpoint, saveProgress: false)", "Capture(readback.SaveData)",
             "scope.Expected ?? throw", "Compare(expected, actualReadback)",
             "catch when (!saved) { visit.RoomCheckpoint = previousCheckpoint; throw; }")
    native_rng_state = "RngState111" in state
    if native_rng_state:
        contains(save, "var rng = model.Rng.ToSerializable()", "RngCounter = rng.counter",
                 "RngState111 = new[] { rng.state0, rng.state1, rng.state2, rng.state3 }")
    else:
        contains(save, "RngCounter = model.Rng.Counter")
    require(events.count(".MarkPreFinished(") == 1 and save.count("manager.SaveRun(") == 1,
            "only the detached checkpoint may be pre-finished; only its native save may run")
    require("SaveRun(null" not in save and "checkpoint = room" not in save,
            "an entered room must not save the pre-entry/null boundary or mutate the live room")
    require(".ToSave(" not in save, "expected must come from the actual native pre-write DTO, not a second ToSave")
    require(save.index("EventRoom checkpoint = new(") < save.index("checkpoint.MarkPreFinished()")
            < save.index("manager.SaveRun(checkpoint"), "detached room must exist before marking and saving")

    gate = body(events, "private static ShinGetterPlayerEventState.Visit? RestoredFourthVisit(")
    contains(gate, "EventRoom { IsPreFinished: true }", "ReferenceEquals(run.CurrentRoom, room)",
             "ReferenceEquals(run.BaseRoom, room)", "run.Players.Count != 1",
             "FourthEvents.Contains(eventRoom.ModelId.Entry)", "eventRoom.CanonicalEvent.IsShared",
             "NetService.Type != NetGameType.Singleplayer", "Player owner = run.Players[0]",
             "IsShinGetter(owner.Character.Id)", "FourthVisitKey(owner, eventRoom.ModelId)",
             "Visits.TryGetValue(key, out var visit)", "visit.RoomCheckpoint is { Version: 1 }")
    require("FourthVisit(" not in gate and "Visits[key] =" not in gate,
            "failed restore identity gates must not manufacture a visit")
    identity = body(events, "private static string? FourthVisitKey(Player owner, ModelId modelId)")
    contains(identity, "IRunState run = owner.RunState", "room.ModelId != modelId", "rooms[visit].ModelId != modelId",
             "run.Rng.StringSeed, owner.NetId, act", "run.CurrentMapCoord.ToString(), points.Count - 1, visit, modelId")
    restored = body(events, "internal static bool RestoreFourthState(")
    contains(restored, "ReferenceEquals(model.Owner, model.Owner.RunState.Players[0])",
             "isPreFinished && model.Owner.RunState.CurrentRoom?.ModelId != model.Id",
             "var visit = isPreFinished", "? RestoredFourthVisit(", ": FourthVisit(model)", "if (visit == null) return false",
             "FourthRouteNames", "Finish(model", "else if (isPreFinished) ReturnFourthInitial(model)")
    if native_rng_state:
        contains(restored, "checkpoint.RngState111", "model.Rng.LoadFromSerializable(new SerializableRng",
                 "counter = checkpoint.RngCounter", "state0 = rng[0], state1 = rng[1], state2 = rng[2], state3 = rng[3]")
    else:
        contains(restored, "model.Rng.FastForwardCounter(visit.RoomCheckpoint!.RngCounter)")
    for token in ("AddFourthCard", "Obtain", "LoseGold", "FourthDamage", "SaveFourth", "NextInt"):
        require(token not in restored, f"restoring a room must not replay {token}")
    contains(patches, "RestoreFourthState(__instance, isPreFinished)")
    hook = body(body(patches, "internal static class ShinGetterFourthRoomRestoreHookPatch"), "private static bool Prefix(")
    contains(hook, "if (room is not EventRoom { IsPreFinished: true }) return true",
             "if (!ShinGetterEventInvasionService.IsRestoringFourthRoom(runState, room)) return true",
             "__result = Task.CompletedTask", "return false")
    require(hook.count("return false") == 1 and hook.index("IsRestoringFourthRoom") < hook.index("return false"),
            "AfterRoomEntered may be skipped only after the exact checkpoint/identity gate")

    checkpoint = body(state, "internal sealed class EnteredRoomCheckpoint")
    contains(checkpoint, "Version { get; set; } = 1", "RngCounter", "[JsonExtensionData]")
    require(checkpoint.count("[JsonRequired]") == 2, "checkpoint version and RNG counter must be explicit")
    visit = body(state, "internal sealed class Visit")
    contains(visit, "[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]",
             "EnteredRoomCheckpoint? RoomCheckpoint", "[JsonExtensionData]")
    contains(body(state, "internal sealed class Data"), "[JsonExtensionData]")
    contains(body(state, "private static void Validate("), "checkpoint.Version != 1",
             "checkpoint.RngCounter is < 0 or > 100000")

    stable = body(verifier, "internal static Snapshot Capture(")
    contains(stable, "InspectSnapshot(save)", "MapFlag(p.Path, p.Point.CanBeModified)",
             "SerializeToUtf8Bytes(save, JsonSerializationUtility.GetTypeInfo<SerializableRun>())",
             "new Snapshot(native, StabilizeNativeJson(native), flags)")
    normalized = body(verifier, "internal static byte[] StabilizeNativeJson(")
    contains(normalized, 'root["map_point_history"] is JsonArray acts', "act is not JsonArray points",
             'history["player_stats"] is not JsonArray players', 'stats["event_choices"] is not JsonArray choices',
             'entry["variables"] is JsonObject { Count: 0 }', 'entry.Remove("variables")', "root.WriteTo(writer)")
    require(re.findall(r'\w+\.Remove\("([^"]+)"\)', normalized) == ["save_time", "run_time", "variables"]
            and "Deserialize" not in stable + normalized,
            "only two clocks and exact-path empty native event variables may be normalized")
    contains(body(verifier, "internal static async Task Verify("), "await write()", "if (!wasSaved())",
             "readback()", "catch (Exception ex) when (wasSaved())")
    contains(scope, "AsyncLocal<ShinGetterEventMapSaveScope?>", "ReferenceEqualityComparer.Instance",
             "ToFrozenSet<SerializableMapPoint>", "Interlocked.CompareExchange(ref points",
             "Volatile.Read(ref scope.active)", "Interlocked.Exchange(ref active, 0)", "Ambient.Value = previous",
             "nativeCondition != null ? nativeCondition(obj, value) : value is true",
             "if (info.IsReadOnly", "InstalledMetadata.TryGetValue(info")
    capture = body(scope, "internal static void CaptureNativeToSave(")
    contains(capture, "ReferenceEquals(manager.DebugOnlyGetState(), scope.run)",
             "ReferenceEquals(preFinishedRoom, scope.checkpoint)", "room.EventId != scope.checkpoint!.ModelId",
             "scope.RegisterPoints(save)", "scope.Expected = ShinGetterEventSaveVerification.Capture(save)")
    contains(reload, '"MoveNext"', "code.Count(i => i.Calls(NativeReload)) != 1",
             "ReferenceEquals(runManager.DebugOnlyGetState(), state)", "OwnsFourthReloadCheckpoint(state, save)",
             "InspectSnapshot(save)")
    if native_rng_state:
        contains(reload, "nameof(RunManager.SetUpSavedSingleplayer)", "await manager.IncrementNumReloads(save, type, forceInTest)")
    else:
        contains(reload, "nameof(RunManager.SetUpSavedSinglePlayer)", "await manager.IncrementNumReloads(save, isMultiplayer)")
    owns = body(events, "internal static bool OwnsFourthReloadCheckpoint(")
    contains(owns, "run.CurrentRoom != null", "run.Players.Count != 1", "save.Players.Count != 1",
             "player.NetId != owner.NetId", "save.SerializableRng.Seed != run.Rng.StringSeed",
             "save.VisitedMapCoords.SequenceEqual(run.VisitedMapCoords)", "point.Rooms.Count != 1",
             "save.MapPointHistory[act].Count - 1, 0, eventId", "visit.RoomCheckpoint is { Version: 1 }",
             "ShinGetterPlayerEventState.Encode(saved) == ShinGetterPlayerEventState.Encode(live)")
    require("NetService" not in owns, "reload identity must not use the uninitialized network service")
    contains(body(verifier, "internal static void Compare("), "FindDifferences", "DiagnosticSink?.Invoke(comparison)",
             "catch (Exception ex)", "if (!equal)")
    commit = body(events, "private static async Task<bool> CommitFourth(")
    confirmed = commit[commit.index("catch (ShinGetterEventSaveVerification.ConfirmedWriteReadbackException"):commit.index("catch { snapshot.Rollback()")]
    require("Rollback" not in confirmed and "return false" not in confirmed,
            "a confirmed native write must not reopen the reward transaction")
    require(events.count("await SaveFourth(model)") == 6, "six direct save boundaries must retain model identity")
    run = body(events, "private static async Task RunFourth(")
    contains(run, "await interaction.HistoryReady", "await SaveFourth(model)", "interaction?.Dispose()")
    require(run.index("await interaction.HistoryReady") < run.index("await SaveFourth(model)") < run.index("await ExecuteFourth"),
            "native history must finish and save before any owned interaction executes")
    canceled = body(run, "if (!committed && !model.IsFinished)")
    require(canceled.index("await SaveFourth(model)") < canceled.index("ReturnFourthInitial(model)"),
            "Skip/cancel must persist native effects before returning to initial page")
    prepare = body(events, "internal static ShinGetterEventInteractionCheckpoint? PrepareFourthInteraction(")
    contains(prepare, "ReferenceEquals(model.Owner, player)", "model.IsShared", "player.RunState.Players.Count != 1",
             "IsShinGetter(player.Character.Id)", "NetService.Type != NetGameType.Singleplayer",
             "option.TextKey == Key(model.Id.Entry, r)", "ReferenceEquals(run.BaseRoom, room)", "() => SaveFourth(model)")
    selection = body(events, "private static async Task<CardModel?> AwaitFourthSelection(")
    contains(selection, "using var presentation = interaction?.Select()", "interaction?.ThrowPresentationFailure()")
    contains(interaction, "AsyncLocal<ShinGetterEventInteractionCheckpoint?>", "ReferenceEquals(session.source, source)",
             "ReferenceEquals(session.player, player)", "ReferenceEquals(session.option, option)",
             "await HistoryReady", "if (IsCurrent) await save()", "return await nativeSelection",
             "ExceptionDispatchInfo.Capture(error)", "Interlocked.Exchange(ref active, 0)")
    contains(interaction_patch, '"ChooseOptionForEvent"', '"SaveEventOptionToHistory"',
             "HistoryFailed(__exception)", "NotifyHistoryCompleted(__instance, player, option)",
             "nameof(CardSelectCmd.FromChooseACardScreen)", "nameof(CardSelectCmd.FromSimpleGrid)",
             "code.Count(i => i.Calls(Choose) || i.Calls(Grid)) != 1",
             "BeforeSelectionResult(screen.CardsSelected())")


def save_boundary_negative_contracts(events):
    """Ensure the guard rejects concrete unsafe regressions instead of deleting the old ban."""
    sources = {"events": events, "state": read("src/Services/ShinGetterPlayerEventState.cs"),
               "patches": read("src/Patches/ShinGetterEventInvasionPatch.cs"),
                "verifier": read("src/Events/ShinGetterEventSaveVerification.cs"),
                "scope": read("src/Events/ShinGetterEventMapSaveScope.cs"),
                "reload": read("src/Patches/ShinGetterEventSaveScopePatch.cs"),
                "interaction": read("src/Events/ShinGetterEventInteractionCheckpoint.cs"),
                "interaction_patch": read("src/Patches/ShinGetterEventInteractionCheckpointPatch.cs")}
    mutations = [
        ("live room marking", "events", "EventRoom checkpoint = new(room.CanonicalEvent);", "EventRoom checkpoint = room;"),
        ("null save", "events", "manager.SaveRun(checkpoint,", "manager.SaveRun(null,"),
        ("missing explicit v1 stamp", "events", "visit.RoomCheckpoint is { Version: 1 }", "visit.RoomCheckpoint != null"),
        ("owner mismatch", "events", "!ReferenceEquals(model.Owner, model.Owner.RunState.Players[0])", "false"),
        ("coordinate mismatch", "events", "run.CurrentMapCoord.ToString(), points.Count - 1, visit, modelId", '"other", points.Count - 1, visit, modelId'),
        ("history mismatch", "events", "points.Count - 1, visit, modelId", "0, visit, modelId"),
        ("non-base room", "events", "!ReferenceEquals(run.BaseRoom, room)", "false"),
        ("host service", "events", "RunManager.Instance.NetService.Type != NetGameType.Singleplayer", "false"),
        ("global Hook skip", "patches", "if (!ShinGetterEventInvasionService.IsRestoringFourthRoom(runState, room)) return true;", ""),
        ("future stamp admitted", "state", "checkpoint.Version != 1", "false"),
        ("player-only readback", "events", "Capture(readback.SaveData)", "Capture(expectedPlayerOnly)"),
        ("hidden pool mismatch", "verifier", 'root.Remove("run_time");', 'root.Remove("run_time"); root.Remove("acts");'),
        ("nonempty variables ignored", "verifier", 'JsonObject { Count: 0 }', 'JsonObject'),
        ("wrong variable path", "verifier", 'stats["event_choices"]', 'stats["card_choices"]'),
        ("Saved boundary inverted", "verifier", "catch (Exception ex) when (wasSaved())", "catch (Exception ex) when (!wasSaved())"),
        ("uncaptured pre-write expected", "events", "scope.Expected ?? throw", "otherExpected ?? throw"),
        ("wrong native checkpoint reference", "scope", "ReferenceEquals(preFinishedRoom, scope.checkpoint)", "true"),
        ("wrong active run", "scope", "ReferenceEquals(manager.DebugOnlyGetState(), scope.run)", "true"),
        ("revocation removed", "scope", "Interlocked.Exchange(ref active, 0)", "1"),
        ("global false serialization", "scope", "nativeCondition != null ? nativeCondition(obj, value) : value is true", "true"),
        ("reload wrong owner", "events", "player.NetId != owner.NetId", "false"),
        ("reload wrong seed", "events", "save.SerializableRng.Seed != run.Rng.StringSeed", "false"),
        ("reload no stamp", "events", "&& visit.RoomCheckpoint is { Version: 1 }", "&& true"),
        ("reload wrong input state", "reload", "ReferenceEquals(runManager.DebugOnlyGetState(), state)", "true"),
        ("reload global scope", "reload", "!ShinGetterEventInvasionService.OwnsFourthReloadCheckpoint(state, save)", "false"),
        ("history boundary skipped", "events", "await interaction.HistoryReady;", ""),
        ("reentry history not saved", "events", "await SaveFourth(model); // Native history is now complete", "await Task.CompletedTask; // Native history is now complete"),
        ("Skip effects not saved", "events", "await SaveFourth(model); // Includes Skip/cancel", "await Task.CompletedTask; // Includes Skip/cancel"),
        ("foreign native option history", "interaction", "ReferenceEquals(session.option, option)", "true"),
        ("presentation not saved", "interaction", "if (IsCurrent) await save()", "if (IsCurrent) await Task.CompletedTask"),
        ("presentation save failure ignored", "events", "interaction?.ThrowPresentationFailure()", ""),
        ("unscoped selection lifetime", "events", "using var presentation = interaction?.Select()", "using var presentation = (IDisposable?)null"),
        ("interaction token not revoked", "interaction", "Interlocked.Exchange(ref active, 0)", "1"),
    ]
    for name, key, old, new in mutations:
        require(old in sources[key], f"negative contract fixture no longer matches: {name}")
        mutated = dict(sources); mutated[key] = mutated[key].replace(old, new)
        rejected = False
        try:
            save_boundary_contracts(**mutated)
        except (AssertionError, ValueError):
            rejected = True
        require(rejected, f"entered-room gate accepted unsafe regression: {name}")
    print(f"issue#238: entered-room source contract and {len(mutations)} unsafe-regression negatives PASS (not production restore)")


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
             "card.Owner == Owner", "ShinGetterAnnotationsCardChoice.Select", "if (_canceled) return", "PrepareManualPlay",
             "ConsumeManualCancellation", "HasPreparedTarget", "committedSelectionScreenPrompt",
             "previousAmount", "PowerCmd.Apply<VigorPower>")
    require("GainEnergy" not in annotations and "CardPileCmd.Add(this, PileType.Hand)" not in annotations,
            "committed annotations must never refund/return after native hooks")
    require("CardSelectCmd.FromHand" not in annotations, "Annotations must not use Hand's unsupported cancel flag")
    picker = read("src/Services/ShinGetterAnnotationsCardChoice.cs")
    contains(picker, "owner.PlayerCombatState.Hand", "hand.Cards.Where(legal).ToArray()", "cards.Length == 0",
             "new CardSelectorPrefs(prompt, 1)", "Cancelable = true", "RequireManualConfirmation = true",
             "NDeckCardSelectScreen.Create(cards, prefs)", "PlayerChoiceResult.FromMutableCombatCards(result)",
             "SignalPlayerChoiceBegun(PlayerChoiceOptions.CancelPlayCardActions)", "ReferenceEquals(manager.DebugOnlyGetState(), run)",
             "manager.IsCleaningUp", "RunAbortedException", "WaitForActionToResumeExecutingAfterPlayerChoice()")
    require("manager.State" not in picker, "formal109 RunManager.State is private; retain identity via its public getter")
    require("FromDeckGeneric" not in picker and "CardSelectCmd.FromSimpleGrid" not in picker,
            "hand instances require the local native-UI adapter, not a permanent Deck query or SimpleGrid")
    require("cards.Length == 1" not in picker and "WaitAsync" not in picker and "Task.WhenAny" not in picker,
            "no single-target auto-commit or abandoned wait race")
    contains(wrapper, "ShinGetterAnnotationsCardChoice.RunAbortedException", "context.LastInvolvedModel", "context.PopModel(annotations)")
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
    stone = routes[("STONE_OF_ALL_TIME", "BENKEI")]["result"][0]
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


def damage_template_contracts():
    source = json.loads(read("tools/issue238-localization.json"))
    for model, key, allowed in (
        ("SGC_DrillMissile", "S_G_C_DRILL_MISSILE.description", {"Damage"}),
        ("SGC_TripleCyclone", "S_G_C_TRIPLE_CYCLONE.description", {"Damage", "Block"}),
    ):
        code = read(f"src/Models/Cards/{model}.cs")
        require(re.search(r"new DamageVar\(\d+m, ValueProp\.Move\)", code), "default DamageVar declaration drift")
        for index, lang in enumerate(source["languages"]):
            value = source["content"]["cards"][key][index]
            require("{Damage:diff()}" in value and "CalculatedDamage" not in value, f"{lang}: missing native Damage variable")
            selectors = set(re.findall(r"\{([A-Za-z_][A-Za-z_0-9]*)(?=[:}])", value))
            require(selectors <= allowed, f"{lang}: unsupported selectors in {model}: {selectors - allowed}")
            actual = json.loads(read(f"ShinGetterMod/localization/{lang}/cards.json"))[key]
            require(actual == value, f"{lang}: synchronized damage template drift")
    # This gate checks real source declarations and authored/synchronized selectors.
    # Native LocManager base/upgrade/target-preview execution belongs to the runtime review.


def event_selection_contracts(events):
    deck = body(events, "private static async Task<CardModel?> SelectFourthCard(")
    contains(deck, "owner.Deck.Cards.Any(legal)", "CardSelectCmd.FromDeckGeneric(owner,",
             "Cancelable = cancelable", "RequireManualConfirmation = true", "}, legal)",
             "owner.Deck.Cards.Contains(selected)", "PileType.Deck", "legal(selected)")
    require("FromSimpleGrid" not in deck, "cancelable Deck routes require a real native Back entry")
    selection = body(events, "private static async Task<CardModel?> AwaitFourthSelection(")
    contains(selection, "ReferenceEquals(manager.DebugOnlyGetState(), run)", "ReferenceEquals(owner.RunState, run)",
             "manager.IsCleaningUp", "ReferenceEquals(owner.RunState.CurrentRoom, room)",
             "eventRoom.ModelId == model.Id", "!model.IsFinished", "FourthSelectionAbortedException",
             "catch (OperationCanceledException) when (IsCurrent())")
    require(selection.count("if (!IsCurrent())") == 2, "event identity must be checked before and after selection")
    run = body(events, "private static async Task RunFourth(")
    aborted = run[run.index("catch (FourthSelectionAbortedException)"):run.index("catch (Exception ex)")]
    require("ReturnFourthInitial" not in aborted and "ShowFourthError" not in aborted,
            "an exited selection must not reopen the stale event or display a popup")
    execute = body(events, "private static async Task<bool> ExecuteFourth(")
    aroma = execute[execute.index('case ("AROMA_OF_CHAOS", _):'):execute.index('case ("HUNGRY_FOR_MUSHROOMS", _):')]
    contains(aroma, "CardSelectCmd.FromChooseACardScreen(", "choices, owner, canSkip: true",
             "if (selected == null || !choices.Contains(selected)) return false;")
    require("FromDeckGeneric" not in aroma and "FromSimpleGrid" not in aroma,
            "no-Pile Aroma candidates must not use the Deck preview or uncancelable grid")
    require(aroma.index("visit.Target = FourthCardIdentity") < aroma.index("await SaveFourth(model)")
            < aroma.index("await CachedFourthChoices") < aroma.index("FromChooseACardScreen")
            < aroma.index("return await CommitFourth") < aroma.index("await FourthDamage(owner, 6)"),
            "Aroma must persist its target/candidates before selection and pay only on confirmation")
    brain = execute[execute.index('case ("BRAIN_LEECH", _):'):execute.index('case ("DOLL_ROOM", _):')]
    contains(brain, "FromSimpleGrid", "Cancelable = false", "RequireManualConfirmation = true")
    require(events.count("CardSelectCmd.FromSimpleGrid(") == 1,
            "only the deliberately noncancelable Brain Leech selection may retain SimpleGrid")
    # These source/API/commit-boundary gates do not replace actual native input,
    # same-run overlay cleanup, run exit, or persisted re-entry/restore execution.


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
                # A combined candidate also contains separately reviewed balance text.
                overlay = "1d30b3815e96095572af3c016f6a105fac426646"
                overlay_keys = {
                    "events": {"S_G_E_GETTER_MANDALA.pages.INITIAL.options.SAINT_DRAGON.description"},
                    "relics": {"S_G_R_MUSASHI_CLONE.title", "S_G_R_MUSASHI_CLONE.description"},
                }
                if key in overlay_keys.get(table, set()) and subprocess.run(
                    ["git", "merge-base", "--is-ancestor", overlay, "HEAD"], cwd=ROOT,
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                ).returncode == 0:
                    approved = json.loads(subprocess.check_output([
                        "git", "show", f"{overlay}:shin-getter-mod-godot/ShinGetterMod/localization/{language}/{table}.json",
                    ], cwd=ROOT))
                    expected = approved[key]
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
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--probe", action="store_true", help="Run actual 109 CLR integration probe; no game scene/window")
    parser.add_argument("--self-test-save-boundary", action="store_true", help="Check only entered-room source guards and negative mutations; no game/save writes")
    args = parser.parse_args()
    events = read("src/Events/ShinGetterEventInvasionService.Issue238.cs")
    save_boundary_contracts(events)
    save_boundary_negative_contracts(events)
    if args.self_test_save_boundary:
        return
    native_event_contracts(events)
    routes = ROUTES
    require(len(routes) == 15 and sum(map(len, routes.values())) == 20, "fourth batch route quota")
    require("owner.RunState.Players.Count != 1" in events, "single-player guard missing")
    require("disableOnChosen: false" in events, "cancelable routes must not latch WasChosen")
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
    damage_template_contracts()
    event_selection_contracts(events)
    prose_contracts()
    if args.probe:
        subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/issue238-probe/issue238-probe.csproj"), "--verbosity", "quiet"], check=True)
    print("issue#238: 15 events / 20 routes; persistence/content/current-prose structural gates PASS (not game acceptance)")


if __name__ == "__main__":
    main()
