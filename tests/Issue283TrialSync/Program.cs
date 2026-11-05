using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Events;
using TrialFixture;

int passed = 0;
void Check(string name, Action action)
{
    ShinGetterEventInvasionService.Entries.Clear();
    ShinGetterEventInvasionService.Pending.Clear();
    ShinGetterEventInvasionService.FailEntry = false;
    action();
    Console.WriteLine("PASS " + name);
    passed++;
}
static void Assert(bool value, string text) { if (!value) throw new Exception(text); }
static int Entries(Peer peer) => ShinGetterEventInvasionService.Entries.Count(entry => peer.Players.Contains(entry.Owner));
static ulong[] Pending(Peer peer) => peer.Players.Where(ShinGetterEventInvasionService.Pending.ContainsKey).Select(p => p.NetId).ToArray();
static void Consistent(Peer host, Peer client, ulong owner)
{
    Assert(Entries(host) == 1 && Entries(client) == 1, "duplicate/missing local battle entry");
    Assert(Pending(host).SequenceEqual(new[] { owner }) && Pending(client).SequenceEqual(new[] { owner }), "pending owners disagree");
    Assert(ShinGetterEventInvasionService.Entries.All(entry => entry.Event.Owner?.NetId == (host.Players.Contains(entry.Owner) ? 11UL : 22UL)), "foreign event used for entry");
}

Check("modeled native Proceed branch: Getter host click reaches synchronization once", () =>
{
    using var host = new Peer(true, 11);
    using var client = new Peer(false, 22);
    host.ClickStart();
    Assert(host.Net.Sent.Count == 1 && host.Sync.OutgoingChoices.Count == 1 && Entries(host) == 1,
        "IsProceed START_FIGHT bypassed the synchronized request/commit route");
    Assert(host.Room.NativeProceedCalls == 0 && host.History.SequenceEqual(new[] { 11UL }), "routed click still used original local-only Proceed branch");
    client.Deliver(host.Net.Sent.Single());
    client.Choose(11); // Host option may arrive after the commit, but must still be recorded natively.
    Consistent(host, client, 11);
    Assert(host.History.SequenceEqual(client.History), "late original choice missing history");
});

Check("modeled native Proceed branch: Getter client sends one request to non-Getter host", () =>
{
    using var host = new Peer(true, 11, hostGetter: false);
    using var client = new Peer(false, 22, hostGetter: false);
    client.ClickStart();
    Assert(client.Sync.OutgoingChoices.Count == 1 && client.Net.Sent.Count == 0 && Entries(client) == 0,
        "client Proceed action was not sent as a native event choice");
    var choice = client.Sync.OutgoingChoices.Single();
    host.Choose(choice.Owner);
    Assert(host.Net.Sent.Count == 1, "host failed to accept native client request");
    client.Deliver(host.Net.Sent.Single());
    client.ClickStart();
    Consistent(host, client, 22);
    Assert(host.History.SequenceEqual(client.History), "client-origin native history differs");
});

foreach (ulong firstAccepted in new[] { 11UL, 22UL })
{
    Check($"two Proceed clicks with reversed host arrival winner={firstAccepted}", () =>
    {
        using var host = new Peer(true, 11);
        using var client = new Peer(false, 22);
        client.ClickStart();
        if (firstAccepted == 22)
            host.Choose(22);
        host.ClickStart();
        if (firstAccepted == 11)
            host.Choose(22);
        Assert(host.Net.Sent.Count == 1, "opposite arrival order made multiple decisions");
        client.Deliver(host.Net.Sent.Single());
        Consistent(host, client, firstAccepted);
        host.DrawFirstHand();
        client.DrawFirstHand();
        client.Choose(11); // Replicated original host option arrives after the first-hand hook.
        host.ClickStart();
        client.ClickStart();
        client.Deliver(host.Net.Sent.Single());
        Assert(host.Sync.OutgoingChoices.Count == 1 && client.Sync.OutgoingChoices.Count == 1, "repeated Proceed click resent an already-chosen option");
        Assert(host.History.Order().SequenceEqual(client.History.Order()) && host.History.Count == 2,
            "each native owner choice must be recorded once on every peer");
        Assert(host.Played.SequenceEqual(new[] { firstAccepted }) && client.Played.SequenceEqual(new[] { firstAccepted })
            && Pending(host).Length == 0 && Pending(client).Length == 0, "late original option changed setup");
    });
}

