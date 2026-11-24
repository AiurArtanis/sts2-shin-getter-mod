using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using Verifier = ShinGetterMod.Events.ShinGetterEventSaveVerification;

// Pure CLR: two linked production helpers + unchanged official109 dependencies.
// No Entry, Harmony installation, game project build, native writes or Godot scene.
int checks = 0;
void Require(bool value, string label)
{
    Console.WriteLine($"{(value ? "PASS" : "FAIL")} {label}");
    if (!value) throw new InvalidOperationException(label);
    checks++;
}
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
var nativeAssembly = typeof(SerializableRun).Assembly;
Require(Hash(File.ReadAllBytes(nativeAssembly.Location)) == "C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0", "exact official109 dependency SHA");
Require(nativeAssembly.ManifestModule.ModuleVersionId == Guid.Parse("79823e71-0931-4599-bb90-166bdd53ff56"), "exact official109 MVID");
bool baseline = args.Length == 2 && args[0] == "--baseline-red";
if (args.Length != 0 && !(args.Length == 2 && (baseline || args[0] == "--evidence-manifest")))
    throw new ArgumentException("Use --evidence-manifest <Comparison001 manifest>, or --baseline-red <same manifest>.");
MethodInfo? stabilize = typeof(Verifier).GetMethod("StabilizeNativeJson", BindingFlags.NonPublic | BindingFlags.Static);
byte[] Stable(byte[] json) => (byte[])stabilize!.Invoke(null, [json])!;
byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);
string History(string choice) => "{\"map_point_history\":[[{\"player_stats\":[{\"event_choices\":[{" + choice + "}]}]}]]}";
Verifier.Comparison? observed = null;
Verifier.DiagnosticSink = comparison => observed = comparison;
bool Compare(byte[] leftNative, byte[] rightNative, byte[] leftStable, byte[] rightStable,
    Verifier.MapFlag[]? leftFlags = null, Verifier.MapFlag[]? rightFlags = null)
{
    observed = null;
    try
    {
        Verifier.Compare(new(leftNative, leftStable, leftFlags ?? []), new(rightNative, rightStable, rightFlags ?? []));
        return true;
    }
    catch (InvalidOperationException) { return false; }
}
bool Same(string left, string right)
{
    byte[] a = Utf8(left), b = Utf8(right);
    return Compare(a, b, Stable(a), Stable(b));
}
SerializableRun MakeRun(Dictionary<string, object>? variables) => new()
{
    Players = [], SerializableRng = new() { Seed = "issue238-formal109-vars" },
    SerializableOdds = new(), SerializableSharedRelicGrabBag = new(),
    MapPointHistory = [[new()
    {
        PlayerStats = [new PlayerMapPointHistoryEntry
        {
            PlayerId = 1,
            EventChoices = [new EventOptionHistoryEntry
            {
                Title = new LocString("events", "SHIN_GETTER_EVENT_INVASION.BRAIN_LEECH.pages.INITIAL.options.TRIPLE_COORDINATION.title"),
                Variables = variables,
            }],
        }],
    }]],
};
// Roundtrip is confined to this map-free fixture to observe the actual converter;
// production Capture receives direct DTOs and does not add a DTO roundtrip.
var filtered = MakeRun(new() { ["FromCardChoiceCount"] = new IntVar("FromCardChoiceCount", 5m) });
Verifier.Snapshot expectedFixture = Verifier.Capture(filtered);
var loaded = JsonSerializer.Deserialize(expectedFixture.NativeJson, JsonSerializationUtility.GetTypeInfo<SerializableRun>())!;
Verifier.Snapshot readbackFixture = Verifier.Capture(loaded);
using (JsonDocument original = JsonDocument.Parse(expectedFixture.NativeJson))
    Require(original.RootElement.GetProperty("map_point_history")[0][0].GetProperty("player_stats")[0]
        .GetProperty("event_choices")[0].GetProperty("variables").EnumerateObject().Count() == 0,
        "production Capture retains native filtered-empty object in original NativeJson");
bool fixtureEqual = Compare(expectedFixture.NativeJson, readbackFixture.NativeJson, expectedFixture.StableJson, readbackFixture.StableJson);
Require(fixtureEqual != baseline, baseline ? "old production Capture/Compare rejects native filtered-empty roundtrip (expected RED)" : "new production Capture/Compare accepts official109 empty/missing representation");

