using System.Text.Json;
using Godot;
using Sts2HeadlessTestBridge.Contract;

namespace Sts2HeadlessTestBridge.Dispatch;

public sealed record BridgeCommandResult(
    bool Success,
    Dictionary<string, object?>? Result = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    bool Shutdown = false);

public sealed record BridgeCommandDescriptor(
    string Name,
    string Kind,
    string ConcurrencyClass,
    string CompletionStrategy,
    string DefaultWaitFor,
    string[] RequiredCapabilities);

public sealed class CommandRegistry
{
    private readonly Dictionary<string, BridgeCommandDescriptor> _descriptors = new(StringComparer.Ordinal)
    {
        ["runtime.ping"] = new("runtime.ping", "query", "snapshot-safe-query", "immediate_query", "immediate", []),
        ["runtime.capabilities"] = new("runtime.capabilities", "query", "snapshot-safe-query", "immediate_query", "immediate", []),
        ["runtime.commands"] = new("runtime.commands", "query", "snapshot-safe-query", "immediate_query", "immediate", []),
        ["runtime.shutdown"] = new("runtime.shutdown", "lifecycle", "control", "immediate_query", "immediate", []),
    };

    public IReadOnlyDictionary<string, BridgeCommandDescriptor> Descriptors => _descriptors;

    public BridgeCommandResult Execute(JsonElement request, int dispatcherDepth)
    {
        string command = ProtocolContract.RequireString(request, "command");
        if (!_descriptors.TryGetValue(command, out BridgeCommandDescriptor? descriptor))
            return new(false, ErrorCode: ErrorCodes.InvalidArgument, ErrorMessage: $"unknown or unavailable command: {command}");
        string waitFor = ProtocolContract.RequireString(request, "wait_for");
        if (descriptor.CompletionStrategy == "immediate_query" && waitFor != "immediate")
            return new(false, ErrorCode: ErrorCodes.InvalidArgument, ErrorMessage: $"{command} only supports immediate completion");
        return command switch
        {
            "runtime.ping" => new(true, new Dictionary<string, object?>
            {
                ["frame"] = Engine.GetProcessFrames(),
                ["wall_clock"] = DateTimeOffset.UtcNow.ToString("O"),
                ["queue_depth"] = dispatcherDepth,
                ["main_thread_id"] = System.Environment.CurrentManagedThreadId,
            }),
            "runtime.capabilities" => new(true, new Dictionary<string, object?>
            {
                ["capabilities"] = BridgeCapabilities.Create(),
            }),
            "runtime.commands" => new(true, new Dictionary<string, object?>
            {
                ["commands"] = _descriptors.Values.OrderBy(item => item.Name).ToArray(),
            }),
            "runtime.shutdown" => new(true, new Dictionary<string, object?> { ["flushed"] = true }, Shutdown: true),
            _ => new(false, ErrorCode: ErrorCodes.InvalidArgument, ErrorMessage: $"unhandled command: {command}"),
        };
    }
}

public static class BridgeCapabilities
{
    public static Dictionary<string, object?> Create() => new(StringComparer.Ordinal)
    {
        ["named_pipe_duplex"] = State("available"),
        ["bidirectional_hmac"] = State("available"),
        ["main_thread_dispatch"] = State("available"),
        ["state_dump"] = State("unavailable", "D4 adapter not registered"),
        ["typed_card_play"] = State("unavailable", "D5 adapter not registered"),
        ["card_select_local_selector"] = State("unavailable", "D6 adapter not registered"),
        ["pixel_output"] = State("unknown", "H0 capability probe only"),
        ["virtual_clock"] = State("unavailable", "not supported by v0.2"),
    };

    private static Dictionary<string, object?> State(string state, string? reason = null) =>
        new(StringComparer.Ordinal) { ["state"] = state, ["reason"] = reason };
}
