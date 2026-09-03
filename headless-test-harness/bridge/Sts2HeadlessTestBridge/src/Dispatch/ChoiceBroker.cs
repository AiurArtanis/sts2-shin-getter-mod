using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using Sts2HeadlessTestBridge.Contract;
using Sts2HeadlessTestBridge.State;

namespace Sts2HeadlessTestBridge.Dispatch;

public sealed record ChoiceTransition(
    string RequestId,
    string Name,
    Dictionary<string, object?> Data);

/// <summary>
/// Implements the 0.111 singleplayer LocalSelector bridge. A selection is
/// resumed only with the exact parent, owner, per-owner generation, choice
/// handle, and server-issued candidate handles reported by this object.
/// </summary>
public sealed class ChoiceBroker(string processEpoch, ActionObserver actions) : ICardSelector, IDisposable
{
    private readonly Dictionary<ulong, long> _generations = new();
    private readonly Queue<ChoiceTransition> _transitions = new();
    private RunState? _run;
    private IDisposable? _selectorScope;
    private ActiveChoice? _active;

    public bool HasActiveChoice => _active is not null;

    public void Synchronize()
    {
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (!ReferenceEquals(run, _run))
        {
            InvalidateActive("run epoch changed while a card choice was pending");
            DisposeSelector();
            _run = run;
        }

        if (_run is null)
            return;
        if (ReferenceEquals(CardSelectCmd.LocalSelector, this))
            return;
        DisposeSelector();
        if (CardSelectCmd.LocalSelector is not null)
        {
            throw new BridgeStateException(
                ErrorCodes.CapabilityUnavailable,
                "another LocalSelector is already installed");
        }
        _selectorScope = CardSelectCmd.UseSelector(this, localOnly: true);
    }

    public Task<IEnumerable<CardModel>> GetSelectedCards(
        IEnumerable<CardModel> options,
        int minSelect,
        int maxSelect)
    {
        Synchronize();
        if (_active is not null)
            throw new InvalidOperationException("a second local card choice began before the first one completed");
        GameAction action = RunManager.Instance.ActionExecutor.CurrentlyRunningAction
            ?? throw new InvalidOperationException("local card choice has no exact currently running action");
        if (action.State != MegaCrit.Sts2.Core.Entities.Actions.GameActionState.GatheringPlayerChoice)
        {
            throw new InvalidOperationException(
                $"local card choice action is in {action.State}, expected GatheringPlayerChoice");
        }
        string parentRequestId = actions.RequestFor(action)
            ?? throw new InvalidOperationException("local card choice action is not correlated to a bridge request");
        List<CardModel> cards = options.ToList();
        if (minSelect < 0 || maxSelect < minSelect || minSelect > cards.Count)
            throw new InvalidOperationException($"invalid card selection bounds: {minSelect}..{maxSelect} for {cards.Count} cards");

        ulong ownerId = action.OwnerId;
        long generation = _generations.GetValueOrDefault(ownerId) + 1;
        _generations[ownerId] = generation;
        string choiceHandle = $"choice:{processEpoch}:player-{ownerId}:g{generation}";
        var completion = new TaskCompletionSource<IEnumerable<CardModel>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ChoiceCandidate[] candidates = cards
            .Select((card, index) => new ChoiceCandidate(
                $"choice-item:{processEpoch}:player-{ownerId}:g{generation}:{index}",
                card))
            .ToArray();
        _active = new ActiveChoice(
            parentRequestId,
            ownerId,
            generation,
            choiceHandle,
            minSelect,
            Math.Min(maxSelect, cards.Count),
            action,
            candidates,
            completion);
        _transitions.Enqueue(new ChoiceTransition(
            parentRequestId,
            "choice_required",
            Describe(_active)));
        return completion.Task;
    }

    public CardRewardSelection GetSelectedCardReward(
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> alternatives)
    {
        throw new NotSupportedException(
            "v0.2 LocalSelector supports asynchronous card choices only, not synchronous reward selection");
    }

