using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace HarmonyLib
{
    public enum MethodType { Constructor }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(params object[] args) { _ = args; }
    }
}

namespace TrialFixture
{
    public static class ProductionHook
    {
        public static object? Call(string patch, string method, params object?[] args)
        {
            var type = typeof(ShinGetterMod.Events.ShinGetterTrialCombatSync).Assembly.GetType("ShinGetterMod.Patches." + patch);
            var hook = type?.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            if (hook == null)
                throw new InvalidOperationException("Missing production hook: " + patch + "." + method);
            try { return hook.Invoke(null, args); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        public static bool RouteButton(EventOption option, int index)
        {
            // Before the production route exists, execute the unpatched native Proceed branch for RED.
            if (typeof(ShinGetterMod.Events.ShinGetterTrialCombatSync).Assembly.GetType(
                "ShinGetterMod.Patches.ShinGetterTrialProceedRoutePatch") == null)
                return true;
            return (bool)(Call("ShinGetterTrialProceedRoutePatch", "Prefix", option, index)
                ?? throw new InvalidOperationException("Button prefix did not return a branch decision."));
        }
    }
}

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
        public List<ulong> History { get; } = new();
        public List<(ulong Owner, int Index)> OutgoingChoices { get; } = new();
        public void BeginEvent() { }
        public void Dispose() { }
        public void ChooseLocalOption(int index)
        {
            DeliverChoice(localId, index);
            OutgoingChoices.Add((localId, index));
        }
        public void DeliverChoice(ulong senderId, int index)
        {
            Player player = players.GetPlayer(senderId) ?? throw new InvalidOperationException("unknown native sender");
            EventOption option = GetEventForPlayer(player).CurrentOptions[index];
            TrialFixture.ProductionHook.Call("ShinGetterTrialSyncChoicePatch", "Prefix", this, player);
            Exception? failure = null;
            try
            {
                option.Chosen().GetAwaiter().GetResult();
                if (option.ShouldSaveChoiceToHistory)
                    History.Add(player.NetId);
                TrialFixture.ProductionHook.Call("ShinGetterTrialSyncChoicePatch", "Postfix", this, player);
            }
            catch (Exception error) { failure = error; throw; }
            finally { TrialFixture.ProductionHook.Call("ShinGetterTrialSyncChoicePatch", "Finalizer", this, player, failure); }
        }
    }
}
namespace MegaCrit.Sts2.Core.Runs
{
    public readonly record struct RunLocation(int Act, int Floor, int Room);
    public sealed class RunState { public RunLocation RunLocation { get; set; } }
    public sealed class RunManager
    {
        public static RunManager Instance { get; set; } = new();
        private EventSynchronizer? _synchronizer;
        public EventSynchronizer EventSynchronizer
        {
            get => _synchronizer ?? throw new InvalidOperationException("No current run synchronizer");
            set => _synchronizer = value;
        }
        public void CleanUp() { }
    }
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
    public class EventModel(Player? owner)
    {
        public Player? Owner { get; set; } = owner;
        public List<EventOption> CurrentOptions { get; } = new();
    }
}
namespace MegaCrit.Sts2.Core.Models.Events { public sealed class Trial(Player? owner) : EventModel(owner) { } }
namespace MegaCrit.Sts2.Core.Events
{
    public sealed class EventOption(string textKey, Action? onChosen = null, bool isProceed = true)
    {
        public string TextKey { get; } = textKey;
        public bool IsProceed { get; } = isProceed;
        public bool IsLocked { get; init; }
        public bool WasChosen { get; private set; }
        public bool ShouldSaveChoiceToHistory { get; init; } = true;
        public Task Chosen()
        {
            if (!WasChosen)
            {
                WasChosen = true;
                onChosen?.Invoke();
            }
            return Task.CompletedTask;
        }
    }
}
namespace MegaCrit.Sts2.Core.Nodes.Rooms
{
    public sealed class NEventRoom(EventSynchronizer synchronizer)
    {
        public int NativeProceedCalls { get; private set; }
        public void OptionButtonClicked(EventOption option, int index)
        {
            if (!TrialFixture.ProductionHook.RouteButton(option, index))
                return;
            if (option.IsLocked)
                return;
            if (option.IsProceed)
            {
                NativeProceedCalls++;
                option.Chosen().GetAwaiter().GetResult();
                return;
            }
            synchronizer.ChooseLocalOption(index);
        }
    }
}
namespace MegaCrit.Sts2.Core.Rooms { public sealed class EventRoom { public Task Exit() => Task.CompletedTask; } }
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
