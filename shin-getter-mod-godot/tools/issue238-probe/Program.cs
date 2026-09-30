using System.Reflection;
using System.Text.Json;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;

// Isolated CLR probe: native 109 serializers and actual mod patches, no game/scene/window.
var mod = typeof(ShinGetterMod.Entry).Assembly;
var state = mod.GetType("ShinGetterMod.Services.ShinGetterPlayerEventState", true)!;
object? Call(string name, params object?[] values) => state.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, values);
void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
var harmony = new Harmony("issue238.isolated.persistence.probe");
foreach (var type in mod.GetTypes().Where(t => t.Name.StartsWith("ShinGetterEventState") && t.GetCustomAttribute<HarmonyPatch>() != null))
    new PatchClassProcessor(harmony, type).Patch();
Call("VerifyJsonMetadata");
const string dataTrue = "{\"Version\":1,\"Initialized\":true,\"Enabled\":true,\"Seed\":\"fixture\",\"Visits\":{\"visit-a\":{\"CompletedRoute\":\"BENKEI\",\"Target\":\"\",\"Candidates\":[]}}}";
const string dataFalse = "{\"Version\":1,\"Initialized\":true,\"Enabled\":false,\"Seed\":\"fixture\",\"Visits\":{}}";
SerializablePlayer Player(ulong id, bool shin) => new()
{
    NetId = id, CharacterId = new ModelId("CHARACTER", shin ? "SHIN_GETTER" : "IRONCLAD"),
    CurrentHp = 35, MaxHp = 70, MaxEnergy = 3, Gold = (int)id * 10,
    Rng = new(), Odds = new(), RelicGrabBag = new(), ExtraFields = new(), UnlockState = new()
};
var players = new List<SerializablePlayer> { Player(1, true), Player(2, false), Player(3, true), Player(4, false) };
Call("SetWire", players[0], dataTrue);
Call("SetWire", players[2], dataFalse);
var run = new SerializableRun { Players = players, SerializableRng = new() { Seed = "fixture" }, SerializableOdds = new(), SerializableSharedRelicGrabBag = new() };
string json = JsonSerializer.Serialize(run, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
using (var doc = JsonDocument.Parse(json))
{
    var nodes = doc.RootElement.GetProperty("players");
    Require(nodes[0].TryGetProperty("shin_getter_event_state_v1", out _), "nested native field absent");
    Require(!nodes[1].TryGetProperty("shin_getter_event_state_v1", out _), "vanilla JSON modified");
}
var loaded = JsonSerializer.Deserialize(json, JsonSerializationUtility.GetTypeInfo<SerializableRun>())!;
Require((string?)Call("GetWire", loaded.Players[0]) == dataTrue, "completed journal JSON roundtrip");
Require((string?)Call("GetWire", loaded.Players[2]) == dataFalse, "explicit false lost");
Require((string?)Call("GetWire", loaded.Players[0].Anonymized()) == dataTrue, "anonymized state lost");
var badPayloads = new List<string> { "{", dataTrue.Replace("\"Version\":1", "\"Version\":2"), "", "{}",
    dataTrue.Replace("\"Initialized\":true", "\"Initialized\":false") };
foreach (string field in new[] { "Version", "Initialized", "Enabled", "Seed", "Visits" })
{
    var value = System.Text.Json.Nodes.JsonNode.Parse(dataTrue)!.AsObject(); value.Remove(field);
    badPayloads.Add(value.ToJsonString());
}
foreach (var bad in badPayloads)
{
    bool rejected = false;
    try { Call("SetWire", Player(9, true), bad); } catch (TargetInvocationException e) when (e.InnerException is JsonException) { rejected = true; }
    Require(rejected, "damaged/unknown field accepted");
}
foreach (var bad in badPayloads.Where(v => v.Length > 0))
{
    var corruptedRun = System.Text.Json.Nodes.JsonNode.Parse(json)!;
    corruptedRun["players"]![0]!["shin_getter_event_state_v1"] = bad;
    bool rejected = false;
    try { JsonSerializer.Deserialize(corruptedRun.ToJsonString(), JsonSerializationUtility.GetTypeInfo<SerializableRun>()); }
    catch (JsonException) { rejected = true; }
    Require(rejected, "nested native run accepted damaged payload");
}
var explicitNullRun = System.Text.Json.Nodes.JsonNode.Parse(json)!;
explicitNullRun["players"]![0]!["shin_getter_event_state_v1"] = null;
bool nullRejected = false;
try { JsonSerializer.Deserialize(explicitNullRun.ToJsonString(), JsonSerializationUtility.GetTypeInfo<SerializableRun>()); }
catch (JsonException) { nullRejected = true; }
Require(nullRejected, "explicit JSON null treated as absent");
var absentRun = System.Text.Json.Nodes.JsonNode.Parse(json)!;
absentRun["players"]![0]!.AsObject().Remove("shin_getter_event_state_v1");
var oldRun = JsonSerializer.Deserialize(absentRun.ToJsonString(), JsonSerializationUtility.GetTypeInfo<SerializableRun>())!;
Require(Call("GetWire", oldRun.Players[0]) == null, "genuinely absent legacy field rejected");
Console.WriteLine("native nested SerializableRun JSON: true/false/absent/complete/anonymized/corrupt/version PASS");
Console.WriteLine("required schema attributes / contradictory uninitialized state / explicit null rejection / real absent legacy field PASS");

// Inventory-only owner fixtures avoid SaveManager/Godot startup. Execute actual state Get/Restore.
Player Owner(bool enabled, bool fragment)
{
    var owner = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
    typeof(Player).GetField("<Character>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
        .SetValue(owner, RuntimeHelpers.GetUninitializedObject(typeof(ShinGetter)));
    typeof(Player).GetField("_runState", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(owner, NullRunState.Instance);
    RelicModel starter = (RelicModel)RuntimeHelpers.GetUninitializedObject(fragment ? typeof(SGR_EmperorsFragment) : typeof(SGR_GetterFurnace));
    starter.GetType().GetField("_eventInvasionEnabled", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(starter, enabled);
    typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(owner, new List<RelicModel> { starter });
    return owner;
}
bool Enabled(object obj) => (bool)obj.GetType().GetProperty("Enabled")!.GetValue(obj)!;
foreach (bool fragment in new[] { false, true })
foreach (bool enabled in new[] { false, true })
{
    var owner = Owner(enabled, fragment); var first = Call("Get", owner)!;
    Require(Enabled(first) == enabled, "legacy true/false migration");
    typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(owner, new List<RelicModel>());
    Require(ReferenceEquals(first, Call("Get", owner)), "starter sacrifice erased authority");
    var replacement = Owner(!enabled, fragment);
    typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(owner,
        typeof(Player).GetField("_relics", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(replacement));
    Require(Enabled(Call("Get", owner)!) == enabled, "reobtained starter overwrote authority");
}
var a = Owner(true, false); var b = Owner(false, true);
Require(!ReferenceEquals(Call("Get", a), Call("Get", b)), "two-owner isolation");
Call("Restore", a, players[2]); Require(!Enabled(Call("Get", a)!), "explicit false load reverted");
Call("Restore", a, Player(1, true)); Require(Enabled(Call("Get", a)!), "missing sync retained stale false instead of migrating actual inventory");
Console.WriteLine("actual state methods with inventory fixtures: both starters / true-false migration / sacrifice / reacquire / owner isolation / missing sync PASS");

// Minimal deterministic ID table fixture. Do not initialize the game's model/resource catalog.
var cache = typeof(ModelIdSerializationCache);
var entries = (List<string>)cache.GetField("_netIdToEntryNameMap", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
var entryMap = (Dictionary<string, int>)cache.GetField("_entryNameToNetIdMap", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
foreach (var entry in new[] { "SHIN_GETTER", "IRONCLAD" }) { entryMap[entry] = entries.Count; entries.Add(entry); }
cache.GetProperty("EntryIdBitSize")!.SetValue(null, 2);
var writer = new PacketWriter();
writer.WriteList(players);
writer.WriteInt(0x1234abcd);
var reader = new PacketReader(); reader.Reset(writer.Buffer);
var packet = reader.ReadList<SerializablePlayer>();
Require(reader.ReadInt() == 0x1234abcd && reader.BitPosition == writer.BitPosition, "four-player tail alignment");
Require((string?)Call("GetWire", packet[0]) == dataTrue && (string?)Call("GetWire", packet[2]) == dataFalse, "mixed-player state");
Require(Call("GetWire", packet[1]) == null && Call("GetWire", packet[3]) == null, "vanilla packet changed");
writer.Reset(); run.Serialize(writer); writer.WriteInt(0x1234abcd);
reader.Reset(writer.Buffer); var packetRun = new SerializableRun(); packetRun.Deserialize(reader);
Require(reader.ReadInt() == 0x1234abcd && reader.BitPosition == writer.BitPosition, "full run packet alignment");
Require((string?)Call("GetWire", packetRun.Players[0]) == dataTrue, "full run state lost");
Console.WriteLine("native packet: four mixed players + sentinel and full SerializableRun + sentinel PASS");
// Compare an original character's serialization with patches disabled: byte-for-byte unchanged.
writer.Reset(); players[1].Serialize(writer); var vanilla = writer.Buffer.Take(writer.BytePosition).ToArray(); int bits = writer.BitPosition;
harmony.UnpatchAll(harmony.Id); writer.Reset(); players[1].Serialize(writer);
Require(bits == writer.BitPosition && vanilla.SequenceEqual(writer.Buffer.Take(writer.BytePosition)), "vanilla packet layout changed");
Console.WriteLine("vanilla packet byte/bit equivalence PASS; probe does not cover game UI or live network reconnect");

// Execute the same save-verification helper used by SaveFourth, with deterministic fault
// injection. This does not boot SaveManager or write into the user's live run-save directory.
var verification = mod.GetType("ShinGetterMod.Events.ShinGetterEventSaveVerification", true)!;
var verify = verification.GetMethod("Verify", BindingFlags.Static | BindingFlags.NonPublic)!;
async Task VerifySave(Func<Task> write, Func<bool> saved, Action readback)
    => await (Task)verify.Invoke(null, new object[] { write, saved, readback })!;
foreach (string fault in new[] { "ok", "no-signal", "write-error", "read-error", "different-player", "late-write-error" })
{
    bool saved = false, rolledBack = false, completed = false;
    SerializablePlayer snapshot = Player(7, true);
    snapshot.Gold -= 60;
    Call("SetWire", snapshot, dataTrue);
    string expected = JsonSerializer.Serialize(snapshot, JsonSerializationUtility.GetTypeInfo<SerializablePlayer>());
    string disk = "";
    Task Write()
    {
        if (fault == "write-error") throw new IOException("injected before Saved");
        if (fault == "no-signal") return Task.CompletedTask;
        disk = expected; saved = true;
        if (fault == "late-write-error") throw new IOException("injected after Saved");
        return Task.CompletedTask;
    }
    void ReadBack()
    {
        if (fault == "read-error") throw new IOException("injected readback failure");
        var nativeLoaded = JsonSerializer.Deserialize(disk, JsonSerializationUtility.GetTypeInfo<SerializablePlayer>())!;
        if (fault == "different-player") nativeLoaded.Gold++;
        if (JsonSerializer.Serialize(nativeLoaded, JsonSerializationUtility.GetTypeInfo<SerializablePlayer>()) != expected)
            throw new InvalidOperationException("injected whole-player mismatch");
    }
    try { await VerifySave(Write, () => saved, ReadBack); completed = true; }
    catch (Exception ex) when (ex.GetType().Name == "ConfirmedWriteReadbackException") { completed = true; }
    catch { rolledBack = true; }
    Require(rolledBack == !saved && completed == saved, "save signal/readback rollback boundary: " + fault);
    if (saved) Require(disk == expected, "complete native player snapshot did not include completion and cost");
}
Console.WriteLine("actual SaveFourth verifier: successful whole-player JSON / swallowed write / pre-signal error / readback exception / mismatch / post-signal error PASS (injected, not native disk I/O)");

var identity = mod.GetType("ShinGetterMod.Events.ShinGetterEventVisitIdentity", true)!;
string VisitKey(string seed = "a", ulong ownerId = 1, int act = 0, string coord = "1,2", int point = 3, int room = 0)
    => (string)identity.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
        new object[] { seed, ownerId, act, coord, point, room, new ModelId("EVENT", "WHISPERING_HOLLOW") })!;
Require(VisitKey() == VisitKey(), "visit key not deterministic on reload");
Require(new[] { VisitKey(), VisitKey(seed: "b"), VisitKey(ownerId: 2), VisitKey(act: 1), VisitKey(coord: "2,2"),
    VisitKey(point: 4), VisitKey(room: 1) }.Distinct().Count() == 7, "visit/run/owner/act/point/subroom collision");
Console.WriteLine("actual visit identity: stable reload / distinct run-owner-act-coordinate-visit-subroom PASS");

var transaction = mod.GetType("ShinGetterMod.Events.ShinGetterEventTransaction", true)!;
RelicModel removedFixture = (RelicModel)RuntimeHelpers.GetUninitializedObject(typeof(SGR_GetterFurnace));
removedFixture.RemoveInternal();
Require(removedFixture.HasBeenRemovedFromState, "fixture did not use native removal flag");
transaction.GetMethod("RestoreRemovedRelicFlag", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { removedFixture });
Require(!removedFixture.HasBeenRemovedFromState, "rollback would return a permanently inactive relic");
Console.WriteLine("actual scoped transaction restoration: native removed relic hook flag reset PASS (not full inventory rollback/UI)");

// Native LocString has no virtual formatter. Probe only the scoped GetRawText integration,
// replacing lookup with a locale table fixture; the actual native Variables remain intact.
var prose = mod.GetType("ShinGetterMod.Events.ShinGetterEventReturnProse", true)!;
object? ProseCall(string method, params object[] values) => prose.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, values);
new PatchClassProcessor(harmony, mod.GetType("ShinGetterMod.Patches.ShinGetterEventReturnProsePatch", true)!).Patch();
harmony.Patch(AccessTools.Method(typeof(LocString), nameof(LocString.GetRawText)),
    prefix: new HarmonyMethod(typeof(ProbeLoc), nameof(ProbeLoc.Prefix)) { priority = Priority.Last });
EventModel eventOne = (EventModel)RuntimeHelpers.GetUninitializedObject(typeof(EndlessConveyor));
EventModel eventTwo = (EventModel)RuntimeHelpers.GetUninitializedObject(typeof(EndlessConveyor));
var original = new LocString("events", "native"); original.Add("Dish", "fixture-dish"); original.Add("Cost", 20m);
Require(ReferenceEquals(original, ProseCall("Compose", eventOne, original)), "unused prose changed native description");
ProseCall("Begin", eventOne, "native", "prose");
var composed = (LocString)ProseCall("Compose", eventOne, original)!;
Require(composed != original && composed.Variables["Dish"].Equals("fixture-dish") && composed.Variables["Cost"].Equals(20m), "native parameters lost/mutated");
Require(composed.GetRawText() == "EN {Dish} {Cost}\n\nEN prose", "successful return did not retain both raw loc keys");
Require(ReferenceEquals(composed, ProseCall("Compose", eventOne, composed)), "repeat render stacked composition");
Require(composed.GetRawText().Split("EN prose").Length == 2, "prose duplicated");
Require(ReferenceEquals(original, ProseCall("Compose", eventTwo, original)), "another event inherited return scope");
ProbeLoc.Language = "JP";
Require(composed.GetRawText() == "JP {Dish} {Cost}\n\nJP prose", "locale refresh froze the native or mod text");
ProseCall("Compose", eventOne, new LocString("events", "next"));
Require(ReferenceEquals(original, ProseCall("Compose", eventOne, original)), "prose scope survived advancing to another page");
Require(original.Variables.Count == 2, "native shared LocString modified");
Console.WriteLine("actual return prose patch: before/success/refresh/no duplication/locale/variable/event isolation/advance cleanup PASS (lookup fixture, no game UI)");

// Execute the actual final-loss interceptor and Harmony dispatch, not a duplicate model.
var sapphireOwner = Owner(true, false);
var sapphireCreature = new Creature(sapphireOwner, 35, 70)
    { CombatState = (CombatState)RuntimeHelpers.GetUninitializedObject(typeof(CombatState)) };
typeof(Player).GetField("<Creature>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(sapphireOwner, sapphireCreature);
var sapphire = (SGR_ActivatedSapphire)RuntimeHelpers.GetUninitializedObject(typeof(SGR_ActivatedSapphire));
typeof(AbstractModel).GetProperty(nameof(AbstractModel.IsMutable))!.SetValue(sapphire, true);
sapphire.Owner = sapphireOwner;
typeof(Player).GetField("_relics", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(sapphireOwner, new List<RelicModel> { sapphire });
var otherCreature = new Creature(Owner(false, true), 35, 70) { CombatState = sapphireCreature.CombatState };
decimal Loss(Creature target, decimal amount) => (decimal)typeof(SGR_ActivatedSapphire)
    .GetMethod("InterceptFinalHpLoss", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(sapphire, new object[] { target, amount })!;
await sapphire.BeforeCombatStart();
Require(Loss(sapphireCreature, 0m) == 0 && Loss(sapphireCreature, 0.99m) == 0.99m && Loss(otherCreature, 9m) == 9m
    && sapphire.DisplayAmount == 0, "zero/subinteger/other-player damage consumed Sapphire");
sapphireCreature.CombatState = null;
Require(Loss(sapphireCreature, 9m) == 9 && sapphire.DisplayAmount == 0, "outside combat counted");
sapphireCreature.CombatState = otherCreature.CombatState;
Require(Loss(sapphireCreature, 2m) == 2 && Loss(sapphireCreature, 3m) == 3 && Loss(sapphireCreature, 1m) == 1
    && Loss(sapphireCreature, 8m) == 0 && Loss(sapphireCreature, 7m) == 7 && sapphire.DisplayAmount == 4,
    "fourth final HP-loss / once per combat regression");
await sapphire.BeforeCombatStart();
var finalPatch = mod.GetType("ShinGetterMod.Patches.ShinGetterActivatedSapphirePatch", true)!;
var postfix = finalPatch.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!;
object?[] beforeArgs = { sapphireCreature.CombatState, sapphireCreature, HpLossHookPhase.BeforeOsty, 9m };
postfix.Invoke(null, beforeArgs);
Require(sapphire.DisplayAmount == 0 && (decimal)beforeArgs[3]! == 9, "prediction/pre-mitigation stage counted");
object?[] afterArgs = { sapphireCreature.CombatState, sapphireCreature, HpLossHookPhase.AfterOsty, 2m };
postfix.Invoke(null, afterArgs);
Require(sapphire.DisplayAmount == 1 && (decimal)afterArgs[3]! == 2, "final-stage dispatch/reset did not count");
Console.WriteLine("actual Sapphire interceptor + final postfix: zero/subinteger/outside/other owner/fourth/once/reset/Before-AfterOsty PASS (CLR fixtures, no damage UI)");

public static class ProbeLoc
{
    public static string Language = "EN";
    public static bool Prefix(LocString __instance, ref string __result)
    {
        __result = __instance.LocEntryKey == "native" ? Language + " {Dish} {Cost}" : Language + " prose";
        return false;
    }
}