Check("unrelated Proceed, ordinary choice and singleplayer keep native routing", () =>
{
    using var peer = new Peer(true, 11);
    RunManager.Instance.EventSynchronizer = peer.Sync;
    int callbacks = 0;
    peer.Room.OptionButtonClicked(new EventOption("PROCEED", () => callbacks++), 0);
    Assert(callbacks == 1 && peer.Room.NativeProceedCalls == 1 && peer.Sync.OutgoingChoices.Count == 0,
        "unrelated Proceed was rerouted");
    peer.Sync.Models[0].CurrentOptions[0] = new EventOption("TRIAL.pages.INITIAL.options.ACCEPT", () => callbacks++, isProceed: false);
    peer.Room.OptionButtonClicked(peer.Sync.Models[0].CurrentOptions[0], 0);
    Assert(callbacks == 2 && peer.Sync.OutgoingChoices.Count == 1 && peer.Net.Sent.Count == 0,
        "ordinary event choice changed route");
    using var single = new Peer(true, 11, singleplayer: true);
    single.ClickStart();
    Assert(single.Room.NativeProceedCalls == 1 && single.Sync.OutgoingChoices.Count == 0,
        "singleplayer START_FIGHT no longer uses its original Proceed route");
});

Check("locked, mismatched option/index, wrong event and nullable Owner are rejected safely", () =>
{
    using var peer = new Peer(true, 11);
    RunManager.Instance.EventSynchronizer = peer.Sync;
    var original = peer.Sync.Models[0].CurrentOptions[0];
    Assert(!ShinGetterTrialCombatSync.TryRouteProceed(peer.Sync, original, -1)
        && !ShinGetterTrialCombatSync.TryRouteProceed(peer.Sync, original, 1), "out-of-range index accepted");
    Assert(!ShinGetterTrialCombatSync.TryRouteProceed(peer.Sync, new EventOption(Peer.StartKey), 0), "foreign button option accepted");
    var locked = new EventOption(Peer.StartKey) { IsLocked = true };
    peer.Sync.Models[0].CurrentOptions[0] = locked;
    peer.Room.OptionButtonClicked(locked, 0);
    Assert(peer.Sync.OutgoingChoices.Count == 0 && peer.Net.Sent.Count == 0, "locked button selected");
    peer.Sync.Models[0].CurrentOptions[0] = original;
    peer.Sync.Models[0].Owner = null;
    peer.Room.OptionButtonClicked(original, 0);
    Assert(peer.Sync.OutgoingChoices.Count == 0 && Entries(peer) == 0, "unbound Owner was routed");
    peer.Sync.Models[0].Owner = peer.Players[0];
    peer.Sync.Models[0] = new EventModel(peer.Players[0]);
    ProductionHook.Call("ShinGetterTrialSyncBeginPatch", "Postfix", peer.Sync, false);
    Assert(!ShinGetterTrialCombatSync.TryRouteProceed(peer.Sync, original, 0), "non-Trial scope accepted");
});

