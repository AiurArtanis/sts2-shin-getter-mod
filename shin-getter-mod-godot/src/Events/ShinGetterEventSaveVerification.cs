#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Saves;

namespace ShinGetterMod.Events;

// The native save manager swallows write errors. A completed Task is not proof of a write.
// Once Saved fires, readback failures must never reopen a possibly persisted reward route.
internal static class ShinGetterEventSaveVerification
{
    internal sealed record MapFlag(string Path, bool Value);
    internal sealed record Snapshot(byte[] NativeJson, byte[] StableJson, MapFlag[] MapFlags);
    internal sealed record Difference(string Path, string Expected, string Readback);
    internal sealed record Comparison(Snapshot Expected, Snapshot Readback, Difference[] Differences, bool BytesEqual);
    // An isolated test driver may save these binary snapshots to its own evidence root.
    // The sink must not write saves; failure cannot mask the original transaction result.
    internal static Action<Comparison>? DiagnosticSink { get; set; }

    // Native ToSave is called again by SaveRun. Only the two clocks may advance;
    // empty event-history variable objects also have an omitted native equivalent.
    internal static Snapshot Capture(SerializableRun save)
    {
        MapFlag[] flags = ShinGetterEventMapSaveScope.MapPoints(save).Select(p => new MapFlag(p.Path, p.Point.CanBeModified)).ToArray();
        // Explicit map values come directly from this DTO, never from a lossy
        // serialize/deserialize normalization of expected or readback.
        using var scope = ShinGetterEventMapSaveScope.InspectSnapshot(save);
        byte[] native = JsonSerializer.SerializeToUtf8Bytes(save, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
        return new Snapshot(native, StabilizeNativeJson(native), flags);
    }

    internal static byte[] StabilizeNativeJson(byte[] native)
    {
        JsonObject root = JsonNode.Parse(native)!.AsObject();
        root.Remove("save_time");
        root.Remove("run_time");
        // The native converter can filter a nonempty dictionary into {}. On readback
        // its empty dictionary is omitted. No persisted member may be discarded.
        if (root["map_point_history"] is JsonArray acts)
            foreach (JsonNode? act in acts)
            {
                if (act is not JsonArray points) continue;
                foreach (JsonNode? point in points)
                {
                    if (point is not JsonObject history || history["player_stats"] is not JsonArray players) continue;
                    foreach (JsonNode? player in players)
                    {
                        if (player is not JsonObject stats || stats["event_choices"] is not JsonArray choices) continue;
                        foreach (JsonNode? choice in choices)
                            if (choice is JsonObject entry && entry["variables"] is JsonObject { Count: 0 })
                                entry.Remove("variables");
                    }
                }
            }
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
            root.WriteTo(writer);
        return stream.ToArray();
    }

    internal static string StableRunSnapshot(SerializableRun save) => Encoding.UTF8.GetString(Capture(save).StableJson);

    internal static void Compare(Snapshot expected, Snapshot readback)
    {
        bool equal = expected.StableJson.AsSpan().SequenceEqual(readback.StableJson);
        using JsonDocument left = JsonDocument.Parse(expected.StableJson);
        using JsonDocument right = JsonDocument.Parse(readback.StableJson);
        List<Difference> differences = new();
        FindDifferences(left.RootElement, right.RootElement, "$", differences);
        Comparison comparison = new(expected, readback, differences.ToArray(), equal);
        try { DiagnosticSink?.Invoke(comparison); }
        catch (Exception ex)
        {
            MegaCrit.Sts2.Core.Logging.Log.Error($"issue#238 diagnostic sink failed ({ex.GetType().Name}).");
        }
        if (!equal)
        {
            string paths = string.Join(", ", differences.Take(12).Select(d => d.Path));
            throw new InvalidOperationException($"Native event save readback differs: {differences.Count} semantic paths; first paths: {paths}. "
                + (differences.Count == 0 ? "JSON ordering/text differs despite matching values." : "Complete differences retained in diagnostic comparison."));
        }
    }

    private static string Summary(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => $"string(length={value.GetString()!.Length},sha256={Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.GetString()!)))})",
        JsonValueKind.Array => $"array(length={value.GetArrayLength()})",
        JsonValueKind.Object => "object",
        _ => value.GetRawText()
    };

    private static void FindDifferences(JsonElement expected, JsonElement readback, string path, List<Difference> result)
    {
        if (expected.ValueKind != readback.ValueKind)
        {
            result.Add(new(path, Summary(expected), Summary(readback))); return;
        }
        if (expected.ValueKind == JsonValueKind.Object)
        {
            var left = expected.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
            var right = readback.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
            foreach (string name in left.Keys.Union(right.Keys).OrderBy(n => n, StringComparer.Ordinal))
            {
                bool hasLeft = left.TryGetValue(name, out var l), hasRight = right.TryGetValue(name, out var r);
                string child = path + "['" + name.Replace("'", "\\'") + "']";
                if (!hasLeft || !hasRight) result.Add(new(child, hasLeft ? Summary(l) : "missing", hasRight ? Summary(r) : "missing"));
                else FindDifferences(l, r, child, result);
            }
        }
        else if (expected.ValueKind == JsonValueKind.Array)
        {
            int length = Math.Max(expected.GetArrayLength(), readback.GetArrayLength());
            for (int i = 0; i < length; i++)
            {
                if (i >= expected.GetArrayLength() || i >= readback.GetArrayLength())
                    result.Add(new($"{path}[{i}]", i < expected.GetArrayLength() ? Summary(expected[i]) : "missing",
                        i < readback.GetArrayLength() ? Summary(readback[i]) : "missing"));
                else FindDifferences(expected[i], readback[i], $"{path}[{i}]", result);
            }
        }
        else if (expected.GetRawText() != readback.GetRawText()) result.Add(new(path, Summary(expected), Summary(readback)));
    }

    internal sealed class ConfirmedWriteReadbackException : Exception
    {
        internal ConfirmedWriteReadbackException(Exception inner)
            : base("Native write completed but event save readback could not be verified.", inner) { }
    }

    internal static async Task Verify(Func<Task> write, Func<bool> wasSaved, Action readback)
    {
        try
        {
            await write();
            if (!wasSaved()) throw new InvalidOperationException("Native event save did not report a completed write.");
            readback();
        }
        catch (Exception ex) when (wasSaved())
        {
            throw new ConfirmedWriteReadbackException(ex);
        }
    }
}
