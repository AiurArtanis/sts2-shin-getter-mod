using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Events;

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
    Assert(ShinGetterEventInvasionService.Entries.All(entry => entry.Event.Owner.NetId == (host.Players.Contains(entry.Owner) ? 11UL : 22UL)), "foreign event used for entry");
}

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
    host.Choose(11);
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
Console.WriteLine($"issue#283 CPU controller regression: {passed} PASS; native transport/Harmony/history/reward cleanup/gameplay NOT_RUN");

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
    public List<ulong> History { get; } = new();
    public List<ulong> Played { get; } = new();
    public Peer(bool host, ulong localId, bool hostGetter = true)
    {
        Players = new[] { new Player(11, State, hostGetter), new Player(22, State, true) };
        Net = new(host);
        Sync = new(this, localId);
        foreach (Player player in Players)
        {
            var model = new Trial(player);
            model.CurrentOptions.Add(new EventOption(StartKey));
            Sync.Models.Add(model);
        }
        ShinGetterTrialCombatSync.Attach(Sync, Buffer, Net, this, 123);
        ShinGetterTrialCombatSync.BeginEvent(Sync, false);
    }
    public Player? GetPlayer(ulong id) => Players.FirstOrDefault(player => player.NetId == id);
    public void Choose(ulong id)
    {
        Player owner = GetPlayer(id)!;
        ShinGetterTrialCombatSync.BeforeChoice(Sync, owner);
        ShinGetterTrialCombatSync.Request(Sync, Sync.GetEventForPlayer(owner));
        if (Net.Type == NetGameType.Host && Net.Sent.Count == 0
            && ShinGetterEventInvasionService.Entries.Any(entry => Players.Contains(entry.Owner)))
            throw new Exception("entry before native history boundary");
        History.Add(id); // Models native SaveEventOptionToHistory, not a native-history acceptance proof.
        ShinGetterTrialCombatSync.AfterChoice(Sync, owner);
    }
    public void DrawFirstHand()
    {
        foreach (Player player in Players)
            if (ShinGetterEventInvasionService.Pending.Remove(player))
                Played.Add(player.NetId);
    }
    public ShinGetterTrialChallengeMessage Commit(ulong owner) => new() { OwnerId = owner, RunSeed = 123, Location = State.RunLocation };
    public void Deliver(ShinGetterTrialChallengeMessage message, ulong sender = 11) => Buffer.Deliver(message, sender);
    public void Dispose() => ShinGetterTrialCombatSync.Detach(Sync);
}
