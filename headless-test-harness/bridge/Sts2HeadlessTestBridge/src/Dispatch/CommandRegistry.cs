using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using Sts2HeadlessTestBridge.Contract;
using Sts2HeadlessTestBridge.State;

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

public sealed record BridgeCommandOperation(
    BridgeCommandDescriptor Descriptor,
    Dictionary<string, object?> Result,
    Task? CompletionTask = null,
    GameAction? Action = null,
    Func<bool>? ReadyPredicate = null,
    Func<SnapshotCapture, Dictionary<string, object?>>? Finalize = null,
    bool Shutdown = false);

public sealed class CommandRegistry(SnapshotBuilder snapshots, ActionObserver actions)
{
    private MegaCrit.Sts2.Core.DevConsole.DevConsole? _devConsole;

    private readonly Dictionary<string, BridgeCommandDescriptor> _descriptors = new(StringComparer.Ordinal)
    {
        ["runtime.ping"] = new("runtime.ping", "query", "snapshot-safe-query", "immediate_query", "immediate", []),
        ["runtime.capabilities"] = new("runtime.capabilities", "query", "snapshot-safe-query", "immediate_query", "immediate", []),
        ["runtime.commands"] = new("runtime.commands", "query", "snapshot-safe-query", "immediate_query", "immediate", []),
        ["runtime.shutdown"] = new("runtime.shutdown", "lifecycle", "control", "immediate_query", "immediate", []),
        ["state.dump"] = new("state.dump", "query", "snapshot-safe-query", "immediate_query", "immediate", ["state_dump"]),
        ["run.new"] = new("run.new", "mutation", "gameplay-mutation", "location_predicate", "queue_settled", []),
        ["run.status"] = new("run.status", "query", "snapshot-safe-query", "immediate_query", "immediate", []),
        ["console.exec"] = new("console.exec", "mutation", "gameplay-mutation", "location_predicate", "queue_settled", []),
        ["combat.status"] = new("combat.status", "query", "snapshot-safe-query", "immediate_query", "immediate", []),
        ["combat.add_card"] = new("combat.add_card", "mutation", "gameplay-mutation", "awaitable_cmd_result", "queue_settled", []),
        ["combat.play_card"] = new("combat.play_card", "mutation", "gameplay-mutation", "typed_action_reference", "queue_settled", ["typed_card_play"]),
    };

    public IReadOnlyDictionary<string, BridgeCommandDescriptor> Descriptors => _descriptors;
    public SnapshotBuilder Snapshots => snapshots;

    public BridgeCommandDescriptor GetDescriptor(JsonElement request)
    {
        string command = ProtocolContract.RequireString(request, "command");
        if (!_descriptors.TryGetValue(command, out BridgeCommandDescriptor? descriptor))
            throw new BridgeStateException(ErrorCodes.InvalidArgument, $"unknown or unavailable command: {command}");
        ValidateWaitFor(descriptor, ProtocolContract.RequireString(request, "wait_for"));
        return descriptor;
    }

    public BridgeCommandOperation Begin(
        JsonElement request,
        BridgeCommandDescriptor descriptor,
        string requestId,
        int dispatcherDepth)
    {
        return descriptor.Name switch
        {
            "runtime.ping" => Immediate(descriptor, new Dictionary<string, object?>
            {
                ["frame"] = Engine.GetProcessFrames(),
                ["wall_clock"] = DateTimeOffset.UtcNow.ToString("O"),
                ["queue_depth"] = dispatcherDepth,
                ["main_thread_id"] = System.Environment.CurrentManagedThreadId,
            }),
            "runtime.capabilities" => Immediate(descriptor, new Dictionary<string, object?>
            {
                ["capabilities"] = BridgeCapabilities.Create(),
            }),
            "runtime.commands" => Immediate(descriptor, new Dictionary<string, object?>
            {
                ["commands"] = _descriptors.Values.OrderBy(item => item.Name).ToArray(),
            }),
            "state.dump" => StateDump(request, descriptor),
            "run.new" => StartRun(request, descriptor),
            "run.status" => RunStatus(descriptor),
            "console.exec" => ConsoleExec(request, descriptor),
            "combat.status" => CombatStatus(descriptor),
            "combat.add_card" => AddCard(request, descriptor),
            "combat.play_card" => PlayCard(request, descriptor, requestId),
            "runtime.shutdown" => new BridgeCommandOperation(
                descriptor,
                new Dictionary<string, object?> { ["flushed"] = true },
                Shutdown: true),
            _ => throw new BridgeStateException(ErrorCodes.InvalidArgument, $"unhandled command: {descriptor.Name}"),
        };
    }

