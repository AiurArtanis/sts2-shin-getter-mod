using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace ShinGetterMod.Events;

// Only callback bodies are excluded. Native types, Harmony attributes and target resolution are real.
internal static class ShinGetterTrialCombatSync
{
    internal const string StartFightOptionKey = "SHIN_GETTER_EVENT_INVASION.TRIAL.pages.RYOMA.options.START_FIGHT";
    private static InvalidOperationException NotInvoked() => new("Metadata probe must not execute a product callback.");
    internal static bool TryRouteProceed(EventSynchronizer sync, EventOption option, int index) => throw NotInvoked();
    internal static void Attach(EventSynchronizer sync, RunLocationTargetedMessageBuffer buffer,
        INetGameService net, IPlayerCollection players, uint seed) => throw NotInvoked();
    internal static void BeginEvent(EventSynchronizer sync, bool prefinished) => throw NotInvoked();
    internal static void BeforeChoice(EventSynchronizer sync, Player player) => throw NotInvoked();
    internal static void AfterChoice(EventSynchronizer sync, Player player) => throw NotInvoked();
    internal static void ChoiceFailed(EventSynchronizer sync, Player player) => throw NotInvoked();
    internal static void EndEvent(EventSynchronizer sync) => throw NotInvoked();
    internal static void Detach(EventSynchronizer sync) => throw NotInvoked();
}
