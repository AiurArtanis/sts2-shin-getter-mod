using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;
using Sts2HeadlessTestBridge.State;

namespace Sts2HeadlessTestBridge.Dispatch;

public sealed record ActionTransition(
    string RequestId,
    string Name,
    Dictionary<string, object?> Data);

/// <summary>
/// Maintains a reference-identity mirror of the public action lifecycle. The
/// observer never searches by frame proximity or by whichever action happened
/// to execute last: request correlation is registered on the exact object
/// before that object is submitted to the game's synchronizer.
/// </summary>
public sealed class ActionObserver(StableHandleRegistry handles) : IDisposable
{
    private readonly Dictionary<GameAction, ObservedAction> _actions = new(new ActionReferenceComparer());
    private readonly Queue<ActionTransition> _transitions = new();
    private ActionQueueSet? ActionQueueSet { get; set; }
    private ActionExecutor? ActionExecutor { get; set; }
    private long _queueGeneration;
    private bool _detailsComplete;

    public bool HasPendingChoice => _actions.Keys.Any(
        action => action.State == GameActionState.GatheringPlayerChoice);

    public void Synchronize()
    {
        ActionQueueSet? queue = null;
        ActionExecutor? executor = null;
        if (RunManager.Instance.DebugOnlyGetState() is not null)
        {
            queue = RunManager.Instance.ActionQueueSet;
            executor = RunManager.Instance.ActionExecutor;
        }

        if (ReferenceEquals(queue, ActionQueueSet) && ReferenceEquals(executor, ActionExecutor))
            return;

        Detach();
        _actions.Clear();
        ActionQueueSet = queue;
        ActionExecutor = executor;
        _detailsComplete = queue is not null && queue.NextActionId == 0;
        if (ActionQueueSet is not null)
        {
            ActionQueueSet.ActionEnqueued += OnActionEnqueued;
            ActionQueueSet.ActionResumed += OnActionResumed;
            ActionQueueSet.ActionQueueChanged += OnActionQueueChanged;
        }
        if (ActionExecutor is not null)
        {
            ActionExecutor.BeforeActionExecuted += OnActionStarted;
            ActionExecutor.JustBeforeActionFinishedExecuting += OnActionFinishing;
            ActionExecutor.AfterActionExecuted += OnActionFinished;
        }
    }

    public string RegisterCorrelation(GameAction action, string requestId)
    {
        Synchronize();
        if (ActionQueueSet is null || ActionExecutor is null)
            throw new BridgeStateException(Contract.ErrorCodes.InvalidPhase, "action correlation requires an active run");
        ObservedAction observed = GetOrCreate(action);
        if (observed.RequestId is not null
            && !StringComparer.Ordinal.Equals(observed.RequestId, requestId))
        {
            throw new BridgeStateException(
                Contract.ErrorCodes.ActionCorrelationFailed,
                "the exact action reference is already owned by another request");
        }
        observed.RequestId = requestId;
        observed.Handle ??= handles.IssueAction(action, action.Id);
        return observed.Handle;
    }

    public string? RequestFor(GameAction action) =>
        _actions.TryGetValue(action, out ObservedAction? observed) ? observed.RequestId : null;

    public bool IsEnqueued(GameAction action) =>
        _actions.TryGetValue(action, out ObservedAction? observed) && observed.Enqueued;

    public bool IsQueueSettled()
    {
        Synchronize();
        if (ActionQueueSet is null || ActionExecutor is null)
            return false;
        return ActionQueueSet.IsEmpty
            && !ActionExecutor.IsRunning
            && ActionExecutor.CurrentlyRunningAction is null
            && !HasPendingChoice;
    }

    public IEnumerable<ActionTransition> DrainTransitions()
    {
        while (_transitions.Count > 0)
            yield return _transitions.Dequeue();
    }