foreach (ulong winner in new[] { 11UL, 22UL })
foreach (int acceptedOptionDelay in new[] { 0, 1, 2 })
foreach (bool localChoiceFirst in new[] { false, true })
{
    Check($"opposite peer order winner={winner}, option delay={acceptedOptionDelay}, client first={localChoiceFirst}", () =>
    {
        using var host = new Peer(true, 11);
        using var client = new Peer(false, 22);
        ulong other = winner == 11 ? 22UL : 11UL;
        if (localChoiceFirst)
            client.Choose(other);
        host.Choose(winner);
        Assert(host.Net.Sent.Count == 1, "host did not commit once after history");
        if (acceptedOptionDelay == 0)
            client.Choose(winner);
        client.Deliver(host.Net.Sent[0]);
        Consistent(host, client, winner);
        if (acceptedOptionDelay == 1)
            client.Choose(winner);
        host.DrawFirstHand();
        client.DrawFirstHand(); // Native sync may already have completed before the original option arrives.
        if (acceptedOptionDelay == 2)
            client.Choose(winner);
        host.Choose(other);
        if (!localChoiceFirst)
            client.Choose(other);
        client.Deliver(host.Net.Sent[0]);
        host.DrawFirstHand();
        client.DrawFirstHand();
        Assert(Entries(host) == 1 && Entries(client) == 1 && host.Net.Sent.Count == 1, "late/duplicate challenge restarted battle");
        Assert(Pending(host).Length == 0 && Pending(client).Length == 0, "late option resurrected setup");
        Assert(host.Played.SequenceEqual(new[] { winner }) && client.Played.SequenceEqual(new[] { winner }), "first-turn setup lost or repeated");
        Assert(host.History.Order().SequenceEqual(client.History.Order()), "modeled per-owner option history was dropped");
    });
}

Check("mixed party non-Getter host accepts Getter client only", () =>
{
    using var host = new Peer(true, 11, hostGetter: false);
    using var client = new Peer(false, 22, hostGetter: false);
    host.Choose(11);
    Assert(host.Net.Sent.Count == 0, "non-Getter was accepted");
    host.Choose(22);
    client.Deliver(host.Net.Sent.Single());
    Consistent(host, client, 22);
});

Check("commit requires real host, correct run/location and eligible existing owner", () =>
{
    using var client = new Peer(false, 22, hostGetter: false);
    var valid = client.Commit(22);
    client.Deliver(valid, 22);
    client.Deliver(valid with { RunSeed = 999 });
    client.Deliver(valid with { Location = new RunLocation(1, 5, 9) });
    client.Deliver(valid with { OwnerId = 999 });
    client.Deliver(valid with { OwnerId = 11 });
    client.Players[1].Furnace.EventInvasionEnabled = false;
    client.Deliver(valid);
    client.Players[1].Furnace.EventInvasionEnabled = true;
    Assert(Entries(client) == 0, "invalid authority/context accepted");
    client.Deliver(valid);
    client.Deliver(valid);
    Assert(Entries(client) == 1 && Pending(client).SequenceEqual(new[] { 22UL }), "valid commit not idempotent");
});

Check("host ignores received commit and rejects foreign/wrong-page requests", () =>
{
    using var host = new Peer(true, 11);
    host.Deliver(host.Commit(22), 22);
    var foreign = new Trial(host.Players[0]);
    foreign.CurrentOptions.Add(new EventOption(Peer.StartKey));
    ShinGetterTrialCombatSync.BeforeChoice(host.Sync, host.Players[0]);
    ShinGetterTrialCombatSync.Request(host.Sync, foreign);
    ShinGetterTrialCombatSync.AfterChoice(host.Sync, host.Players[0]);
    host.Sync.Models[0].CurrentOptions.Clear();
    ShinGetterTrialCombatSync.BeforeChoice(host.Sync, host.Players[0]);
    ShinGetterTrialCombatSync.Request(host.Sync, host.Sync.Models[0]);
    ShinGetterTrialCombatSync.AfterChoice(host.Sync, host.Players[0]);
    Assert(host.Net.Sent.Count == 0 && Entries(host) == 0, "unbound/invalid request accepted");
});

