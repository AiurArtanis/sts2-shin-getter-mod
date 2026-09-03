using System.Security.Cryptography;
using System.Text.Json;
using Sts2HeadlessTestBridge.Contract;

namespace Sts2HeadlessTestBridge.Dispatch;

public enum RequestIdempotencyStatus
{
    New,
    InFlight,
    Replay,
    Conflict,
    Expired,
}

public sealed record CachedRequestTerminal(string Type, JsonElement Fields);

public sealed record RequestIdempotencyDecision(
    RequestIdempotencyStatus Status,
    CachedRequestTerminal? Terminal = null);

/// <summary>
/// Thread-safe authority for request-id payload identity and terminal replay.
/// This exact component is shared by the Godot bridge and ComponentHost tests.
/// </summary>
public sealed class RequestIdempotencyGate(int capacity = 256)
{
    private static readonly HashSet<string> IgnoredDigestFields =
    ["seq", "wall_time", "engine_frame", "logical_time", "connection_id", "broker_epoch"];

    private readonly int _capacity = capacity > 0
        ? capacity
        : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _terminalLru = new();
    private int _retainedTerminalCount;

    public RequestIdempotencyDecision Accept(JsonElement request)
    {
        string requestId = ProtocolContract.RequireString(request, "request_id");
        string digest = RequestPayloadSha256(request);
        lock (_gate)
        {
            if (!_entries.TryGetValue(requestId, out Entry? entry))
            {
                _entries.Add(requestId, new Entry(digest));
                return new RequestIdempotencyDecision(RequestIdempotencyStatus.New);
            }
            if (!StringComparer.Ordinal.Equals(entry.Digest, digest))
                return new RequestIdempotencyDecision(RequestIdempotencyStatus.Conflict);
            if (!entry.Completed)
                return new RequestIdempotencyDecision(RequestIdempotencyStatus.InFlight);
            if (entry.Terminal is null)
                return new RequestIdempotencyDecision(RequestIdempotencyStatus.Expired);
            TouchTerminal(entry);
            return new RequestIdempotencyDecision(RequestIdempotencyStatus.Replay, entry.Terminal);
        }
    }

    public void Complete(
        string requestId,
        string type,
        IReadOnlyDictionary<string, object?> fields)
    {
        if (type is not ("completed" or "failed"))
            throw new ArgumentException("cached request terminal must be completed or failed", nameof(type));
        JsonElement serializedFields = JsonSerializer.SerializeToElement(fields).Clone();
        lock (_gate)
        {
            if (!_entries.TryGetValue(requestId, out Entry? entry))
                throw new InvalidOperationException($"request_id is not registered: {requestId}");
            if (entry.Completed)
                return;
            entry.Completed = true;
            entry.Terminal = new CachedRequestTerminal(type, serializedFields);
            entry.TerminalNode = _terminalLru.AddLast(requestId);
            _retainedTerminalCount++;
            TrimTerminalPayloads();
        }
    }

    public void Complete(string requestId, JsonElement terminal)
    {
        string type = ProtocolContract.RequireString(terminal, "type");
        if (type is not ("completed" or "failed"))
            throw new ArgumentException("request terminal must be completed or failed", nameof(terminal));

        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (JsonProperty property in terminal.EnumerateObject())
        {
            if (property.Name is not (
                "protocol" or "schema_version" or "type" or "seq" or
                "request_id" or "instance_id" or "replayed"))
            {
                fields[property.Name] = property.Value.Clone();
            }
        }
        Complete(requestId, type, fields);
    }

    public static Dictionary<string, object?> ReplayFields(CachedRequestTerminal terminal)
    {
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (JsonProperty property in terminal.Fields.EnumerateObject())
            fields[property.Name] = property.Value.Clone();
        return fields;
    }

    public static string RequestPayloadSha256(JsonElement request)
    {
        var payload = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in request.EnumerateObject())
        {
            if (!IgnoredDigestFields.Contains(property.Name))
                payload[property.Name] = property.Value.Clone();
        }
        JsonElement canonical = JsonSerializer.SerializeToElement(payload);
        return Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Serialize(canonical)));
    }

    private void TouchTerminal(Entry entry)
    {
        if (entry.TerminalNode is null)
            return;
        _terminalLru.Remove(entry.TerminalNode);
        _terminalLru.AddLast(entry.TerminalNode);
    }

    private void TrimTerminalPayloads()
    {
        while (_retainedTerminalCount > _capacity)
        {
            LinkedListNode<string> candidate = _terminalLru.First
                ?? throw new InvalidOperationException("retained terminal count is inconsistent");
            Entry entry = _entries[candidate.Value];
            _terminalLru.Remove(candidate);
            entry.Terminal = null;
            entry.TerminalNode = null;
            _retainedTerminalCount--;
        }
    }

    private sealed class Entry(string digest)
    {
        public string Digest { get; } = digest;
        public bool Completed { get; set; }
        public CachedRequestTerminal? Terminal { get; set; }
        public LinkedListNode<string>? TerminalNode { get; set; }
    }
}
