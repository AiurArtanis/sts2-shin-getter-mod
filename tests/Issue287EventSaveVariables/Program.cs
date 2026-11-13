using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;

// Actual official serialization and production capture; no game, writes or native acceptance.
var verifier = typeof(ShinGetterMod.Entry).Assembly.GetType("ShinGetterMod.Events.ShinGetterEventSaveVerification", true)!;
var capture = verifier.GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static)!;
byte[] Stable(SerializableRun run)
{
    object snapshot = capture.Invoke(null, [run])!;
    return (byte[])snapshot.GetType().GetProperty("StableJson")!.GetValue(snapshot)!;
}
void Require(bool value, string label)
{
    Console.WriteLine($"{(value ? "PASS" : "FAIL")} {label}");
    if (!value) throw new InvalidOperationException(label);
}
SerializableRun MakeRun(Dictionary<string, object>? variables) => new()
{
    Players = [], SerializableRng = new() { Seed = "issue287-vars" },
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
SerializableRun RoundTrip(SerializableRun run) => JsonSerializer.Deserialize(
    JsonSerializer.SerializeToUtf8Bytes(run, JsonSerializationUtility.GetTypeInfo<SerializableRun>()),
    JsonSerializationUtility.GetTypeInfo<SerializableRun>())!;
var filtered = MakeRun(new() { ["FromCardChoiceCount"] = new IntVar("FromCardChoiceCount", 5m) });
byte[] before = Stable(filtered), after = Stable(RoundTrip(filtered));
Console.WriteLine("Official native JSON: " + JsonSerializer.Serialize(filtered, JsonSerializationUtility.GetTypeInfo<SerializableRun>()));
Require(before.AsSpan().SequenceEqual(after), "native filtered-empty event variables have a stable capture");
foreach (Dictionary<string, object>? variables in new Dictionary<string, object>?[]
{
    null, new(), new() { ["count"] = 5m }, new() { ["text"] = "kept", ["flag"] = true },
    new() { ["count"] = new DynamicVar("count", 5m) },
    new() { ["text"] = new StringVar("text", "kept") },
    new() { ["filtered"] = new IntVar("filtered", 2m), ["count"] = 5m },
})
{
    var run = MakeRun(variables);
    Require(Stable(run).AsSpan().SequenceEqual(Stable(RoundTrip(run))), "supported, mixed, empty and null native dictionaries roundtrip");
}
var persisted = MakeRun(new() { ["count"] = 5m });
Require(!Stable(persisted).AsSpan().SequenceEqual(Stable(MakeRun(null))), "lost nonempty variables are detected");
Require(!Stable(persisted).AsSpan().SequenceEqual(Stable(MakeRun(new() { ["count"] = 6m }))), "changed persisted variable is detected");
var changed = RoundTrip(filtered);
changed.MapPointHistory[0][0].PlayerStats[0].CurrentHp++;
Require(!Stable(filtered).AsSpan().SequenceEqual(Stable(changed)), "unrelated gameplay differences remain detected");
var clocks = RoundTrip(filtered); clocks.RunTime++; clocks.SaveTime++;
Require(Stable(filtered).AsSpan().SequenceEqual(Stable(clocks)), "existing clock-only tolerance remains");
var stabilize = verifier.GetMethod("StabilizeNativeJson", BindingFlags.NonPublic | BindingFlags.Static)!;
byte[] CanonicalBytes(byte[] json) => (byte[])stabilize.Invoke(null, [json])!;
byte[] Canonical(string json) => CanonicalBytes(System.Text.Encoding.UTF8.GetBytes(json));
string History(string choice) => "{\"map_point_history\":[[{\"player_stats\":[{\"event_choices\":[{" + choice + "}]}]}]]}";
bool Same(string left, string right) => Canonical(left).AsSpan().SequenceEqual(Canonical(right));
Require(Same(History("\"variables\":{}"), History("")), "only exact-path empty object and missing are equivalent");
foreach (string value in new[] { "null", "[]", "\"\"", "0", "{\"x\":{}}", "{\"x\":1}" })
    Require(!Same(History("\"variables\":" + value), History("")), "nonempty or nonobject variable is not omitted: " + value);
Require(!Same("{\"variables\":{}}", "{}"), "same property at root is not omitted");
Require(!Same("{\"extra_fields\":{\"variables\":{}}}", "{\"extra_fields\":{}}"), "same property in unrelated payload is not omitted");
Require(!Same("{\"map_point_history\":[{\"player_stats\":[{\"event_choices\":[{\"variables\":{}}]}]}]}",
    "{\"map_point_history\":[{\"player_stats\":[{\"event_choices\":[{}]}]}]}"), "wrong intermediate shape receives no equivalence");
Require(!Same(History("\"title\":\"before\",\"variables\":{}"), History("\"title\":\"after\"")), "choice title changes remain detected");
Require(!Same(History("\"variables\":{}"), "{\"map_point_history\":[[{\"player_stats\":[{\"event_choices\":[]}]}]]}"), "choice removal remains detected");
Require(!Same("{\"acts\":[{\"saved_map\":{\"points\":[{\"can_modify\":false}]}}]}",
    "{\"acts\":[{\"saved_map\":{\"points\":[{\"can_modify\":true}]}}]}"), "map false-to-true remains detected");
bool duplicateRejected = false;
try { Canonical(History("\"variables\":{},\"variables\":{\"x\":1}")); }
catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException or JsonException) { duplicateRejected = true; }
Require(duplicateRejected, "duplicate keys cannot be canonicalized away");
foreach (string token in new[] { "1.00", "1e+3", "999999999999999999999999999999999", "-0" })
{
    using var lexical = JsonDocument.Parse(Canonical("{\"payload\":{\"number\":" + token + "}}"));
    Require(lexical.RootElement.GetProperty("payload").GetProperty("number").GetRawText() == token,
        "unrelated numeric lexeme is preserved: " + token);
}
using (var duplicate = JsonDocument.Parse(Canonical("{\"payload\":{\"x\":1,\"x\":2}}")))
    Require(duplicate.RootElement.GetProperty("payload").EnumerateObject().Count(p => p.Name == "x") == 2,
        "unvisited duplicate keys remain present for strict Compare rejection");
object originalSnapshot = capture.Invoke(null, [filtered])!;
byte[] nativeBytes = (byte[])originalSnapshot.GetType().GetProperty("NativeJson")!.GetValue(originalSnapshot)!;
using var nativeDoc = JsonDocument.Parse(nativeBytes);
Require(nativeDoc.RootElement.GetProperty("map_point_history")[0][0].GetProperty("player_stats")[0]
    .GetProperty("event_choices")[0].GetProperty("variables").EnumerateObject().Count() == 0,
    "original NativeJson still contains filtered-empty object");
if (args.Length == 2)
    Require(CanonicalBytes(File.ReadAllBytes(args[0])).AsSpan().SequenceEqual(CanonicalBytes(File.ReadAllBytes(args[1]))),
        "captured native expected/readback pair matches without changing source snapshots");
else if (args.Length != 0) throw new ArgumentException("Pass either no arguments or two captured native JSON paths.");
Console.WriteLine("ISSUE287_EVENT_VARIABLES_CLR_CONTRACTS_PASS; NATIVE_GAMEPLAY_NOT_RUN");