if (args.Length == 2)
{
    byte[] manifestBytes = File.ReadAllBytes(args[1]);
    Require(Hash(manifestBytes) == "567630FF0537A55CDFCB0033CD7D4DEE0FBB3E3B776DBEEFD8A2A6D24B8F7A9B", "preserved formal RED manifest SHA");
    using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
    var files = new Dictionary<string, byte[]>();
    var inputPaths = new Dictionary<string, string>();
    foreach (JsonElement entry in manifest.RootElement.GetProperty("files").EnumerateArray())
    {
        string path = entry.GetProperty("path").GetString()!;
        byte[] data = File.ReadAllBytes(path);
        Require(data.Length == entry.GetProperty("bytes").GetInt32()
            && Hash(data) == entry.GetProperty("sha256").GetString(), "archived input identity: " + Path.GetFileName(path));
        string key = entry.GetProperty("side").GetString() + "/" + entry.GetProperty("format").GetString();
        files.Add(key, data); inputPaths.Add(key, path);
    }
    Verifier.MapFlag[] Flags(string name) => manifest.RootElement.GetProperty(name).EnumerateArray()
        .Select(e => new Verifier.MapFlag(e.GetProperty("Path").GetString()!, e.GetProperty("Value").GetBoolean())).ToArray();
    var leftFlags = Flags("expectedFlags"); var rightFlags = Flags("readbackFlags");
    Require(leftFlags.Length == 51 && leftFlags.SequenceEqual(rightFlags), "all 51 original MapFlags remain equal");
    byte[] left = files["Expected/NativeJson"], right = files["Readback/NativeJson"];
    bool equal = Compare(left, right, baseline ? files["Expected/StableJson"] : Stable(left),
        baseline ? files["Readback/StableJson"] : Stable(right), leftFlags, rightFlags);
    Require(equal != baseline, baseline ? "actual old Compare rejects preserved formal RED pair" : "actual new Compare accepts preserved formal RED pair offline");
    Require(observed is not null && ReferenceEquals(observed.Expected.NativeJson, left)
        && ReferenceEquals(observed.Readback.NativeJson, right), "diagnostic comparison retains both original NativeJson arrays");
    if (baseline)
        Require(observed!.Differences.Length == 1 && observed.Differences[0].Path == "$['map_point_history'][0][1]['player_stats'][0]['event_choices'][0]['variables']",
            "baseline RED is exactly the archived empty/missing path");
    else
    {
        Require(observed!.BytesEqual && observed.Differences.Length == 0, "offline repaired pair has no remaining differences");
        foreach (var flag in leftFlags.Where(f => !f.Value))
        {
            JsonObject Point(JsonObject root)
            {
                Match m = Regex.Match(flag.Path, @"^\$\.acts\[(\d+)\]\.saved_map\.(?:points\[(\d+)\]|(boss|start|second_boss))\.can_modify$");
                if (!m.Success) throw new InvalidOperationException("Unexpected map path " + flag.Path);
                JsonNode map = root["acts"]![int.Parse(m.Groups[1].Value)]!["saved_map"]!;
                return (m.Groups[2].Success ? map["points"]![int.Parse(m.Groups[2].Value)] : map[m.Groups[3].Value])!.AsObject();
            }
            foreach (bool remove in new[] { false, true })
            {
                JsonObject changed = JsonNode.Parse(left)!.AsObject();
                if (remove) Point(changed).Remove("can_modify"); else Point(changed)["can_modify"] = true;
                byte[] mutation = Utf8(changed.ToJsonString());
                Require(!Compare(left, mutation, Stable(left), Stable(mutation), leftFlags, leftFlags),
                    "each original map false remains strict (" + (remove ? "missing" : "true") + "): " + flag.Path);
            }
        }
    }
    foreach (var item in files)
        Require(item.Value.AsSpan().SequenceEqual(File.ReadAllBytes(inputPaths[item.Key])), "archived bytes unchanged after checks: " + item.Key);
    Require(manifestBytes.AsSpan().SequenceEqual(File.ReadAllBytes(args[1])), "archived manifest unchanged after checks");
}
if (baseline)
{
    Verifier.DiagnosticSink = null;
    Console.WriteLine($"EXPECTED_BASELINE_RED_CONFIRMED ({checks} checks); NATIVE_GAMEPLAY_NOT_RUN");
    return;
}
Require(stabilize is not null, "production stabilization method is present");
Require(Same(History("\"variables\":{}"), History("")) && Same(History(""), History("\"variables\":{}")), "only exact-path empty object/missing is bidirectionally equivalent");
foreach (string value in new[] { "null", "[]", "\"\"", "0", "true", "{\"x\":{}}", "{\"x\":1}" })
    Require(!Same(History("\"variables\":" + value), History("")), "nonempty/null/array/wrong type remains strict: " + value);
