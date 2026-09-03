using System.Text.Json;
using Godot;
using Sts2HeadlessTestBridge.Contract;
using Sts2HeadlessTestBridge.Transport;

namespace Sts2HeadlessTestBridge.Dispatch;

public sealed class RequestExecution(CommandRegistry registry, Node owner, int mainThreadId)
{
    public void Execute(PendingRequest pending, int dispatcherDepth)
    {
        string requestId = ProtocolContract.RequireString(pending.Request, "request_id");
        if (System.Environment.CurrentManagedThreadId != mainThreadId)
        {
            _ = pending.Connection.SendAsync(
                "failed", requestId,
                new Dictionary<string, object?>
                {
                    ["error"] = ProtocolServer.Error(ErrorCodes.MainThreadViolation, "command did not execute on the recorded Godot main thread"),
                });
            return;
        }
        Task<JsonElement> started = pending.Connection.SendAsync(
            "started", requestId,
            new Dictionary<string, object?> { ["engine_frame"] = Engine.GetProcessFrames() });
        BridgeCommandResult result;
        try
        {
            result = registry.Execute(pending.Request, dispatcherDepth);
        }
        catch (Exception exception)
        {
            result = new BridgeCommandResult(
                false,
                ErrorCode: ErrorCodes.InvalidArgument,
                ErrorMessage: exception.Message);
        }
        _ = PublishTerminalAsync(started, pending.Connection, requestId, result);
    }

    private async Task PublishTerminalAsync(
        Task<JsonElement> started,
        ProtocolConnection connection,
        string requestId,
        BridgeCommandResult result)
    {
        await started.ConfigureAwait(false);
        if (result.Success)
        {
            await connection.SendAsync(
                "completed", requestId,
                new Dictionary<string, object?> { ["result"] = result.Result ?? new Dictionary<string, object?>() });
        }
        else
        {
            await connection.SendAsync(
                "failed", requestId,
                new Dictionary<string, object?>
                {
                    ["error"] = ProtocolServer.Error(
                        result.ErrorCode ?? ErrorCodes.InvalidArgument,
                        result.ErrorMessage ?? "command failed"),
                });
        }
        if (result.Shutdown)
            owner.CallDeferred(Node.MethodName.QueueFree);
    }
}
