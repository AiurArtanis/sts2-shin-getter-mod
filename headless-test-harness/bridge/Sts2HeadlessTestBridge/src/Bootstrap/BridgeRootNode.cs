using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Modding;
using Sts2HeadlessTestBridge.Contract;
using Sts2HeadlessTestBridge.Dispatch;
using Sts2HeadlessTestBridge.Security;
using Sts2HeadlessTestBridge.Transport;
using Sts2HeadlessTestBridge.State;

namespace Sts2HeadlessTestBridge.Bootstrap;

public partial class BridgeRootNode : Node
{
    private BridgeConfiguration? _configuration;
    private MainThreadDispatcher? _dispatcher;
    private RequestExecution? _execution;
    private ActionObserver? _actions;
    private ChoiceBroker? _choices;
    private RequestIdempotencyGate? _idempotency;
    private ProtocolServer? _server;
    private Task? _serverTask;
    private CancellationTokenSource? _serverCancellation;
    private bool _serverFaultObserved;
    private int _mainThreadId;
    private bool _mainThreadProbe;

    public override void _Ready()
    {
        _configuration = BridgeConfiguration.Load();
        if (_configuration is null)
        {
            QueueFree();
            return;
        }
        _mainThreadId = System.Environment.CurrentManagedThreadId;
        _mainThreadProbe = true;
        _dispatcher = new MainThreadDispatcher();
        string processEpoch = Guid.NewGuid().ToString("D");
        var snapshots = new SnapshotBuilder(
            _configuration,
            processEpoch,
            () => ReleaseInfo("version", "unknown") ?? "unknown",
            () => ReleaseInfo("commit", null),
            actionSnapshot: () => _actions?.Snapshot() ?? new Dictionary<string, object?>(),
            choiceSnapshot: () => _choices?.Snapshot() ?? Array.Empty<Dictionary<string, object?>>());
        _actions = new ActionObserver(snapshots.Handles);
        _choices = new ChoiceBroker(processEpoch, _actions);
        _idempotency = new RequestIdempotencyGate();
        _actions.Synchronize();
        _choices.Synchronize();
        var registry = new CommandRegistry(snapshots, _actions, _choices);
        _execution = new RequestExecution(
            registry,
            _actions,
            _choices,
            _idempotency,
            () => _server?.CaseFailure,
            GetTree(),
            _mainThreadId);
        _server = new ProtocolServer(
            _configuration.PipeName,
            _configuration.SessionId,
            _configuration.InstanceId,
            _configuration.Token,
            CreateAcknowledgementBody,
            AcceptRequestAsync,
            processEpoch);
        _serverCancellation = new CancellationTokenSource();
        _serverTask = Task.Run(() => _server.RunAsync(_serverCancellation.Token));
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_dispatcher is null || _execution is null)
            return;
        if (_server?.CaseFailure is ProtocolCaseFailure transportFailure)
            _execution.ApplyTransportFailure(transportFailure);
        if (!_serverFaultObserved && _serverTask is { IsFaulted: true })
        {
            _serverFaultObserved = true;
            string message = _serverTask.Exception?.GetBaseException().Message ?? "protocol server faulted";
            GD.PushError($"Sts2HeadlessTestBridge protocol server faulted: {message}");
            _execution.ApplyTransportFailure(
                new ProtocolCaseFailure(
                    ErrorCodes.ProcessExit,
                    message,
                    new Dictionary<string, object?>()));
        }
        foreach (PendingRequest request in _dispatcher.Drain())
            _execution.Execute(request, _dispatcher.Count);
        _execution.Poll();
    }

    public override void _ExitTree()
    {
        _server?.RequestStop();
        _serverCancellation?.Cancel();
        _serverCancellation?.Dispose();
        _choices?.Dispose();
        _choices = null;
        _actions?.Dispose();
        _actions = null;
        _configuration?.DestroySecret();
        _configuration = null;
        _idempotency = null;
    }

    private async Task AcceptRequestAsync(
        JsonElement request,
        ProtocolConnection connection,
        CancellationToken cancellationToken)
    {
        string requestId = ProtocolContract.RequireString(request, "request_id");
        RequestIdempotencyDecision decision = _idempotency!.Accept(request);
        if (decision.Status == RequestIdempotencyStatus.Conflict)
        {
            await connection.SendAsync(
                "failed",
                requestId,
                new Dictionary<string, object?>
                {
                    ["error"] = ProtocolServer.Error(
                        ErrorCodes.IdempotencyConflict,
                        "request_id was already used with a different payload"),
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
            await connection.SendAsync(
                "accepted",
                requestId,
                replayed: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        JsonElement accepted = await connection.SendAsync(
            "accepted",
            requestId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (accepted.TryGetProperty("type", out JsonElement acceptedType)
            && acceptedType.GetString() == "failed")
        {
            _idempotency.Complete(requestId, accepted);
            return;
        }
        if (_dispatcher is null || !_dispatcher.TryEnqueue(new PendingRequest(request.Clone(), connection)))
        {
            var fields = new Dictionary<string, object?>
            {
                ["error"] = ProtocolServer.Error(ErrorCodes.ObserverOverflow, "main-thread inbound request queue is full"),
            };
            JsonElement terminal = await connection.SendAsync(
                "failed", requestId,
                fields,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            _idempotency.Complete(requestId, terminal);
        }
    }

    private JsonElement CreateAcknowledgementBody(HandshakeContext context)
    {
        Assembly bridgeAssembly = typeof(BridgeRootNode).Assembly;
        Assembly gameAssembly = typeof(ModInitializerAttribute).Assembly;
        string display = SafeCall(() => DisplayServer.GetName(), "unknown");
        string audio = SafeCall(() => AudioServer.GetDriverName(), "unknown");
        return JsonSerializer.SerializeToElement(new
        {
            session_id = context.SessionId,
            instance_id = context.InstanceId,
            process_epoch = context.ProcessEpoch,
            connection_id = context.ConnectionId,
            negotiated_protocol = context.NegotiatedProtocol,
            game = new
            {
                version = ReleaseInfo("version", "unknown"),
                commit = ReleaseInfo("commit", null),
                assembly_sha256 = AssemblySha256(gameAssembly),
                assembly_mvid = gameAssembly.ManifestModule.ModuleVersionId.ToString("D"),
            },
            adapter = new
            {
                id = "sts2-0.111",
                assembly_sha256 = AssemblySha256(bridgeAssembly),
            },
            runtime = new
            {
                main_thread_id = _mainThreadId,
                // The acknowledgement is serialized by the pipe thread. This
                // flag records that the bridge node and dispatcher themselves
                // were initialized from Godot's main-thread _Ready callback.
                main_thread_probe = _mainThreadProbe,
                display_driver = display,
                audio_driver = audio,
                user_data_path = OS.GetUserDataDir(),
                output_root = _configuration!.OutputRoot,
            },
            capabilities = BridgeCapabilities.Create(),
        });
    }

    private static string AssemblySha256(Assembly assembly)
    {
        string path = assembly.Location;
        return File.Exists(path)
            ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))
            : new string('0', 64);
    }

    private static string SafeCall(Func<string> callback, string fallback)
    {
        try
        {
            return callback();
        }
        catch
        {
            return fallback;
        }
    }

    private static string? ReleaseInfo(string property, string? fallback)
    {
        try
        {
            string executable = OS.GetExecutablePath();
            string? directory = Path.GetDirectoryName(executable);
            if (directory is null)
                return fallback;
            string path = Path.Combine(directory, "release_info.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(property, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? fallback
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