Require(!Same(History("\"variables\":{\"x\":1}"), History("\"variables\":{\"x\":2}")), "persisted variable value change remains strict");
foreach (string pair in new[]
{
    "{\"variables\":{}}|{}",
    "{\"payload\":{\"variables\":{}}}|{\"payload\":{}}",
    "{\"map_point_history\":{\"variables\":{}}}|{\"map_point_history\":{}}",
    "{\"map_point_history\":[{\"player_stats\":[{\"event_choices\":[{\"variables\":{}}]}]}]}|{\"map_point_history\":[{\"player_stats\":[{\"event_choices\":[{}]}]}]}",
    "{\"map_point_history\":[[{\"player_stats\":{\"event_choices\":[{\"variables\":{}}]}}]]}|{\"map_point_history\":[[{\"player_stats\":{\"event_choices\":[{}]}}]]}",
    "{\"map_point_history\":[[{\"player_stats\":[{\"card_choices\":[{\"variables\":{}}]}]}]]}|{\"map_point_history\":[[{\"player_stats\":[{\"card_choices\":[{}]}]}]]}"
})
{
    string[] halves = pair.Split('|');
    Require(!Same(halves[0], halves[1]), "root/unrelated/similar path or intermediate wrong shape remains strict");
}
Require(!Same(History("\"title\":\"before\",\"variables\":{}"), History("\"title\":\"after\"")), "adjacent title remains strict");
Require(!Same(History("\"variables\":{}"), "{\"map_point_history\":[[{\"player_stats\":[{\"event_choices\":[]}]}]]}"), "choice removal remains strict");
foreach (string field in new[] { "gold", "current_hp", "max_hp", "rng_counter", "seed", "acts", "difficulty", "mode", "new_field" })
    Require(!Same("{\"" + field + "\":1}", "{\"" + field + "\":2}"), "whole-tree gameplay/adjacent field remains strict: " + field);
Require(Same("{\"save_time\":1,\"run_time\":2,\"hp\":72}", "{\"save_time\":3,\"run_time\":4,\"hp\":72}"), "existing root clock tolerance remains");
Require(!Same("{\"payload\":{\"save_time\":1}}", "{\"payload\":{\"save_time\":2}}"), "nested clock names are not excluded");
foreach (string token in new[] { "1.00", "1e+3", "999999999999999999999999999999999", "-0" })
{
    byte[] input = Utf8("{\"payload\":{\"number\":" + token + "}}");
    string hash = Hash(input);
    using JsonDocument doc = JsonDocument.Parse(Stable(input));
    Require(doc.RootElement.GetProperty("payload").GetProperty("number").GetRawText() == token, "numeric lexeme retained: " + token);
    Require(Hash(input) == hash, "stabilization never edits the input byte array: " + token);
}
Require(!Same("{\"n\":1.00}", "{\"n\":1}"), "numerically equal but lexically different number is still rejected");
bool duplicateRejected = false;
try { Stable(Utf8(History("\"variables\":{},\"variables\":{\"x\":1}"))); }
catch (TargetInvocationException e) when (e.InnerException is ArgumentException or JsonException) { duplicateRejected = true; }
Require(duplicateRejected, "duplicate relevant keys are rejected rather than hidden");
using (JsonDocument duplicate = JsonDocument.Parse(Stable(Utf8("{\"payload\":{\"x\":1,\"x\":2}}"))))
    Require(duplicate.RootElement.GetProperty("payload").EnumerateObject().Count(p => p.Name == "x") == 2, "unvisited duplicate keys remain for Compare rejection");
bool compareRejectedDuplicate = false;
try { Same("{\"payload\":{\"x\":1,\"x\":2}}", "{\"payload\":{\"x\":1}}"); }
catch (ArgumentException) { compareRejectedDuplicate = true; }
Require(compareRejectedDuplicate, "actual Compare rejects unvisited duplicate keys");
Verifier.DiagnosticSink = null;
Console.WriteLine($"ISSUE238_FORMAL109_EVENT_VARIABLES_CLR_CONTRACTS_PASS ({checks} checks); NATIVE_GAMEPLAY_NOT_RUN");
