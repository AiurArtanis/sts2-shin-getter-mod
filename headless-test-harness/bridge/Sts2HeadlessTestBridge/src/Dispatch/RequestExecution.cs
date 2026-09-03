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
    ChoiceBroker choices,
    RequestIdempotencyGate idempotency,
    Func<ProtocolCaseFailure?> transportFailure,
    SceneTree sceneTree,
    int mainThreadId)
{
    private readonly Dictionary<string, ActiveRequest> _active = new(StringComparer.Ordinal);
    private string? _mutationOwner;
    private string? _mutationFrozenReason;
    private string? _mutationFrozenCode;

    public void ApplyTransportFailure(ProtocolCaseFailure failure)
    {
        if (_mutationFrozenCode is not null)
            return;
        _mutationFrozenCode = failure.Code;
        _mutationFrozenReason = failure.Message;
        foreach (ActiveRequest request in _active.Values.ToArray())
        {
            if (request.TerminalSent)
                continue;
            if (request.IsMutation)
                choices.InvalidateParent(request.RequestId, failure.Message);
            if (IsAlreadyPublishedOverflow(request.RequestId, failure))
            {
                idempotency.Complete(
                    request.RequestId,
                    "failed",
                    TransportFailureFields(failure));
                request.TerminalSent = true;
            }
            else
            {
                Fail(request, failure.Code, failure.Message, freezeMutation: request.IsMutation);
            }
        }
    }

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
            SendImmediateFailure(
                pending.Connection,
                requestId,
                ErrorCodes.MainThreadViolation,
                "command did not execute on the recorded Godot main thread");
            return;
        }

        Task<JsonElement> startedSend = pending.Connection.SendAsync(
            "started",
            requestId,
            new Dictionary<string, object?> { ["engine_frame"] = Engine.GetProcessFrames() });
        Task sendChain = startedSend;
        JsonElement startedEnvelope = startedSend.GetAwaiter().GetResult();
        if (IsObserverOverflowTerminal(startedEnvelope))
        {
            idempotency.Complete(requestId, startedEnvelope);
            ProtocolCaseFailure failure = transportFailure()
                ?? new ProtocolCaseFailure(
                    ErrorCodes.ObserverOverflow,
                    "live critical outbound queue overflowed",
                    new Dictionary<string, object?>());
            _mutationFrozenCode ??= failure.Code;
            _mutationFrozenReason ??= failure.Message;
            return;
        }
        BridgeCommandDescriptor? descriptor = null;
        bool acquiredMutation = false;
        try
        {
            descriptor = registry.GetDescriptor(pending.Request);
            bool gameplayMutation = descriptor.ConcurrencyClass == "gameplay-mutation";
            if (gameplayMutation && _mutationFrozenCode is not null)
            {
                throw new BridgeStateException(
                    _mutationFrozenCode,
                    $"mutation lane is frozen: {_mutationFrozenReason}");
            }
            if (descriptor.ConcurrencyClass == "choice-continuation"
                && (_mutationOwner is null || _mutationFrozenCode is not null))
            {
                throw new BridgeStateException(
                    ErrorCodes.StaleHandle,
                    "choice continuation has no live gameplay-mutation parent");
            }
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
                {
                    _mutationFrozenCode = exception.Code;
                    _mutationFrozenReason = exception.Message;
                }
                else
                    _mutationOwner = null;
            }
            SendImmediateFailure(pending.Connection, requestId, exception.Code, exception.Message, sendChain);
        }
        catch (Exception exception)
        {
            if (acquiredMutation)
                _mutationOwner = null;
            SendImmediateFailure(
                pending.Connection,
                requestId,
                ErrorCodes.InvalidArgument,
                exception.Message,
                sendChain);
        }
    }

    public void Poll()
    {
        if (System.Environment.CurrentManagedThreadId != mainThreadId)
            throw new InvalidOperationException("RequestExecution.Poll must run on the recorded Godot main thread");

        actions.Synchronize();
        choices.Synchronize();
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
        foreach (ChoiceTransition transition in choices.DrainTransitions())
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

        if (transportFailure() is ProtocolCaseFailure failure)
        {
            ApplyTransportFailure(failure);
            return;
        }

        foreach (ActiveRequest request in _active.Values.ToArray())
            PollOne(request);
    }

    private void PollOne(ActiveRequest request)
    {
        if (request.TerminalSent)
        {
            if (request.SendChain.IsCompletedSuccessfully && CanReleaseMutation(request))
                Remove(request);
            return;
        }

        Task? task = request.Operation.CompletionTask;
        if (request.Operation.Action is { CompletionTask.IsCompleted: true, Exception: not null } action)
        {
            Fail(
                request,
                ErrorCodes.ActionCorrelationFailed,
                action.Exception.GetBaseException().Message,
                freezeMutation: request.IsMutation);
            return;
        }
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
                && actions.IsQueueSettled()
                && !choices.HasActiveChoice,
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

            var fields = new Dictionary<string, object?> { ["result"] = result };
            request.SendChain = SendTerminalAfterAsync(
                request.SendChain,
                request.Connection,
                request.RequestId,
                "completed",
                fields,
                waitForFlush: request.Operation.Shutdown);
            request.TerminalSent = true;
            if (request.Operation.Shutdown)
                _ = ShutdownAfterAsync(request.SendChain);
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
        var fields = new Dictionary<string, object?>
        {
            ["error"] = ProtocolServer.Error(code, message),
        };
        request.SendChain = SendTerminalAfterAsync(
            request.SendChain,
            request.Connection,
            request.RequestId,
            "failed",
            fields);
        request.TerminalSent = true;
        if (request.IsMutation)
            choices.InvalidateParent(request.RequestId, message);
        if (freezeMutation && request.IsMutation)
        {
            _mutationFrozenCode ??= code;
            _mutationFrozenReason = message;
        }
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
            sceneTree.CallDeferred(SceneTree.MethodName.Quit, 0);
        }
        catch (Exception exception)
        {
            GD.PushError($"Sts2HeadlessTestBridge shutdown terminal was not flushed: {exception.GetBaseException().Message}");
        }
    }

    private void SendImmediateFailure(
        ProtocolConnection connection,
        string requestId,
        string code,
        string message,
        Task? previous = null)
    {
        var fields = new Dictionary<string, object?>
        {
            ["error"] = ProtocolServer.Error(code, message),
        };
        _ = SendTerminalAfterAsync(
            previous ?? Task.CompletedTask,
            connection,
            requestId,
            "failed",
            fields);
    }

    private async Task SendTerminalAfterAsync(
        Task previous,
        ProtocolConnection connection,
        string requestId,
        string type,
        IReadOnlyDictionary<string, object?> fields,
        bool waitForFlush = false)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // A prior delivery failure does not remove the terminal from the
            // server replay journal or change deterministic game execution.
        }
        JsonElement terminal = await connection.SendAsync(
            type,
            requestId,
            fields,
            waitForFlush: waitForFlush).ConfigureAwait(false);
        idempotency.Complete(requestId, terminal);
    }

    private static bool IsAlreadyPublishedOverflow(string requestId, ProtocolCaseFailure failure)
    {
        return failure.Code == ErrorCodes.ObserverOverflow
            && failure.Details.TryGetValue("first_lost_request_id", out object? lost)
            && StringComparer.Ordinal.Equals(lost as string, requestId);
    }

    private static Dictionary<string, object?> TransportFailureFields(ProtocolCaseFailure failure) =>
        new(StringComparer.Ordinal)
        {
            ["out_of_band"] = true,
            ["case_invalid"] = true,
            ["error"] = ProtocolServer.Error(failure.Code, failure.Message, details: failure.Details),
        };

    private static bool IsObserverOverflowTerminal(JsonElement envelope)
    {
        return envelope.TryGetProperty("type", out JsonElement type)
            && type.GetString() == "failed"
            && envelope.TryGetProperty("error", out JsonElement error)
            && error.TryGetProperty("code", out JsonElement code)
            && code.GetString() == ErrorCodes.ObserverOverflow;
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