    public Dictionary<string, object?> Select(JsonElement args)
    {
        ActiveChoice choice = _active
            ?? throw new BridgeStateException(ErrorCodes.StaleHandle, "there is no active local card choice");
        string blockedRequestId = RequiredString(args, "blocked_request_id");
        if (!StringComparer.Ordinal.Equals(blockedRequestId, choice.ParentRequestId))
            throw Stale("blocked_request_id");
        if (!args.TryGetProperty("owner_id", out JsonElement owner)
            || !owner.TryGetUInt64(out ulong ownerId)
            || ownerId != choice.OwnerId)
        {
            throw Stale("owner_id");
        }
        if (!StringComparer.Ordinal.Equals(RequiredString(args, "choice_handle"), choice.Handle))
            throw Stale("choice_handle");
        if (!args.TryGetProperty("choice_generation", out JsonElement generation)
            || !generation.TryGetInt64(out long generationValue)
            || generationValue != choice.Generation)
        {
            throw Stale("choice_generation");
        }
        if (!args.TryGetProperty("candidates", out JsonElement selected)
            || selected.ValueKind != JsonValueKind.Array)
        {
            throw new BridgeStateException(ErrorCodes.InvalidArgument, "args.candidates must be an array of server-issued handles");
        }

        string[] handles = selected.EnumerateArray().Select(value =>
        {
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new BridgeStateException(ErrorCodes.InvalidArgument, "every selected candidate must be a string handle");
            return value.GetString()!;
        }).ToArray();
        if (handles.Length < choice.Min || handles.Length > choice.Max)
        {
            throw new BridgeStateException(
                ErrorCodes.InvalidArgument,
                $"selection count {handles.Length} is outside [{choice.Min}, {choice.Max}]");
        }
        if (handles.Distinct(StringComparer.Ordinal).Count() != handles.Length)
            throw new BridgeStateException(ErrorCodes.InvalidArgument, "duplicate candidate handles are not allowed");

        var selectedCards = new List<CardModel>(handles.Length);
        foreach (string handle in handles)
        {
            ChoiceCandidate? candidate = choice.Candidates.FirstOrDefault(
                item => StringComparer.Ordinal.Equals(item.Handle, handle));
            if (candidate is null)
                throw Stale("candidate");
            selectedCards.Add(candidate.Card);
        }

        _active = null;
        if (!choice.Completion.TrySetResult(selectedCards))
            throw new BridgeStateException(ErrorCodes.StaleHandle, "choice selector was already completed");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["selector_accepted"] = true,
            ["blocked_request_id"] = choice.ParentRequestId,
            ["owner_id"] = choice.OwnerId,
            ["choice_handle"] = choice.Handle,
            ["choice_generation"] = choice.Generation,
            ["selected"] = handles,
        };
    }

    public Dictionary<string, object?> List()
    {
        ActiveChoice choice = _active
            ?? throw new BridgeStateException(ErrorCodes.InvalidPhase, "there is no active local card choice");
        return Describe(choice);
    }

    public IReadOnlyList<Dictionary<string, object?>> Snapshot()
    {
        return _active is null
            ? Array.Empty<Dictionary<string, object?>>()
            : new[] { Describe(_active) };
    }

    public IEnumerable<ChoiceTransition> DrainTransitions()
    {
        while (_transitions.Count > 0)
            yield return _transitions.Dequeue();
    }

    public void InvalidateParent(string requestId, string message)
    {
        if (_active is not null
            && StringComparer.Ordinal.Equals(_active.ParentRequestId, requestId))
        {
            InvalidateActive(message);
        }
    }

    public void Dispose()
    {
        InvalidateActive("bridge is shutting down");
        DisposeSelector();
        _run = null;
        _transitions.Clear();
    }

    private static Dictionary<string, object?> Describe(ActiveChoice choice) => new(StringComparer.Ordinal)
    {
        ["choice_handle"] = choice.Handle,
        ["owner_id"] = choice.OwnerId,
        ["choice_generation"] = choice.Generation,
        ["kind"] = "card",
        ["min"] = choice.Min,
        ["max"] = choice.Max,
        ["can_cancel"] = choice.Min == 0,
        ["candidates"] = choice.Candidates.Select(candidate => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["handle"] = candidate.Handle,
            ["model_id"] = candidate.Card.Id.ToString(),
            ["upgrade_level"] = candidate.Card.CurrentUpgradeLevel,
        }).ToArray(),
        ["blocked_request_id"] = choice.ParentRequestId,
        ["blocked_action_id"] = choice.Action.Id,
    };

    private void InvalidateActive(string message)
    {
        ActiveChoice? choice = _active;
        _active = null;
        choice?.Completion.TrySetException(new InvalidOperationException(message));
    }

    private void DisposeSelector()
    {
        _selectorScope?.Dispose();
        _selectorScope = null;
    }

    private static string RequiredString(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new BridgeStateException(ErrorCodes.InvalidArgument, $"required string missing: args.{name}");
        }
        return value.GetString()!;
    }

    private static BridgeStateException Stale(string field) => new(
        ErrorCodes.StaleHandle,
        $"choice continuation has stale or mismatched {field}");

    private sealed record ChoiceCandidate(string Handle, CardModel Card);

    private sealed record ActiveChoice(
        string ParentRequestId,
        ulong OwnerId,
        long Generation,
        string Handle,
        int Min,
        int Max,
        GameAction Action,
        ChoiceCandidate[] Candidates,
        TaskCompletionSource<IEnumerable<CardModel>> Completion);
}
