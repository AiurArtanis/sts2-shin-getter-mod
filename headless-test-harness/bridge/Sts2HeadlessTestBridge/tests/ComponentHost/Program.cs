using System.Text.Json;
using Sts2HeadlessTestBridge.Contract;
using Sts2HeadlessTestBridge.Dispatch;
using Sts2HeadlessTestBridge.Transport;

string pipeName = RequireEnvironment("STS2_TEST_PIPE");
string sessionId = RequireEnvironment("STS2_TEST_SESSION_ID");
string instanceId = RequireEnvironment("STS2_TEST_INSTANCE_ID");
byte[] token = DecodeBase64Url(RequireEnvironment("STS2_TEST_TOKEN"));
string outputRoot = RequireEnvironment("STS2_TEST_OUTPUT_ROOT");
int replayCapacity = OptionalPositiveInt("STS2_TEST_COMPONENT_REPLAY_CAPACITY", 2048);
int outboundCapacity = OptionalPositiveInt("STS2_TEST_COMPONENT_OUTBOUND_CAPACITY", 512);
int maxLineBytes = OptionalPositiveInt("STS2_TEST_COMPONENT_MAX_LINE_BYTES", 1024 * 1024);
string? writerReleaseFile = Environment.GetEnvironmentVariable("STS2_TEST_COMPONENT_WRITER_RELEASE_FILE");

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
    (request, connection, cancellationToken) => executor!.HandleAsync(request, connection, cancellationToken),
    replayCapacity: replayCapacity,
    outboundCriticalCapacity: outboundCapacity,
    limits: new ProtocolLimits(MaxLineBytes: maxLineBytes),
    writerBarrier: writerReleaseFile is null
        ? null
        : async cancellationToken =>
        {
            while (!File.Exists(writerReleaseFile))
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        });
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

static int OptionalPositiveInt(string name, int fallback)
{
    string? raw = Environment.GetEnvironmentVariable(name);
    return int.TryParse(raw, out int value) && value > 0 ? value : fallback;
}

sealed class ComponentExecutor(ProtocolServer server)
{
    private readonly RequestIdempotencyGate _idempotency = new();
    private string? _mutationOwner;
    private long _choiceGeneration;
    private string? _choiceHandle;
    private string? _choiceCandidate;
    private string? _actionHandle;
    private int _delayedExecutionCount;

