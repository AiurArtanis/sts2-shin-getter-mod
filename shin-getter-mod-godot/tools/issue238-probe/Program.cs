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
foreach (var bad in new[] { "{", dataTrue.Replace("\"Version\":1", "\"Version\":2"), "" })
{
    bool rejected = false;
    try { Call("SetWire", Player(9, true), bad); } catch (TargetInvocationException e) when (e.InnerException is JsonException) { rejected = true; }
    Require(rejected, "damaged/unknown field accepted");
}
Console.WriteLine("native nested SerializableRun JSON: true/false/absent/complete/anonymized/corrupt/version PASS");

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
