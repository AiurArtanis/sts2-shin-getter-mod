using System.Diagnostics;
using System.Text.Json;
using Godot;
using Sts2HeadlessTestBridge.Contract;
using Sts2HeadlessTestBridge.State;
using Sts2HeadlessTestBridge.Transport;

namespace Sts2HeadlessTestBridge.Dispatch;

/// <summary>
/// Starts requests on the Godot thread and advances their exact Task/GameAction
/// references from _Process. No game operation is completed from a timer or a
/// worker-thread continuation.
/// </summary>
public sealed class RequestExecution(
    CommandRegistry registry,
    ActionObserver actions,
    Node owner,
    int mainThreadId)
{
    private readonly Dictionary<string, ActiveRequest> _active = new(StringComparer.Ordinal);
    private string? _mutationOwner;
    private string? _mutationFrozenReason;

    public void Execute(PendingRequest pending, int dispatcherDepth)
    {
        string requestId;
        try
        {
            requestId = ProtocolContract.RequireString(pending.Request, "request_id");
        }
        catch (Exception exception)
        {
            _ = pending.Connection.SendAsync(
                "failed",
                "invalid",
                new Dictionary<string, object?>
                {
                    ["error"] = ProtocolServer.Error(ErrorCodes.InvalidArgument, exception.Message),
                });
            return;
        }

        if (System.Environment.CurrentManagedThreadId != mainThreadId)
        {
            _ = pending.Connection.SendAsync(
                "failed",
                requestId,
                new Dictionary<string, object?>
                {
                    ["error"] = ProtocolServer.Error(
                        ErrorCodes.MainThreadViolation,
                        "command did not execute on the recorded Godot main thread"),
                });
            return;
        }

        Task sendChain = pending.Connection.SendAsync(
            "started",
            requestId,
            new Dictionary<string, object?> { ["engine_frame"] = Engine.GetProcessFrames() });
        BridgeCommandDescriptor? descriptor = null;
        bool acquiredMutation = false;
        try
        {
            descriptor = registry.GetDescriptor(pending.Request);
            bool gameplayMutation = descriptor.ConcurrencyClass == "gameplay-mutation";
            if (descriptor.Name == "runtime.shutdown" && _mutationOwner is not null)
            {
                throw new BridgeStateException(
                    ErrorCodes.CancelUnsafe,
                    $"active mutation {_mutationOwner} prevents bridge shutdown");
            }
            if (gameplayMutation)
            {
                if (_mutationOwner is not null)
                {
                    string detail = _mutationFrozenReason is null
                        ? $"mutation lane is owned by {_mutationOwner}"
                        : $"mutation lane is frozen by {_mutationOwner}: {_mutationFrozenReason}";
                    throw new BridgeStateException(ErrorCodes.MutationBusy, detail);
                }
                _mutationOwner = requestId;
                acquiredMutation = true;
            }

            SnapshotCapture? pre = gameplayMutation
                ? registry.Snapshots.Capture($"{descriptor.Name}:pre")
                : null;
            BridgeCommandOperation operation = registry.Begin(
                pending.Request,
                descriptor,
                requestId,
                dispatcherDepth);
            long timeoutMs = ProtocolContract.RequireLong(pending.Request, "timeout_ms");
            if (timeoutMs is < 1 or > 60_000)
                throw new BridgeStateException(ErrorCodes.InvalidArgument, "timeout_ms must be in [1, 60000]");
            var active = new ActiveRequest(
                requestId,
                pending.Connection,
                operation,
                ProtocolContract.RequireString(pending.Request, "wait_for"),
                timeoutMs,
                Stopwatch.GetTimestamp(),
                pre,
                gameplayMutation,
                sendChain);
            _active[requestId] = active;
            PollOne(active);
        }
        catch (BridgeStateException exception)
        {
            if (acquiredMutation && !StringComparer.Ordinal.Equals(_mutationFrozenReason, exception.Message))
            {
                if (exception.Code == ErrorCodes.ActionCorrelationFailed)
                    _mutationFrozenReason = exception.Message;
                else
                    _mutationOwner = null;
            }
            _ = SendAfterAsync(
                sendChain,
                () => pending.Connection.SendAsync(
                    "failed",
                    requestId,
                    new Dictionary<string, object?>
                    {
                        ["error"] = ProtocolServer.Error(exception.Code, exception.Message),
                    }));
        }
        catch (Exception exception)
        {
            if (acquiredMutation)
                _mutationOwner = null;
            _ = SendAfterAsync(
                sendChain,
                () => pending.Connection.SendAsync(
                    "failed",
                    requestId,
                    new Dictionary<string, object?>
                    {
                        ["error"] = ProtocolServer.Error(ErrorCodes.InvalidArgument, exception.Message),
                    }));
        }
    }

    public void Poll()
    {
        if (System.Environment.CurrentManagedThreadId != mainThreadId)
            throw new InvalidOperationException("RequestExecution.Poll must run on the recorded Godot main thread");

        actions.Synchronize();
        foreach (ActionTransition transition in actions.DrainTransitions())
        {
            if (_active.TryGetValue(transition.RequestId, out ActiveRequest? request)
                && !request.TerminalSent)
            {
                request.SendChain = SendAfterAsync(
                    request.SendChain,
                    () => request.Connection.SendAsync(
                        "event",
                        request.RequestId,
                        new Dictionary<string, object?>
                        {
                            ["name"] = transition.Name,
                            ["data"] = transition.Data,
                        }));
            }
        }

        foreach (ActiveRequest request in _active.Values.ToArray())
            PollOne(request);
    }

    private void PollOne(ActiveRequest request)
    {
        if (request.TerminalSent)
        {
            if (CanReleaseMutation(request))
                Remove(request);
            return;
        }

        Task? task = request.Operation.CompletionTask;
        if (task is { IsFaulted: true })
        {
            string message = task.Exception?.GetBaseException().Message ?? "game operation faulted";
            Fail(request, ErrorCodes.ActionCorrelationFailed, message, freezeMutation: request.IsMutation);
            return;
        }
        if (task is { IsCanceled: true })
        {
            Fail(request, ErrorCodes.Cancelled, "game operation was cancelled", freezeMutation: false);
            return;
        }

        if (Stopwatch.GetElapsedTime(request.StartedTimestamp).TotalMilliseconds >= request.TimeoutMs)
        {
            string diagnostic;
            try
            {
                SnapshotCapture capture = registry.Snapshots.Capture($"{request.Operation.Descriptor.Name}:timeout");
                diagnostic = $"; diagnostic_snapshot={capture.SnapshotId}";
            }
            catch (Exception exception)
            {
                diagnostic = $"; diagnostic_snapshot_failed={exception.Message}";
            }
            Fail(
                request,
                ErrorCodes.TimeoutAction,
                $"request exceeded monotonic timeout budget {request.TimeoutMs}ms{diagnostic}",
                freezeMutation: request.IsMutation);
            return;
        }

        try
        {
            if (!ReachedRequestedBoundary(request))
                return;
        }
        catch (Exception exception)
        {
            Fail(request, ErrorCodes.ActionCorrelationFailed, exception.Message, freezeMutation: request.IsMutation);
            return;
        }

        Complete(request);
    }

    private bool ReachedRequestedBoundary(ActiveRequest request)
    {
        if (request.Operation.ReadyPredicate is not null
            && !request.Operation.ReadyPredicate())
        {
            return false;
        }
        return request.WaitFor switch
        {
            "immediate" => request.Operation.CompletionTask is null,
            "enqueued" => request.Operation.Action is not null
                && actions.IsEnqueued(request.Operation.Action),
            "action_finished" => request.Operation.CompletionTask?.IsCompletedSuccessfully ?? true,
            "queue_settled" => (request.Operation.CompletionTask?.IsCompletedSuccessfully ?? true)
                && actions.IsQueueSettled(),
            _ => false,
        };
    }

    private void Complete(ActiveRequest request)
    {
        try
        {
            SnapshotCapture? post = request.IsMutation
                ? registry.Snapshots.Capture($"{request.Operation.Descriptor.Name}:post")
                : null;
            var result = new Dictionary<string, object?>(request.Operation.Result, StringComparer.Ordinal)
            {
                ["completion"] = request.WaitFor,
            };
            if (request.PreSnapshot is not null)
                result["pre_snapshot"] = CommandRegistry.SnapshotReference(request.PreSnapshot);
            if (post is not null)
            {
                result["post_snapshot"] = CommandRegistry.SnapshotReference(post);
                if (request.Operation.Finalize is not null)
                {
                    foreach ((string key, object? value) in request.Operation.Finalize(post))
                        result[key] = value;
                }
            }

            request.SendChain = SendAfterAsync(
                request.SendChain,
                () => request.Connection.SendAsync(
                    "completed",
                    request.RequestId,
                    new Dictionary<string, object?> { ["result"] = result }));
            request.TerminalSent = true;
            if (request.Operation.Shutdown)
                _ = ShutdownAfterAsync(request.SendChain);
            if (CanReleaseMutation(request))
                Remove(request);
        }
        catch (BridgeStateException exception)
        {
            Fail(request, exception.Code, exception.Message, freezeMutation: request.IsMutation);
        }
        catch (Exception exception)
        {
            Fail(request, ErrorCodes.ActionCorrelationFailed, exception.Message, freezeMutation: request.IsMutation);
        }
    }

    private void Fail(
        ActiveRequest request,
        string code,
        string message,
        bool freezeMutation)
    {
        request.SendChain = SendAfterAsync(
            request.SendChain,
            () => request.Connection.SendAsync(
                "failed",
                request.RequestId,
                new Dictionary<string, object?>
                {
                    ["error"] = ProtocolServer.Error(code, message),
                }));
        request.TerminalSent = true;
        if (freezeMutation && request.IsMutation)
            _mutationFrozenReason = message;
        if (!request.IsMutation || CanReleaseMutation(request))
            Remove(request);
    }

    private bool CanReleaseMutation(ActiveRequest request)
    {
        if (!request.IsMutation)
            return true;
        if (_mutationFrozenReason is not null)
            return false;
        if (request.Operation.CompletionTask is { IsCompleted: false })
            return false;
        return actions.IsQueueSettled();
    }

    private void Remove(ActiveRequest request)
    {
        _active.Remove(request.RequestId);
        if (request.IsMutation && StringComparer.Ordinal.Equals(_mutationOwner, request.RequestId))
            _mutationOwner = null;
    }

    private async Task ShutdownAfterAsync(Task sendChain)
    {
        try
        {
            await sendChain.ConfigureAwait(false);
        }
        finally
        {
            owner.CallDeferred(Node.MethodName.QueueFree);
        }
    }

    private static async Task SendAfterAsync(Task previous, Func<Task> send)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // A disconnected client must not stop deterministic game execution;
            // the server replay journal remains the transport authority.
        }
        await send().ConfigureAwait(false);
    }

    private sealed class ActiveRequest(
        string requestId,
        ProtocolConnection connection,
        BridgeCommandOperation operation,
        string waitFor,
        long timeoutMs,
        long startedTimestamp,
        SnapshotCapture? preSnapshot,
        bool isMutation,
        Task sendChain)
    {
        public string RequestId { get; } = requestId;
        public ProtocolConnection Connection { get; } = connection;
        public BridgeCommandOperation Operation { get; } = operation;
        public string WaitFor { get; } = waitFor;
        public long TimeoutMs { get; } = timeoutMs;
        public long StartedTimestamp { get; } = startedTimestamp;
        public SnapshotCapture? PreSnapshot { get; } = preSnapshot;
        public bool IsMutation { get; } = isMutation;
        public Task SendChain { get; set; } = sendChain;
        public bool TerminalSent { get; set; }
    }
}