Check("request owner must match completed native choice; failed choice is not committed", () =>
{
    using var host = new Peer(true, 11);
    ShinGetterTrialCombatSync.BeforeChoice(host.Sync, host.Players[0]);
    ShinGetterTrialCombatSync.Request(host.Sync, host.Sync.Models[0]);
    ShinGetterTrialCombatSync.AfterChoice(host.Sync, host.Players[1]);
    Assert(host.Net.Sent.Count == 0, "another owner's choice committed request");
    ShinGetterTrialCombatSync.ChoiceFailed(host.Sync, host.Players[0]);
    ShinGetterTrialCombatSync.AfterChoice(host.Sync, host.Players[0]);
    Assert(host.Net.Sent.Count == 0, "failed choice committed");
    host.Choose(22);
    Assert(host.Net.Sent.Single().OwnerId == 22, "failed uncommitted request blocked next valid request");
});

Check("native sender/owner choice scope cannot request another player's challenge", () =>
{
    using var host = new Peer(true, 11);
    ShinGetterTrialCombatSync.BeforeChoice(host.Sync, host.Players[0]);
    ShinGetterTrialCombatSync.Request(host.Sync, host.Sync.Models[1]);
    ShinGetterTrialCombatSync.AfterChoice(host.Sync, host.Players[0]);
    ShinGetterTrialCombatSync.AfterChoice(host.Sync, host.Players[1]);
    Assert(host.Net.Sent.Count == 0 && Entries(host) == 0, "forged owner was accepted");
});

Check("entry failure never reopens a broadcast decision", () =>
{
    using var host = new Peer(true, 11);
    ShinGetterEventInvasionService.FailEntry = true;
    try { host.Choose(11); throw new Exception("expected entry error"); }
    catch (InvalidOperationException) { }
    ShinGetterEventInvasionService.FailEntry = false;
    host.Choose(22);
    Assert(host.Net.Sent.Count == 1 && Entries(host) == 1 && Pending(host).Length == 0, "entry was retried under another winner");
});

Check("leave/begin/disconnect/dispose clear pending and release handlers", () =>
{
    using var peer = new Peer(false, 22);
    var old = peer.Commit(11);
    peer.Deliver(old);
    ShinGetterTrialCombatSync.EndEvent(peer.Sync);
    peer.Deliver(old);
    Assert(Pending(peer).Length == 0 && Entries(peer) == 1, "closed event accepted old commit");
    peer.State.RunLocation = new RunLocation(2, 5, 7);
    ShinGetterTrialCombatSync.BeginEvent(peer.Sync, false);
    peer.Deliver(old);
    Assert(Entries(peer) == 1, "old location leaked to new scope");
    peer.Deliver(peer.Commit(22));
    Assert(Entries(peer) == 2 && Pending(peer).Length == 1, "new scope did not reset once");
    peer.Net.Disconnect();
    Assert(peer.Buffer.HandlerCount == 0 && peer.Net.DisconnectedSubscribers == 0 && Pending(peer).Length == 0, "disconnect leaked state/handler");
    peer.Deliver(peer.Commit(22));
    Assert(Entries(peer) == 2, "disposed handler accepted commit");
});

Check("location change, prefinished or non-Trial events do not start a trial", () =>
{
    using var peer = new Peer(false, 22);
    var old = peer.Commit(11);
    peer.State.RunLocation = new RunLocation(2, 5, 7);
    peer.Deliver(old);
    ShinGetterTrialCombatSync.BeginEvent(peer.Sync, true);
    peer.Deliver(peer.Commit(11));
    peer.Sync.Models[1] = new EventModel(peer.Players[1]);
    ShinGetterTrialCombatSync.BeginEvent(peer.Sync, false);
    peer.Deliver(peer.Commit(11));
    Assert(Entries(peer) == 0, "invalid event lifecycle admitted commit");
});

