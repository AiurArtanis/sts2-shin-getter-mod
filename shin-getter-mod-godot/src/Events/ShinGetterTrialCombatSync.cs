#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Events;

internal sealed class ShinGetterTrialCombatSync : IDisposable
{
    internal const string StartFightOptionKey = "SHIN_GETTER_EVENT_INVASION.TRIAL.pages.RYOMA.options.START_FIGHT";
    private const string ByrdonisStartFightOptionKey = "SHIN_GETTER_EVENT_INVASION.BYRDONIS_NEST.pages.RYOMA.options.START_FIGHT";
    private static readonly ConditionalWeakTable<EventSynchronizer, ShinGetterTrialCombatSync> Instances = new();
    private readonly EventSynchronizer _synchronizer;
    private readonly RunLocationTargetedMessageBuffer _buffer;
    private readonly INetGameService _netService;
    private readonly IPlayerCollection _players;
    private readonly ulong _runSeed;
    private EventModel? _localEvent;
    private RunLocation _location;
    private Player? _choosingOwner;
    private Player? _requestedOwner;
    private ulong? _acceptedOwner;
    private bool _entryAttempted;
    private bool _disposed;

    private ShinGetterTrialCombatSync(EventSynchronizer synchronizer,
        RunLocationTargetedMessageBuffer buffer, INetGameService netService,
        IPlayerCollection players, ulong runSeed)
    {
        _synchronizer = synchronizer;
        _buffer = buffer;
        _netService = netService;
        _players = players;
        _runSeed = runSeed;
        _buffer.RegisterMessageHandler<ShinGetterTrialChallengeMessage>(ReceiveCommit);
        _netService.Disconnected += OnDisconnected;
    }

    internal static void Attach(EventSynchronizer synchronizer,
        RunLocationTargetedMessageBuffer buffer, INetGameService netService,
        IPlayerCollection players, ulong seed) =>
        Instances.Add(synchronizer, new ShinGetterTrialCombatSync(synchronizer, buffer, netService, players, seed));

    internal static void BeginEvent(EventSynchronizer synchronizer, bool isPrefinished)
    {
        if (!Instances.TryGetValue(synchronizer, out var sync) || sync._disposed)
            return;
        sync.CloseEvent();
        sync._acceptedOwner = null;
        sync._entryAttempted = false;
        EventModel localEvent = synchronizer.GetLocalEvent();
        Player? owner = localEvent.Owner;
        if (!isPrefinished && GetStartFightOptionKey(localEvent) != null
            && owner != null && sync._players.Players.Count > 1)
        {
            sync._localEvent = localEvent;
            sync._location = owner.RunState.RunLocation;
        }
    }

    private static string? GetStartFightOptionKey(EventModel? eventModel) => eventModel switch
    {
        Trial => StartFightOptionKey,
        ByrdonisNest => ByrdonisStartFightOptionKey,
        _ => null,
    };

    internal static bool IsChallengeOption(EventOption option) => option.IsProceed
        && option.TextKey is StartFightOptionKey or ByrdonisStartFightOptionKey;

    internal static bool TryRouteProceed(EventSynchronizer synchronizer, EventOption option, int index)
    {
        if (!IsChallengeOption(option) || option.IsLocked || option.WasChosen
            || !Instances.TryGetValue(synchronizer, out var sync) || !sync.IsCurrentEventBattle
            || sync._netService.Type is not (NetGameType.Host or NetGameType.Client))
            return false;

        EventModel? localEvent = sync._localEvent;
        Player? owner = localEvent?.Owner;
        if (localEvent == null || option.TextKey != GetStartFightOptionKey(localEvent)
            || owner == null || !IsEligible(owner)
            || sync._players.Players.Count <= 1
            || !ReferenceEquals(synchronizer.GetLocalEvent(), localEvent)
            || index < 0 || index >= localEvent.CurrentOptions.Count
            || !ReferenceEquals(localEvent.CurrentOptions[index], option))
            return false;

        // Native Proceed buttons bypass ChooseLocalOption; route only this multiplayer challenge.
        synchronizer.ChooseLocalOption(index);
        return true;
    }

    internal static void Request(EventSynchronizer synchronizer, EventModel eventModel)
    {
        if (!Instances.TryGetValue(synchronizer, out var sync))
            throw new InvalidOperationException("Event combat synchronization was not initialized.");
        Player? owner = eventModel.Owner;
        string? optionKey = GetStartFightOptionKey(eventModel);
        if (!sync.IsCurrentEventBattle || sync._netService.Type != NetGameType.Host
            || sync._acceptedOwner.HasValue || sync._requestedOwner != null
            || optionKey == null || optionKey != GetStartFightOptionKey(sync._localEvent)
            || owner == null || !IsEligible(owner)
            || !ReferenceEquals(sync._choosingOwner, owner)
            || !ReferenceEquals(synchronizer.GetEventForPlayer(owner), eventModel)
            || !eventModel.CurrentOptions.Any(option => option.IsProceed && option.TextKey == optionKey))
            return;

        // Only native ChooseOptionForEvent invokes this callback. Its sender selects this Owner's event.
        // Commit in that method's Postfix, after native option history has been recorded.
        sync._requestedOwner = owner;
    }

