using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using Sts2HeadlessTestBridge.Contract;

namespace Sts2HeadlessTestBridge.State;

public sealed record StructuralEpochs(
    string Process,
    string? Run,
    string? Room,
    string? Combat,
    long StateRevision);

internal sealed record HandleBinding(
    string Kind,
    object Value,
    string ProcessEpoch,
    string? RunEpoch,
    string? RoomEpoch,
    string? CombatEpoch);

/// <summary>
/// Issues opaque server-side handles whose lifetime follows the structural game
/// epochs. Ordinary state revisions deliberately do not invalidate handles.
/// </summary>
public sealed class StableHandleRegistry(string processEpoch)
{
    private readonly Dictionary<string, HandleBinding> _bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Kind, object Value), string> _issued = new(new HandleKeyComparer());
    private RunState? _run;
    private AbstractRoom? _room;
    private CombatState? _combat;
    private string? _combatIdentity;
    private long _runGeneration;
    private long _roomGeneration;
    private long _combatGeneration;
    private long _revision;

    public StructuralEpochs Epochs { get; private set; } = new(processEpoch, null, null, null, 0);

    public StructuralEpochs Synchronize(RunState? run, AbstractRoom? room, CombatState? combat, object? combatIdentity)
    {
        if (!ReferenceEquals(run, _run))
        {
            _run = run;
            _room = null;
            _combat = null;
            _combatIdentity = null;
            _runGeneration++;
            _roomGeneration++;
            _combatGeneration++;
            _bindings.Clear();
            _issued.Clear();
        }

        if (!ReferenceEquals(room, _room))
        {
            _room = room;
            _combat = null;
            _combatIdentity = null;
            _roomGeneration++;
            _combatGeneration++;
            RemoveScoped("room", "creature", "combat-card", "action");
        }

        string? currentCombatIdentity = combatIdentity?.ToString();
        if (!ReferenceEquals(combat, _combat)
            || !StringComparer.Ordinal.Equals(currentCombatIdentity, _combatIdentity))
        {
            _combat = combat;
            _combatIdentity = currentCombatIdentity;
            _combatGeneration++;
            RemoveScoped("creature", "combat-card", "action");
        }

        Epochs = new StructuralEpochs(
            processEpoch,
            run is null ? null : $"r{_runGeneration}",
            room is null ? null : $"room{_roomGeneration}",
            combat is null ? null : $"combat{_combatGeneration}",
            _revision);
        return Epochs;
    }

    public long CommitRevision()
    {
        _revision++;
        Epochs = Epochs with { StateRevision = _revision };
        return _revision;
    }

    public string IssuePlayer(Player player) => Issue("player", player, player.NetId.ToString());

    public string IssueRoom(AbstractRoom room) => Issue(
        "room",
        room,
        room.Id?.ToString() ?? $"pending-{RuntimeHelpers.GetHashCode(room):x8}");

    public string IssueCreature(Creature creature)
    {
        string identifier = creature.CombatId?.ToString()
            ?? throw new BridgeStateException(ErrorCodes.InvalidPhase, "creature has no combat id");
        return Issue("creature", creature, identifier);
    }

    public string IssueCombatCard(CardModel card)
    {
        uint identifier;
        try
        {
            identifier = NetCombatCard.FromModel(card).CombatCardIndex;
        }
        catch (Exception exception)
        {
            throw new BridgeStateException(
                ErrorCodes.StaleHandle,
                $"card is not registered in the active combat database: {exception.Message}");
        }
        return Issue("combat-card", card, identifier.ToString());
    }

    public string IssueAction(object action, uint? actionId)
    {
        string identifier = actionId?.ToString() ?? $"pending-{RuntimeHelpers.GetHashCode(action):x8}";
        return Issue("action", action, identifier);
    }

    public T Resolve<T>(string handle, string expectedKind) where T : class
    {
        if (!_bindings.TryGetValue(handle, out HandleBinding? binding)
            || !StringComparer.Ordinal.Equals(binding.Kind, expectedKind)
            || !IsValid(binding)
            || binding.Value is not T value)
        {
            throw new BridgeStateException(ErrorCodes.StaleHandle, "server-issued handle is stale, unknown, or has the wrong kind");
        }
        return value;
    }

    private string Issue(string kind, object value, string identifier)
    {
        RequireScope(kind);
        var key = (kind, value);
        if (_issued.TryGetValue(key, out string? existing) && _bindings.ContainsKey(existing))
            return existing;

        List<string?> pieces = [kind, Epochs.Process, Epochs.Run];
        if (kind == "room")
            pieces.Add(Epochs.Room);
        if (kind is "creature" or "combat-card" or "action")
            pieces.Add(Epochs.Combat);
        pieces.Add(identifier);
        string handle = string.Join(":", pieces);
        _bindings[handle] = new HandleBinding(
            kind,
            value,
            Epochs.Process,
            Epochs.Run,
            kind == "room" ? Epochs.Room : null,
            kind is "creature" or "combat-card" or "action" ? Epochs.Combat : null);
        _issued[key] = handle;
        return handle;
    }

    private void RequireScope(string kind)
    {
        if (Epochs.Run is null)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, $"{kind} handle requires an active run");
        if (kind == "room" && Epochs.Room is null)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, "room handle requires an active room");
        if (kind is "creature" or "combat-card" or "action" && Epochs.Combat is null)
            throw new BridgeStateException(ErrorCodes.InvalidPhase, $"{kind} handle requires an active combat");
    }

    private bool IsValid(HandleBinding binding) =>
        StringComparer.Ordinal.Equals(binding.ProcessEpoch, Epochs.Process)
        && StringComparer.Ordinal.Equals(binding.RunEpoch, Epochs.Run)
        && (binding.Kind != "room" || StringComparer.Ordinal.Equals(binding.RoomEpoch, Epochs.Room))
        && (binding.Kind is not ("creature" or "combat-card" or "action")
            || StringComparer.Ordinal.Equals(binding.CombatEpoch, Epochs.Combat));

    private void RemoveScoped(params string[] kinds)
    {
        var selected = new HashSet<string>(kinds, StringComparer.Ordinal);
        foreach (string handle in _bindings.Where(item => selected.Contains(item.Value.Kind)).Select(item => item.Key).ToArray())
        {
            HandleBinding binding = _bindings[handle];
            _bindings.Remove(handle);
            _issued.Remove((binding.Kind, binding.Value));
        }
    }

    private sealed class HandleKeyComparer : IEqualityComparer<(string Kind, object Value)>
    {
        public bool Equals((string Kind, object Value) left, (string Kind, object Value) right) =>
            StringComparer.Ordinal.Equals(left.Kind, right.Kind) && ReferenceEquals(left.Value, right.Value);

        public int GetHashCode((string Kind, object Value) value) =>
            HashCode.Combine(StringComparer.Ordinal.GetHashCode(value.Kind), RuntimeHelpers.GetHashCode(value.Value));
    }
}

public sealed class BridgeStateException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
