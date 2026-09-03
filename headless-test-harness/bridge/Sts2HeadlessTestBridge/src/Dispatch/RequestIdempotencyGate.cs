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
    private readonly LinkedList<string> _lru = new();

    public RequestIdempotencyDecision Accept(JsonElement request)
    {
        string requestId = ProtocolContract.RequireString(request, "request_id");
        string digest = RequestDigest(request);
        lock (_gate)
        {
            if (!_entries.TryGetValue(requestId, out Entry? entry))
            {
                var node = _lru.AddLast(requestId);
                _entries.Add(requestId, new Entry(digest, node));
                return new RequestIdempotencyDecision(RequestIdempotencyStatus.New);
            }
            Touch(entry);
            if (!StringComparer.Ordinal.Equals(entry.Digest, digest))
                return new RequestIdempotencyDecision(RequestIdempotencyStatus.Conflict);
            return entry.Terminal is null
                ? new RequestIdempotencyDecision(RequestIdempotencyStatus.InFlight)
                : new RequestIdempotencyDecision(RequestIdempotencyStatus.Replay, entry.Terminal);
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
            entry.Terminal ??= new CachedRequestTerminal(type, serializedFields);
            Touch(entry);
            TrimCompleted();
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

    private static string RequestDigest(JsonElement request)
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

    private void Touch(Entry entry)
    {
        _lru.Remove(entry.Node);
        _lru.AddLast(entry.Node);
    }

    private void TrimCompleted()
    {
        while (_entries.Count > _capacity)
        {
            LinkedListNode<string>? candidate = _lru.First;
            while (candidate is not null && _entries[candidate.Value].Terminal is null)
                candidate = candidate.Next;
            if (candidate is null)
                return;
            _entries.Remove(candidate.Value);
            _lru.Remove(candidate);
        }
    }

    private sealed class Entry(string digest, LinkedListNode<string> node)
    {
        public string Digest { get; } = digest;
        public LinkedListNode<string> Node { get; } = node;
        public CachedRequestTerminal? Terminal { get; set; }
    }
}