    public async Task HandleAsync(
        JsonElement request,
        ProtocolConnection connection,
        CancellationToken cancellationToken)
    {
        string requestId = ProtocolContract.RequireString(request, "request_id");
        RequestIdempotencyDecision decision = _idempotency.Accept(request);
        if (decision.Status == RequestIdempotencyStatus.Conflict)
        {
            await Failed(connection, requestId, ErrorCodes.IdempotencyConflict, "request_id payload conflict", cancellationToken);
            return;
        }
        if (decision.Status == RequestIdempotencyStatus.Replay)
        {
            CachedRequestTerminal terminal = decision.Terminal!;
            await connection.SendAsync(
                terminal.Type,
                requestId,
                RequestIdempotencyGate.ReplayFields(terminal),
                replayed: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }
        if (decision.Status == RequestIdempotencyStatus.InFlight)
        {
            await connection.SendAsync("accepted", requestId, replayed: true, cancellationToken: cancellationToken);
            return;
        }

        await connection.SendAsync("accepted", requestId, cancellationToken: cancellationToken);
        await connection.SendAsync(
            "started", requestId,
            new Dictionary<string, object?> { ["engine_frame"] = Environment.TickCount64 },
            cancellationToken: cancellationToken);

        if (server.CaseFailure is ProtocolCaseFailure transportFailure)
        {
            await Failed(connection, requestId, transportFailure.Code, transportFailure.Message, cancellationToken);
            return;
        }

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
            case "test.delayed":
                int delayMs = args.TryGetProperty("delay_ms", out JsonElement delay)
                    ? delay.GetInt32()
                    : 250;
                if (delayMs is < 1 or > 60_000)
                {
                    await Failed(connection, requestId, ErrorCodes.InvalidArgument, "delay_ms must be in [1, 60000]", cancellationToken);
                    break;
                }
                int executionCount = Interlocked.Increment(ref _delayedExecutionCount);
                _ = CompleteDelayedAsync(connection, requestId, delayMs, executionCount, cancellationToken);
                break;
            case "test.action_parent":
                if (_mutationOwner is not null)
                {
                    await Failed(connection, requestId, ErrorCodes.MutationBusy, "mutation lane is busy", cancellationToken);
                    break;
                }
                _mutationOwner = requestId;
                _actionHandle = $"action:{connection.Handshake.ProcessEpoch}:7";
                await connection.SendAsync(
                    "event", requestId,
                    new Dictionary<string, object?>
                    {
                        ["name"] = "action_enqueued",
                        ["data"] = new Dictionary<string, object?>
                        {
                            ["action_handle"] = _actionHandle,
                            ["action_id"] = 7,
                            ["owner_id"] = 1,
                            ["action_type"] = "ComponentAction",
                            ["correlation"] = "exact_reference",
                        },
                    },
                    cancellationToken: cancellationToken);
                break;
            case "test.action_complete":
                if (!ValidActionContinuation(args))
                {
                    await Failed(connection, requestId, ErrorCodes.StaleHandle, "action continuation is stale", cancellationToken);
                    break;
                }
                string actionParent = _mutationOwner!;
                string actionHandle = _actionHandle!;
                await Completed(
                    connection,
                    requestId,
                    new Dictionary<string, object?> { ["released"] = true },
                    cancellationToken);
                await connection.SendAsync(
                    "event", actionParent,
                    new Dictionary<string, object?>
                    {
                        ["name"] = "action_finished",
                        ["data"] = new Dictionary<string, object?>
                        {
                            ["action_handle"] = actionHandle,
                            ["action_id"] = 7,
                            ["owner_id"] = 1,
                            ["action_type"] = "ComponentAction",
                            ["state"] = "Finished",
                        },
                    },
                    cancellationToken: cancellationToken);
                JsonElement actionTerminal = await connection.SendAsync(
                    "completed", actionParent,
                    new Dictionary<string, object?>
                    {
                        ["result"] = new Dictionary<string, object?>
                        {
                            ["completion"] = "queue_settled",
                            ["queue_empty"] = true,
                            ["executor_running"] = false,
                        },
                    },
                    cancellationToken: cancellationToken);
                _idempotency.Complete(actionParent, actionTerminal);
                _mutationOwner = null;
                _actionHandle = null;
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
                _idempotency.Complete(parent, parentTerminal);
                _mutationOwner = null;
                _choiceHandle = null;
                _choiceCandidate = null;
                break;
            case "test.mutation":
                if (server.CaseFailure is ProtocolCaseFailure frozen)
                    await Failed(connection, requestId, frozen.Code, frozen.Message, cancellationToken);
                else if (_mutationOwner is not null)
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

    private bool ValidActionContinuation(JsonElement args)
    {
        if (_mutationOwner is null || _actionHandle is null)
            return false;
        if (!args.TryGetProperty("blocked_request_id", out JsonElement blocked)
            || blocked.GetString() != _mutationOwner)
        {
            return false;
        }
        return args.TryGetProperty("action_handle", out JsonElement handle)
            && handle.GetString() == _actionHandle;
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
        _idempotency.Complete(requestId, terminal);
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
            _idempotency.Complete(requestId, terminal);
    }

    private async Task CompleteDelayedAsync(
        ProtocolConnection connection,
        string requestId,
        int delayMs,
        int executionCount,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            if (server.CaseFailure is ProtocolCaseFailure failure)
            {
                await Failed(connection, requestId, failure.Code, failure.Message, cancellationToken).ConfigureAwait(false);
                return;
            }
            await Completed(
                connection,
                requestId,
                new Dictionary<string, object?>
                {
                    ["delayed"] = true,
                    ["execution_count"] = executionCount,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The process is shutting down; there is no future connection to notify.
        }
    }
}
