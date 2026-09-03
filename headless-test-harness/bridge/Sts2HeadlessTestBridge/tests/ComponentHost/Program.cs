using System.Security.Cryptography;
using System.Text.Json;
using Sts2HeadlessTestBridge.Contract;
using Sts2HeadlessTestBridge.Transport;

string pipeName = RequireEnvironment("STS2_TEST_PIPE");
string sessionId = RequireEnvironment("STS2_TEST_SESSION_ID");
string instanceId = RequireEnvironment("STS2_TEST_INSTANCE_ID");
byte[] token = DecodeBase64Url(RequireEnvironment("STS2_TEST_TOKEN"));
string outputRoot = RequireEnvironment("STS2_TEST_OUTPUT_ROOT");

ComponentExecutor? executor = null;
ProtocolServer? server = null;
server = new ProtocolServer(
    pipeName,
    sessionId,
    instanceId,
    token,
    context => JsonSerializer.SerializeToElement(new
    {
        session_id = context.SessionId,
        instance_id = context.InstanceId,
        process_epoch = context.ProcessEpoch,
        connection_id = context.ConnectionId,
        negotiated_protocol = context.NegotiatedProtocol,
        game = new
        {
            version = "0.111.0-component",
            commit = (string?)null,
            assembly_sha256 = new string('1', 64),
            assembly_mvid = "11111111-1111-1111-1111-111111111111",
        },
        adapter = new { id = "component-test-host", assembly_sha256 = new string('2', 64) },
        runtime = new
        {
            main_thread_id = Environment.CurrentManagedThreadId,
            main_thread_probe = true,
            display_driver = "component",
            audio_driver = "component",
            user_data_path = outputRoot.Replace('\\', '/'),
        },
        capabilities = new Dictionary<string, object?>
        {
            ["typed_card_play"] = new { state = "partial", reason = "component host has no game runtime" },
            ["card_select_local_selector"] = new { state = "partial", reason = "component continuation only" },
            ["pixel_output"] = new { state = "unavailable", reason = "component host has no renderer" },
        },
    }),
    (request, connection, cancellationToken) => executor!.HandleAsync(request, connection, cancellationToken));
executor = new ComponentExecutor(server);
await server.RunAsync();
return 0;

static string RequireEnvironment(string name) =>
    Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"missing environment variable: {name}");

static byte[] DecodeBase64Url(string value)
{
    string padded = value.Replace('-', '+').Replace('_', '/');
    padded += new string('=', (4 - padded.Length % 4) % 4);
    return Convert.FromBase64String(padded);
}

sealed class ComponentExecutor(ProtocolServer server)
{
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private string? _mutationOwner;
    private long _choiceGeneration;
    private string? _choiceHandle;
    private string? _choiceCandidate;