Check("message fields round-trip through serialization API double", () =>
{
    using var peer = new Peer(false, 22);
    var expected = peer.Commit(ulong.MaxValue);
    var writer = new PacketWriter();
    expected.Serialize(writer);
    var actual = new ShinGetterTrialChallengeMessage();
    actual.Deserialize(new PacketReader(writer.Values));
    Assert(actual.OwnerId == expected.OwnerId && actual.RunSeed == expected.RunSeed && actual.Location == expected.Location, "message fields lost");
    Assert(!actual.ShouldBroadcast && actual.ShouldBuffer, "client relay/buffering flags wrong");
});
Console.WriteLine($"issue#283 CPU entry/controller regression: {passed} PASS; native transport/Harmony/history/reward cleanup/gameplay NOT_RUN");

sealed class FakeNet(bool host) : INetClientGameService
{
    private Action<NetErrorInfo>? _disconnected;
    public NetGameType Type => host ? NetGameType.Host : NetGameType.Client;
    public NetClient? NetClient { get; } = host ? null : new() { HostNetId = 11 };
    public List<ShinGetterTrialChallengeMessage> Sent { get; } = new();
    public int DisconnectedSubscribers => _disconnected?.GetInvocationList().Length ?? 0;
    public event Action<NetErrorInfo> Disconnected { add => _disconnected += value; remove => _disconnected -= value; }
    public void SendMessage<T>(T message) where T : INetMessage => Sent.Add((ShinGetterTrialChallengeMessage)(object)message);
    public void Disconnect() => _disconnected?.Invoke(default);
}

sealed class Peer : IPlayerCollection, IDisposable
{
    public const string StartKey = "SHIN_GETTER_EVENT_INVASION.TRIAL.pages.RYOMA.options.START_FIGHT";
    public RunState State { get; } = new() { RunLocation = new(1, 4, 6) };
    public IReadOnlyList<Player> Players { get; }
    public FakeNet Net { get; }
    public RunLocationTargetedMessageBuffer Buffer { get; } = new();
    public EventSynchronizer Sync { get; }
    public List<ulong> History => Sync.History;
    public NEventRoom Room { get; }
    public List<ulong> Played { get; } = new();
    public Peer(bool host, ulong localId, bool hostGetter = true, bool singleplayer = false)
    {
        Players = singleplayer
            ? new[] { new Player(11, State, hostGetter) }
            : new[] { new Player(11, State, hostGetter), new Player(22, State, true) };
        Net = new(host);
        Sync = new(this, localId);
        foreach (Player player in Players)
        {
            var model = new Trial(player);
            model.CurrentOptions.Add(new EventOption(StartKey, () => ShinGetterTrialCombatSync.Request(Sync, model)));
            Sync.Models.Add(model);
        }
        ProductionHook.Call("ShinGetterTrialSyncInitPatch", "Postfix", Sync, Buffer, Net, this, 123U);
        ProductionHook.Call("ShinGetterTrialSyncBeginPatch", "Postfix", Sync, false);
        Room = new NEventRoom(Sync);
    }
    public Player? GetPlayer(ulong id) => Players.FirstOrDefault(player => player.NetId == id);
    public void Choose(ulong id)
    {
        Sync.DeliverChoice(id, 0);
    }
    public void ClickStart()
    {
        RunManager.Instance.EventSynchronizer = Sync;
        Room.OptionButtonClicked(Sync.GetLocalEvent().CurrentOptions[0], 0);
    }
    public void DrawFirstHand()
    {
        foreach (Player player in Players)
            if (ShinGetterEventInvasionService.Pending.Remove(player))
                Played.Add(player.NetId);
    }
    public ShinGetterTrialChallengeMessage Commit(ulong owner) => new() { OwnerId = owner, RunSeed = 123, Location = State.RunLocation };
    public void Deliver(ShinGetterTrialChallengeMessage message, ulong sender = 11) => Buffer.Deliver(message, sender);
    public void Dispose() => ProductionHook.Call("ShinGetterTrialSyncDisposePatch", "Postfix", Sync);
}
