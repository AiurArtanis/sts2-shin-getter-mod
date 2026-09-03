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
    private ProtocolServer? _server;
    private CancellationTokenSource? _serverCancellation;
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
            actionSnapshot: () => _actions?.Snapshot() ?? new Dictionary<string, object?>());
        _actions = new ActionObserver(snapshots.Handles);
        _actions.Synchronize();
        var registry = new CommandRegistry(snapshots, _actions);
        _execution = new RequestExecution(registry, _actions, this, _mainThreadId);
        _server = new ProtocolServer(
            _configuration.PipeName,
            _configuration.SessionId,
            _configuration.InstanceId,
            _configuration.Token,
            CreateAcknowledgementBody,
            AcceptRequestAsync,
            processEpoch);
        _serverCancellation = new CancellationTokenSource();
        _ = Task.Run(() => _server.RunAsync(_serverCancellation.Token));
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (_dispatcher is null || _execution is null)
            return;
        foreach (PendingRequest request in _dispatcher.Drain())
            _execution.Execute(request, _dispatcher.Count);
        _execution.Poll();
    }

    public override void _ExitTree()
    {
        _server?.RequestStop();
        _serverCancellation?.Cancel();
        _serverCancellation?.Dispose();
        _actions?.Dispose();
        _actions = null;
        _configuration?.DestroySecret();
        _configuration = null;
    }

    private async Task AcceptRequestAsync(
        JsonElement request,
        ProtocolConnection connection,
        CancellationToken cancellationToken)
    {
        string requestId = ProtocolContract.RequireString(request, "request_id");
        await connection.SendAsync("accepted", requestId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (_dispatcher is null || !_dispatcher.TryEnqueue(new PendingRequest(request.Clone(), connection)))
        {
            await connection.SendAsync(
                "failed", requestId,
                new Dictionary<string, object?>
                {
                    ["error"] = ProtocolServer.Error(ErrorCodes.ObserverOverflow, "main-thread inbound request queue is full"),
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
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