    public async Task HandleAsync(
        JsonElement request,
        ProtocolConnection connection,
        CancellationToken cancellationToken)
    {
        string requestId = ProtocolContract.RequireString(request, "request_id");
        string digest = RequestDigest(request);
        if (_cache.TryGetValue(requestId, out CacheEntry? cached))
        {
            if (!StringComparer.Ordinal.Equals(cached.Digest, digest))
            {
                await Failed(connection, requestId, ErrorCodes.IdempotencyConflict, "request_id payload conflict", cancellationToken);
                return;
            }
            if (cached.Terminal is JsonElement terminal)
            {
                string type = ProtocolContract.RequireString(terminal, "type");
                var fields = new Dictionary<string, object?>();
                foreach (JsonProperty property in terminal.EnumerateObject())
                {
                    if (property.Name is not ("protocol" or "schema_version" or "type" or "seq" or "request_id" or "instance_id" or "replayed"))
                        fields[property.Name] = property.Value.Clone();
                }
                await connection.SendAsync(
                    type, requestId, fields, replayed: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                return;
            }
            await connection.SendAsync("accepted", requestId, replayed: true, cancellationToken: cancellationToken);
            return;
        }

        _cache[requestId] = new CacheEntry(digest, null);
        await connection.SendAsync("accepted", requestId, cancellationToken: cancellationToken);
        await connection.SendAsync(
            "started", requestId,
            new Dictionary<string, object?> { ["engine_frame"] = Environment.TickCount64 },
            cancellationToken: cancellationToken);

        string command = ProtocolContract.RequireString(request, "command");
        JsonElement args = request.GetProperty("args");
        switch (command)
        {
            case "runtime.ping":
                await Completed(connection, requestId, new Dictionary<string, object?>
                {
                    ["backend"] = "component_test_host",
                    ["frame"] = Environment.TickCount64,
                    ["queue_depth"] = 0,
                }, cancellationToken);
                break;
            case "runtime.capabilities":
                await Completed(connection, requestId, new Dictionary<string, object?>
                {
                    ["backend"] = "component_test_host",
                    ["states"] = new Dictionary<string, string> { ["typed_card_play"] = "partial" },
                }, cancellationToken);
                break;
            case "test.choice_parent":
                if (_mutationOwner is not null)
                {
                    await Failed(connection, requestId, ErrorCodes.MutationBusy, "mutation lane is busy", cancellationToken);
                    break;
                }
                _mutationOwner = requestId;
                _choiceGeneration++;
                _choiceHandle = $"choice:{connection.Handshake.ProcessEpoch}:player-1:g{_choiceGeneration}";
                _choiceCandidate = $"choice-item:{_choiceHandle}:0";
                await connection.SendAsync(
                    "event", requestId,
                    new Dictionary<string, object?>
                    {
                        ["name"] = "choice_required",
                        ["data"] = new Dictionary<string, object?>
                        {
                            ["choice_handle"] = _choiceHandle,
                            ["owner_id"] = 1,
                            ["choice_generation"] = _choiceGeneration,
                            ["kind"] = "card",
                            ["min"] = 1,
                            ["max"] = 1,
                            ["candidates"] = new[] { new Dictionary<string, object?> { ["handle"] = _choiceCandidate, ["model_id"] = "CARD.COMPONENT" } },
                            ["blocked_action_id"] = 1,
                        },
                    },
                    cancellationToken: cancellationToken);
                break;
            case "choice.select":
                if (!ValidChoice(args))
                {
                    await Failed(connection, requestId, ErrorCodes.StaleHandle, "choice continuation is stale", cancellationToken);
                    break;
                }
                string parent = _mutationOwner!;
                await Completed(connection, requestId, new Dictionary<string, object?> { ["selector_accepted"] = true }, cancellationToken);
                JsonElement parentTerminal = await connection.SendAsync(
                    "completed", parent,
                    new Dictionary<string, object?> { ["result"] = new Dictionary<string, object?> { ["completion"] = "queue_settled" } },
                    cancellationToken: cancellationToken);
                CacheTerminal(parent, parentTerminal);
                _mutationOwner = null;
                _choiceHandle = null;
                _choiceCandidate = null;
                break;
            case "test.mutation":
                if (_mutationOwner is not null)
                    await Failed(connection, requestId, ErrorCodes.MutationBusy, "mutation lane is busy", cancellationToken);
                else
                    await Completed(connection, requestId, new Dictionary<string, object?> { ["completion"] = "immediate" }, cancellationToken);
                break;
            case "runtime.shutdown":
                if (_mutationOwner is not null)
                {
                    await Failed(connection, requestId, ErrorCodes.CancelUnsafe, "active mutation prevents shutdown", cancellationToken);
                    break;
                }
                await Completed(connection, requestId, new Dictionary<string, object?> { ["flushed"] = true }, cancellationToken);
                server.RequestStop();
                break;
            default:
                await Failed(connection, requestId, ErrorCodes.InvalidArgument, $"unknown command: {command}", cancellationToken);
                break;
        }
    }

    private bool ValidChoice(JsonElement args)
    {
        if (_mutationOwner is null || _choiceHandle is null || _choiceCandidate is null)
            return false;
        if (!args.TryGetProperty("blocked_request_id", out JsonElement blocked) || blocked.GetString() != _mutationOwner)
            return false;
        if (!args.TryGetProperty("owner_id", out JsonElement owner) || owner.GetInt64() != 1)
            return false;
        if (!args.TryGetProperty("choice_handle", out JsonElement handle) || handle.GetString() != _choiceHandle)
            return false;
        if (!args.TryGetProperty("choice_generation", out JsonElement generation) || generation.GetInt64() != _choiceGeneration)
            return false;
        if (!args.TryGetProperty("candidates", out JsonElement candidates) || candidates.GetArrayLength() != 1)
            return false;
        return candidates[0].GetString() == _choiceCandidate;
    }

    private async Task Completed(
        ProtocolConnection connection,
        string requestId,
        Dictionary<string, object?> result,
        CancellationToken cancellationToken)
    {
        JsonElement terminal = await connection.SendAsync(
            "completed", requestId,
            new Dictionary<string, object?> { ["result"] = result },
            cancellationToken: cancellationToken);
        CacheTerminal(requestId, terminal);
    }

    private async Task Failed(
        ProtocolConnection connection,
        string requestId,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        JsonElement terminal = await connection.SendAsync(
            "failed", requestId,
            new Dictionary<string, object?> { ["error"] = ProtocolServer.Error(code, message) },
            cancellationToken: cancellationToken);
        // A conflicting duplicate is not the terminal for the original request.
        if (code != ErrorCodes.IdempotencyConflict)
            CacheTerminal(requestId, terminal);
    }

    private void CacheTerminal(string requestId, JsonElement terminal)
    {
        if (_cache.TryGetValue(requestId, out CacheEntry? cached))
            _cache[requestId] = cached with { Terminal = terminal.Clone() };
    }

    private static string RequestDigest(JsonElement request)
    {
        var payload = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in request.EnumerateObject())
        {
            if (property.Name is not ("seq" or "wall_time" or "engine_frame" or "logical_time" or "connection_id" or "broker_epoch"))
                payload[property.Name] = property.Value.Clone();
        }
        JsonElement element = JsonSerializer.SerializeToElement(payload);
        return Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Serialize(element)));
    }

    private sealed record CacheEntry(string Digest, JsonElement? Terminal);
}