    public static Dictionary<string, object?> SnapshotReference(SnapshotCapture capture) => new(StringComparer.Ordinal)
    {
        ["snapshot_id"] = capture.SnapshotId,
        ["canonical_sha256"] = capture.CanonicalSha256,
        ["path"] = capture.Path,
    };

    private BridgeCommandOperation StateDump(JsonElement request, BridgeCommandDescriptor descriptor)
    {
        JsonElement args = request.GetProperty("args");
        string purpose = OptionalString(args, "purpose") ?? "state.dump";
        SnapshotCapture capture = snapshots.Capture(purpose);
        return Immediate(descriptor, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["snapshot_id"] = capture.SnapshotId,
            ["canonical_sha256"] = capture.CanonicalSha256,
            ["path"] = capture.Path,
            ["completeness"] = capture.Snapshot["completeness"],
            ["identity"] = capture.Snapshot["identity"],
            ["location"] = capture.Snapshot["location"],
        });
    }

    private BridgeCommandOperation StartRun(JsonElement request, BridgeCommandDescriptor descriptor)
    {
        if (RunManager.Instance.IsInProgress || RunManager.Instance.DebugOnlyGetState() is not null)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, "run.new requires no active run");
        JsonElement args = request.GetProperty("args");
        string characterId = RequiredString(args, "character").ToUpperInvariant();
        string seed = OptionalString(args, "seed") ?? "424242";
        int ascension = OptionalInt(args, "ascension", 0, 20);
        CharacterModel character = UniqueModel(
            ModelDb.All.OfType<CharacterModel>(),
            characterId,
            "character");
        IReadOnlyList<ActModel> acts = ModelDb.Acts.ToList();
        if (acts.Count == 0)
            throw new BridgeStateException(ErrorCodes.NotFound, "no canonical acts are registered");

        NGame game = NGame.Instance
            ?? throw new BridgeStateException(ErrorCodes.InvalidPhase, "NGame is not ready");
        Task<RunState> task = game.StartNewSingleplayerRun(
            character,
            shouldSave: false,
            acts,
            Array.Empty<ModifierModel>(),
            seed,
            GameMode.Standard,
            ascension);
        // SetUpNewSingleplayer creates the queue synchronously before the first
        // asynchronous load boundary; attach now so the mirror starts at ID 0.
        actions.Synchronize();
        return new BridgeCommandOperation(
            descriptor,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["character_id"] = character.Id.ToString(),
                ["seed"] = seed,
                ["ascension"] = ascension,
                ["should_save"] = false,
            },
            CompletionTask: task,
            ReadyPredicate: () => RunManager.Instance.IsInProgress
                && RunManager.Instance.DebugOnlyGetState()?.CurrentRoom is not null,
            Finalize: post => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["run_epoch"] = IdentityValue(post, "run_epoch"),
                ["location"] = post.Snapshot["location"],
            });
    }

    private BridgeCommandOperation RunStatus(BridgeCommandDescriptor descriptor)
    {
        RunState? run = RunManager.Instance.DebugOnlyGetState();
        if (run is null)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, "run.status requires an active run");
        SnapshotCapture capture = snapshots.Capture("run.status", persist: false);
        return Immediate(descriptor, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["run_epoch"] = IdentityValue(capture, "run_epoch"),
            ["seed"] = run.Rng.Seed,
            ["should_save"] = RunManager.Instance.ShouldSave,
            ["location"] = capture.Snapshot["location"],
        });
    }

    private BridgeCommandOperation ConsoleExec(JsonElement request, BridgeCommandDescriptor descriptor)
    {
        RunState run = RequireRun();
        if (CombatManager.Instance.IsInProgress)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, "console.exec fight requires no active combat");
        JsonElement args = request.GetProperty("args");
        string input = RequiredString(args, "input").Trim();
        string[] tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length != 2 || !StringComparer.OrdinalIgnoreCase.Equals(tokens[0], "fight"))
        {
            throw new BridgeStateException(
                ErrorCodes.InvalidArgument,
                "v0.2 console.exec allowlist only accepts exactly: fight <encounter-id>");
        }
        string normalized = $"fight {tokens[1].ToUpperInvariant()}";
        _devConsole ??= new MegaCrit.Sts2.Core.DevConsole.DevConsole(shouldAllowDebugCommands: true);
        CmdResult command = _devConsole.ProcessNetCommand(run.Players[0], normalized);
        if (!command.success)
            throw new BridgeStateException(ErrorCodes.InvalidArgument, command.msg);
        if (command.task is null)
        {
            throw new BridgeStateException(
                ErrorCodes.ActionCorrelationFailed,
                "allowlisted fight command did not return its documented CmdResult.task");
        }
        return new BridgeCommandOperation(
            descriptor,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["backend"] = "dev_console",
                ["input"] = normalized,
                ["message"] = command.msg,
            },
            CompletionTask: command.task,
            ReadyPredicate: () => CombatManager.Instance.IsInProgress
                && run.Players[0].PlayerCombatState?.Phase == PlayerTurnPhase.Play,
            Finalize: post => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["combat_epoch"] = IdentityValue(post, "combat_epoch"),
                ["location"] = post.Snapshot["location"],
            });
    }

    private BridgeCommandOperation CombatStatus(BridgeCommandDescriptor descriptor)
    {
        RequireCombat();
        SnapshotCapture capture = snapshots.Capture("combat.status", persist: false);
        return Immediate(descriptor, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["combat_epoch"] = IdentityValue(capture, "combat_epoch"),
            ["location"] = capture.Snapshot["location"],
            ["actions"] = actions.Snapshot(),
        });
    }

    private BridgeCommandOperation AddCard(JsonElement request, BridgeCommandDescriptor descriptor)
    {
        RequireCombat();
        JsonElement args = request.GetProperty("args");
        Player player = ResolvePlayer(args);
        PlayerCombatState playerState = player.PlayerCombatState
            ?? throw new BridgeStateException(ErrorCodes.InvalidPhase, "combat.add_card requires player combat state");
        string modelId = RequiredString(args, "model_id").ToUpperInvariant();
        string pile = OptionalString(args, "pile") ?? nameof(PileType.Hand);
        if (!StringComparer.OrdinalIgnoreCase.Equals(pile, nameof(PileType.Hand)))
            throw new BridgeStateException(ErrorCodes.InvalidArgument, "v0.2 combat.add_card only supports the Hand pile");
        if (playerState.Hand.Cards.Count >= CardPile.MaxCardsInHand)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, "the target player's hand is full");

        CardModel[] matches = ModelDb.AllCards
            .Where(card => StringComparer.OrdinalIgnoreCase.Equals(card.Id.Entry, modelId))
            .ToArray();
        if (matches.Length == 0)
            throw new BridgeStateException(ErrorCodes.NotFound, $"card model not found: {modelId}");
        if (matches.Length != 1)
            throw new BridgeStateException(ErrorCodes.AmbiguousId, $"card model is ambiguous: {modelId}");

        CardModel[] before = playerState.Hand.Cards.ToArray();
        _devConsole ??= new MegaCrit.Sts2.Core.DevConsole.DevConsole(shouldAllowDebugCommands: true);
        CmdResult command = _devConsole.ProcessNetCommand(player, $"card {modelId} Hand");
        if (!command.success)
            throw new BridgeStateException(ErrorCodes.InvalidArgument, command.msg);
        if (command.task is null)
        {
            throw new BridgeStateException(
                ErrorCodes.ActionCorrelationFailed,
                "card command did not return its documented CmdResult.task");
        }

        return new BridgeCommandOperation(
            descriptor,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["backend"] = "dev_console",
                ["model_id"] = matches[0].Id.ToString(),
                ["owner_id"] = player.NetId,
                ["pile"] = nameof(PileType.Hand),
                ["message"] = command.msg,
            },
            CompletionTask: command.task,
            Finalize: _ =>
            {
                CardModel[] added = playerState.Hand.Cards
                    .Where(candidate => !before.Any(old => ReferenceEquals(old, candidate)))
                    .ToArray();
                if (added.Length != 1)
                {
                    throw new BridgeStateException(
                        ErrorCodes.ActionCorrelationFailed,
                        $"card command changed the hand by {added.Length} reference(s), expected exactly one");
                }
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["card_handle"] = snapshots.Handles.IssueCombatCard(added[0]),
                    ["combat_card_index"] = MegaCrit.Sts2.Core.Entities.Multiplayer.NetCombatCard.FromModel(added[0]).CombatCardIndex,
                };
            });
    }

    private BridgeCommandOperation PlayCard(
        JsonElement request,
        BridgeCommandDescriptor descriptor,
        string requestId)
    {
        RequireCombat();
        if (RunManager.Instance.NetService.Type != NetGameType.Singleplayer)
            throw new BridgeStateException(ErrorCodes.CapabilityUnavailable, "v0.2 typed card play is singleplayer-only");
        JsonElement args = request.GetProperty("args");
        CardModel card = snapshots.Handles.Resolve<CardModel>(RequiredString(args, "card"), "combat-card");
        PlayerCombatState playerState = card.Owner.PlayerCombatState
            ?? throw new BridgeStateException(ErrorCodes.InvalidPhase, "card owner has no combat state");
        if (playerState.Phase != PlayerTurnPhase.Play)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, $"card play requires PlayerTurnPhase.Play, actual={playerState.Phase}");
        if (card.Pile?.Type != PileType.Hand)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, "card handle is not currently in the owner's hand");

        Creature? target = null;
        if (args.TryGetProperty("target", out JsonElement targetValue)
            && targetValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            if (targetValue.ValueKind != JsonValueKind.String)
                throw new BridgeStateException(ErrorCodes.InvalidArgument, "target must be a server-issued creature handle or null");
            target = snapshots.Handles.Resolve<Creature>(targetValue.GetString() ?? "", "creature");
        }
        if (!card.IsValidTarget(target))
            throw new BridgeStateException(ErrorCodes.InvalidArgument, $"target is invalid for {card.TargetType}");
        if (!card.CanPlay(out UnplayableReason reason, out AbstractModel? preventer))
        {
            throw new BridgeStateException(
                ErrorCodes.InvalidPhase,
                $"card cannot be played: reason={reason}, preventer={preventer?.Id.ToString() ?? "none"}");
        }

        int energyBefore = playerState.Energy;
        var action = new PlayCardAction(card, target);
        string actionHandle = actions.RegisterCorrelation(action, requestId);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
        if (!actions.IsEnqueued(action) || action.Id is null)
        {
            throw new BridgeStateException(
                ErrorCodes.ActionCorrelationFailed,
                "singleplayer RequestEnqueue did not enqueue the exact typed PlayCardAction reference");
        }

        return new BridgeCommandOperation(
            descriptor,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["backend"] = "typed_play_card_action",
                ["card_handle"] = RequiredString(args, "card"),
                ["target_handle"] = target is null ? null : RequiredString(args, "target"),
                ["action_handle"] = actionHandle,
                ["action_id"] = action.Id,
                ["energy_before"] = energyBefore,
            },
            CompletionTask: action.CompletionTask,
            Action: action,
            Finalize: _ => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["action_id_final"] = action.Id,
                ["action_state"] = action.State.ToString(),
                ["energy_after"] = playerState.Energy,
            });
    }

    private Player ResolvePlayer(JsonElement args)
    {
        RunState run = RequireRun();
        if (args.TryGetProperty("owner", out JsonElement owner)
            && owner.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
        {
            if (owner.ValueKind != JsonValueKind.String)
                throw new BridgeStateException(ErrorCodes.InvalidArgument, "owner must be a server-issued player handle");
            return snapshots.Handles.Resolve<Player>(owner.GetString() ?? "", "player");
        }
        if (run.Players.Count != 1)
            throw new BridgeStateException(ErrorCodes.AmbiguousId, "owner is required when more than one player exists");
        return run.Players[0];
    }

    private static RunState RequireRun() =>
        RunManager.Instance.DebugOnlyGetState()
        ?? throw new BridgeStateException(ErrorCodes.InvalidPhase, "command requires an active run");

    private static CombatState RequireCombat()
    {
        CombatState? combat = CombatManager.Instance.DebugOnlyGetState();
        if (!CombatManager.Instance.IsInProgress || combat is null)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, "command requires an active combat");
        return combat;
    }

    private static T UniqueModel<T>(IEnumerable<T> models, string entry, string kind) where T : AbstractModel
    {
        T[] matches = models
            .Where(model => StringComparer.OrdinalIgnoreCase.Equals(model.Id.Entry, entry))
            .ToArray();
        return matches.Length switch
        {
            0 => throw new BridgeStateException(ErrorCodes.NotFound, $"{kind} model not found: {entry}"),
            1 => matches[0],
            _ => throw new BridgeStateException(ErrorCodes.AmbiguousId, $"{kind} model is ambiguous: {entry}"),
        };
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

    private static string? OptionalString(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
            throw new BridgeStateException(ErrorCodes.InvalidArgument, $"args.{name} must be a string");
        return value.GetString();
    }

    private static int OptionalInt(JsonElement args, string name, int minimum, int maximum)
    {
        if (!args.TryGetProperty(name, out JsonElement value))
            return minimum;
        if (!value.TryGetInt32(out int result) || result < minimum || result > maximum)
        {
            throw new BridgeStateException(
                ErrorCodes.InvalidArgument,
                $"args.{name} must be an integer in [{minimum}, {maximum}]");
        }
        return result;
    }

    private static object? IdentityValue(SnapshotCapture capture, string name)
    {
        var identity = (Dictionary<string, object?>)capture.Snapshot["identity"]!;
        return identity[name];
    }

    private static BridgeCommandOperation Immediate(
        BridgeCommandDescriptor descriptor,
        Dictionary<string, object?> result) => new(descriptor, result);

    private static void ValidateWaitFor(BridgeCommandDescriptor descriptor, string waitFor)
    {
        bool valid = descriptor.CompletionStrategy switch
        {
            "immediate_query" => waitFor == "immediate",
            "typed_action_reference" => waitFor is "enqueued" or "action_finished" or "queue_settled",
            "awaitable_cmd_result" or "location_predicate" => waitFor is "action_finished" or "queue_settled",
            _ => false,
        };
        if (!valid)
        {
            throw new BridgeStateException(
                ErrorCodes.InvalidArgument,
                $"{descriptor.Name} does not support wait_for={waitFor}");
        }
    }
}

public static class BridgeCapabilities
{
    public static Dictionary<string, object?> Create() => new(StringComparer.Ordinal)
    {
        ["named_pipe_duplex"] = State("available"),
        ["bidirectional_hmac"] = State("available"),
        ["main_thread_dispatch"] = State("available"),
        ["state_dump"] = State("available"),
        ["typed_card_play"] = State("available"),
        ["card_select_local_selector"] = State("unavailable", "D6 adapter not registered"),
        ["pixel_output"] = State("unknown", "H0 capability probe only"),
        ["virtual_clock"] = State("unavailable", "not supported by v0.2"),
    };

    private static Dictionary<string, object?> State(string state, string? reason = null) =>
        new(StringComparer.Ordinal) { ["state"] = state, ["reason"] = reason };
}