    public Dictionary<string, object?> Snapshot()
    {
        Synchronize();
        GameAction? running = ActionExecutor?.CurrentlyRunningAction;
        object? runningState = running is null ? null : Describe(GetOrCreate(running));
        object[] pending = _actions.Values
            .Where(item => item.Action.State is not (GameActionState.Finished or GameActionState.Canceled))
            .OrderBy(item => item.Action.Id ?? uint.MaxValue)
            .Select(item => (object)Describe(item))
            .ToArray();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["running"] = runningState,
            ["pending"] = pending,
            ["queue_empty"] = ActionQueueSet?.IsEmpty ?? true,
            ["executor_running"] = ActionExecutor?.IsRunning ?? false,
            ["pending_choice"] = HasPendingChoice,
            ["queue_generation"] = _queueGeneration,
            ["details_complete"] = _detailsComplete,
            ["incomplete_reason"] = _detailsComplete ? null : "observer attached after the current run action queue was created",
        };
    }

    public void Dispose()
    {
        Detach();
        _actions.Clear();
        _transitions.Clear();
    }

    private void OnActionEnqueued(GameAction action)
    {
        _queueGeneration++;
        ObservedAction observed = GetOrCreate(action);
        observed.Enqueued = true;
        observed.Handle ??= TryIssueHandle(action);
        Publish(observed, "action_enqueued", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["action_handle"] = observed.Handle,
            ["action_id"] = action.Id,
            ["owner_id"] = action.OwnerId,
            ["action_type"] = action.GetType().Name,
            ["correlation"] = "exact_reference",
            ["queue_generation"] = _queueGeneration,
        });
    }

    private void OnActionStarted(GameAction action)
    {
        ObservedAction observed = GetOrCreate(action);
        observed.Started = true;
        observed.Handle ??= TryIssueHandle(action);
        Publish(observed, "action_started", EventData(observed));
    }

    private void OnActionFinishing(GameAction action)
    {
        ObservedAction observed = GetOrCreate(action);
        observed.Finishing = true;
    }

    private void OnActionFinished(GameAction action)
    {
        ObservedAction observed = GetOrCreate(action);
        if (observed.Finished)
            return;
        observed.Finished = true;
        observed.Handle ??= TryIssueHandle(action);
        Publish(observed, "action_finished", EventData(observed));
    }

    private void OnActionResumed(uint oldActionId)
    {
        _queueGeneration++;
        ObservedAction? observed = _actions.Values.FirstOrDefault(item => item.Action.Id == oldActionId);
        if (observed is not null)
        {
            Publish(observed, "action_resumed", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["action_handle"] = observed.Handle,
                ["previous_action_id"] = oldActionId,
                ["owner_id"] = observed.Action.OwnerId,
                ["queue_generation"] = _queueGeneration,
            });
        }
    }

    private void OnActionQueueChanged() => _queueGeneration++;

    private ObservedAction GetOrCreate(GameAction action)
    {
        if (_actions.TryGetValue(action, out ObservedAction? observed))
            return observed;
        observed = new ObservedAction(action);
        _actions[action] = observed;
        return observed;
    }

    private string? TryIssueHandle(GameAction action)
    {
        try
        {
            return handles.IssueAction(action, action.Id);
        }
        catch (BridgeStateException)
        {
            return null;
        }
    }

    private void Publish(ObservedAction observed, string name, Dictionary<string, object?> data)
    {
        if (observed.RequestId is not null)
            _transitions.Enqueue(new ActionTransition(observed.RequestId, name, data));
    }

    private Dictionary<string, object?> EventData(ObservedAction observed) => new(StringComparer.Ordinal)
    {
        ["action_handle"] = observed.Handle,
        ["action_id"] = observed.Action.Id,
        ["owner_id"] = observed.Action.OwnerId,
        ["action_type"] = observed.Action.GetType().Name,
        ["state"] = observed.Action.State.ToString(),
        ["queue_generation"] = _queueGeneration,
    };

    private Dictionary<string, object?> Describe(ObservedAction observed) => new(StringComparer.Ordinal)
    {
        ["handle"] = observed.Handle ?? TryIssueHandle(observed.Action),
        ["action_id"] = observed.Action.Id,
        ["owner_id"] = observed.Action.OwnerId,
        ["action_type"] = observed.Action.GetType().Name,
        ["state"] = observed.Action.State.ToString(),
        ["request_id"] = observed.RequestId,
        ["enqueued"] = observed.Enqueued,
        ["started"] = observed.Started,
        ["finishing"] = observed.Finishing,
        ["finished"] = observed.Finished,
    };

    private void Detach()
    {
        if (ActionQueueSet is not null)
        {
            ActionQueueSet.ActionEnqueued -= OnActionEnqueued;
            ActionQueueSet.ActionResumed -= OnActionResumed;
            ActionQueueSet.ActionQueueChanged -= OnActionQueueChanged;
        }
        if (ActionExecutor is not null)
        {
            ActionExecutor.BeforeActionExecuted -= OnActionStarted;
            ActionExecutor.JustBeforeActionFinishedExecuting -= OnActionFinishing;
            ActionExecutor.AfterActionExecuted -= OnActionFinished;
        }
        ActionQueueSet = null;
        ActionExecutor = null;
    }

    private sealed class ObservedAction(GameAction action)
    {
        public GameAction Action { get; } = action;
        public string? RequestId { get; set; }
        public string? Handle { get; set; }
        public bool Enqueued { get; set; }
        public bool Started { get; set; }
        public bool Finishing { get; set; }
        public bool Finished { get; set; }
    }

    private sealed class ActionReferenceComparer : IEqualityComparer<GameAction>
    {
        public bool Equals(GameAction? left, GameAction? right) => ReferenceEquals(left, right);

        public int GetHashCode(GameAction value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}
