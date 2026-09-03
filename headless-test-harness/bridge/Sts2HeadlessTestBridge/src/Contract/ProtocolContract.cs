using System.Text.Json;

namespace Sts2HeadlessTestBridge.Contract;

public static class ProtocolContract
{
    private static readonly HashSet<string> Types =
    ["challenge", "hello", "hello_ack", "request", "accepted", "started", "event", "completed", "failed"];

    public static void ValidateEnvelope(JsonElement value)
    {
        RequireString(value, "protocol", "sts2-test/v1");
        RequireInt(value, "schema_version", 1);
        string type = RequireString(value, "type");
        if (!Types.Contains(type))
            throw new InvalidDataException($"unknown protocol type: {type}");
        if (type is "request" or "accepted" or "started" or "event" or "completed" or "failed")
        {
            RequireString(value, "request_id");
            RequireString(value, "instance_id");
            RequireLong(value, "seq");
        }
        if (type == "request")
        {
            RequireString(value, "command");
            RequireObject(value, "args");
            RequireString(value, "wait_for");
            RequireLong(value, "timeout_ms");
        }
        if (type == "event")
        {
            RequireString(value, "name");
            RequireObject(value, "data");
        }
        if (type == "completed")
            RequireObject(value, "result");
        if (type == "failed")
            RequireObject(value, "error");
    }

    public static string RequireString(JsonElement value, string name, string? expected = null)
    {
        if (!value.TryGetProperty(name, out JsonElement child) || child.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"required string missing: {name}");
        string result = child.GetString() ?? "";
        if (expected is not null && !StringComparer.Ordinal.Equals(result, expected))
            throw new InvalidDataException($"unexpected {name}: {result}");
        return result;
    }

    public static long RequireLong(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement child) || !child.TryGetInt64(out long result))
            throw new InvalidDataException($"required integer missing: {name}");
        return result;
    }

    private static void RequireInt(JsonElement value, string name, int expected)
    {
        if (RequireLong(value, name) != expected)
            throw new InvalidDataException($"unexpected {name}");
    }

    private static void RequireObject(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement child) || child.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"required object missing: {name}");
    }
}