    internal static void BeforeChoice(EventSynchronizer synchronizer, Player player)
    {
        if (Instances.TryGetValue(synchronizer, out var sync))
            sync._choosingOwner = player;
    }

    internal static void AfterChoice(EventSynchronizer synchronizer, Player player)
    {
        if (!Instances.TryGetValue(synchronizer, out var sync))
            return;
        sync._choosingOwner = null;
        if (!sync.IsCurrentEventBattle
            || sync._netService.Type != NetGameType.Host || sync._acceptedOwner.HasValue
            || !ReferenceEquals(sync._requestedOwner, player))
            return;

        Player owner = sync._requestedOwner;
        sync._requestedOwner = null;
        sync._acceptedOwner = owner.NetId;
        var message = new ShinGetterTrialChallengeMessage
        {
            OwnerId = owner.NetId,
            RunSeed = sync._runSeed,
            Location = sync._location,
        };
        sync._netService.SendMessage(message);
        sync.StartCommittedBattle();
    }

    internal static void ChoiceFailed(EventSynchronizer synchronizer, Player player)
    {
        if (Instances.TryGetValue(synchronizer, out var sync))
        {
            sync._choosingOwner = null;
            if (ReferenceEquals(sync._requestedOwner, player))
                sync._requestedOwner = null;
        }
    }

    private bool IsCurrentEventBattle
    {
        get
        {
            Player? owner = _localEvent?.Owner;
            return !_disposed && owner != null && owner.RunState.RunLocation == _location;
        }
    }

    private static bool IsEligible(Player owner) => owner.Character is ShinGetter
        && (owner.GetRelic<SGR_GetterFurnace>()?.EventInvasionEnabled
            ?? owner.GetRelic<SGR_EmperorsFragment>()?.EventInvasionEnabled ?? false);

    private void ReceiveCommit(ShinGetterTrialChallengeMessage message, ulong senderId)
    {
        if (!IsCurrentEventBattle || _netService.Type != NetGameType.Client
            || _netService is not INetClientGameService client
            || client.NetClient?.HostNetId != senderId
            || message.RunSeed != _runSeed || message.Location != _location
            || _acceptedOwner.HasValue
            || _players.GetPlayer(message.OwnerId) is not { } owner || !IsEligible(owner))
            return;

        _acceptedOwner = owner.NetId;
        StartCommittedBattle();
    }

    private void StartCommittedBattle()
    {
        if (_entryAttempted || _localEvent == null || !_acceptedOwner.HasValue)
            return;
        // This is an attempt latch, not an asynchronous completion receipt. Never reopen after broadcast.
        _entryAttempted = true;
        Player owner = _players.GetPlayer(_acceptedOwner.Value)
            ?? throw new InvalidOperationException("Accepted event challenger left the run.");
        EventModel[] events = ResolveCombatEvents(_synchronizer, _localEvent, _players.Players);
        ShinGetterEventInvasionService.StartCommittedEventBattle(_localEvent, events, owner);
    }

    internal static EventModel[] ResolveCombatEvents(EventSynchronizer synchronizer, EventModel activeEvent,
        IReadOnlyList<Player> players)
    {
        // v0.111 waits for every native player slot before creating the room.
        var events = new EventModel[players.Count];
        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            EventModel playerEvent = synchronizer.GetEventForPlayer(player);
            if (!ReferenceEquals(playerEvent.Owner, player)
                || !ReferenceEquals(playerEvent.CanonicalInstance, activeEvent.CanonicalInstance))
                throw new InvalidOperationException("Event combat has an inconsistent native player slot.");
            events[i] = playerEvent;
        }
        return events;
    }

    internal static void EndEvent(EventSynchronizer synchronizer)
    {
        if (Instances.TryGetValue(synchronizer, out var sync))
            sync.CloseEvent();
    }

    private void CloseEvent()
    {
        _localEvent = null;
        _choosingOwner = null;
        _requestedOwner = null;
        ShinGetterEventInvasionService.ClearPendingEventBattleSetup(_players.Players);
    }

    internal static void Detach(EventSynchronizer synchronizer)
    {
        if (Instances.TryGetValue(synchronizer, out var sync))
        {
            sync.Dispose();
            Instances.Remove(synchronizer);
        }
    }

    private void OnDisconnected(NetErrorInfo _) => Dispose();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        CloseEvent();
        _buffer.UnregisterMessageHandler<ShinGetterTrialChallengeMessage>(ReceiveCommit);
        _netService.Disconnected -= OnDisconnected;
    }
}
