using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;

namespace MegaCrit.Sts2.Core.Logging { public enum LogLevel { VeryDebug } }
namespace MegaCrit.Sts2.Core.Multiplayer.Transport
{
    public enum NetTransferMode { Reliable }
    public sealed class NetClient { public ulong HostNetId { get; init; } }
}
namespace MegaCrit.Sts2.Core.Multiplayer.Serialization
{
    public interface INetMessage
    {
        bool ShouldBroadcast { get; }
        bool ShouldBuffer { get; }
        void Serialize(PacketWriter writer);
        void Deserialize(PacketReader reader);
    }
    public sealed class PacketWriter
    {
        public Queue<object> Values { get; } = new();
        public void WriteULong(ulong value) => Values.Enqueue(value);
        public void WriteUInt(uint value) => Values.Enqueue(value);
        public void Write<T>(T value) where T : struct => Values.Enqueue(value);
    }
    public sealed class PacketReader(Queue<object> values)
    {
        public ulong ReadULong() => (ulong)values.Dequeue();
        public uint ReadUInt() => (uint)values.Dequeue();
        public T Read<T>() where T : struct => (T)values.Dequeue();
    }
}
namespace MegaCrit.Sts2.Core.Multiplayer.Messages.Game
{
    public interface IRunLocationTargetedMessage { RunLocation Location { get; } }
}
namespace MegaCrit.Sts2.Core.Entities.Multiplayer { public readonly struct NetErrorInfo { } }
namespace MegaCrit.Sts2.Core.Multiplayer.Game
{
    public enum NetGameType { None, Singleplayer, Host, Client, Replay }
    public delegate void MessageHandlerDelegate<in T>(T message, ulong senderId) where T : INetMessage;
    public interface INetGameService
    {
        NetGameType Type { get; }
        event Action<MegaCrit.Sts2.Core.Entities.Multiplayer.NetErrorInfo> Disconnected;
        void SendMessage<T>(T message) where T : INetMessage;
    }
    public interface INetClientGameService : INetGameService
    {
        MegaCrit.Sts2.Core.Multiplayer.Transport.NetClient? NetClient { get; }
    }
    public sealed class RunLocationTargetedMessageBuffer
    {
        private readonly Dictionary<Type, object> _handlers = new();
        public int HandlerCount => _handlers.Count;
        public void RegisterMessageHandler<T>(MessageHandlerDelegate<T> handler) where T : INetMessage => _handlers.Add(typeof(T), handler);
        public void UnregisterMessageHandler<T>(MessageHandlerDelegate<T> handler) where T : INetMessage => _handlers.Remove(typeof(T));
        public void Deliver<T>(T message, ulong senderId) where T : INetMessage
        {
            if (_handlers.TryGetValue(typeof(T), out object? handler))
                ((MessageHandlerDelegate<T>)handler)(message, senderId);
        }
    }
    public sealed class EventSynchronizer(IPlayerCollection players, ulong localId)
    {
        public List<EventModel> Models { get; } = new();
        public IReadOnlyList<EventModel> Events => Models;
        public EventModel GetLocalEvent() => GetEventForPlayer(players.GetPlayer(localId)!);
        public EventModel GetEventForPlayer(Player player) => Models.Single(model => ReferenceEquals(model.Owner, player));
    }
}
namespace MegaCrit.Sts2.Core.Runs
{
    public readonly record struct RunLocation(int Act, int Floor, int Room);
    public sealed class RunState { public RunLocation RunLocation { get; set; } }
    public interface IPlayerCollection
    {
        IReadOnlyList<Player> Players { get; }
        Player? GetPlayer(ulong netId);
    }
}
namespace MegaCrit.Sts2.Core.Entities.Players
{
    public sealed class Player(ulong netId, RunState state, bool getter)
    {
        public ulong NetId { get; } = netId;
        public RunState RunState { get; } = state;
        public object Character { get; } = getter ? new ShinGetterMod.Models.Characters.ShinGetter() : new object();
        public ShinGetterMod.Models.Relics.SGR_GetterFurnace Furnace { get; } = new();
        public T? GetRelic<T>() where T : class => Furnace as T;
    }
}
namespace MegaCrit.Sts2.Core.Models
{
    public class EventModel(Player owner)
    {
        public Player Owner { get; } = owner;
        public List<EventOption> CurrentOptions { get; } = new();
    }
    public sealed record EventOption(string TextKey);
}
namespace MegaCrit.Sts2.Core.Models.Events { public sealed class Trial(Player owner) : EventModel(owner) { } }
namespace ShinGetterMod.Models.Characters { public sealed class ShinGetter { } }
namespace ShinGetterMod.Models.Relics
{
    public sealed class SGR_GetterFurnace { public bool EventInvasionEnabled { get; set; } = true; }
    public sealed class SGR_EmperorsFragment { public bool EventInvasionEnabled { get; set; } = true; }
}
namespace ShinGetterMod.Events
{
    // Only the native battle/setup boundary is substituted. The controller itself is linked unchanged.
    internal static class ShinGetterEventInvasionService
    {
        public static readonly Dictionary<Player, int> Pending = new();
        public static readonly List<(EventModel Event, Player Owner)> Entries = new();
        public static bool FailEntry;
        public static void StartCommittedTrialBattle(EventModel localEvent, Player owner)
        {
            Pending.Add(owner, 1);
            Entries.Add((localEvent, owner));
            if (FailEntry)
            {
                Pending.Remove(owner);
                throw new InvalidOperationException("simulated synchronous native entry failure");
            }
        }
        public static void ClearPendingTrialSetup(IEnumerable<Player> players)
        {
            foreach (Player player in players)
                Pending.Remove(player);
        }
    }
}
