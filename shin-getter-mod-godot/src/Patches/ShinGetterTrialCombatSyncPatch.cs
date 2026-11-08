#nullable enable
using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Events;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.OptionButtonClicked))]
internal static class ShinGetterTrialProceedRoutePatch
{
    private static bool Prefix(EventOption option, int index)
    {
        if (!option.IsProceed || option.TextKey != ShinGetterTrialCombatSync.StartFightOptionKey)
            return true;
        return !ShinGetterTrialCombatSync.TryRouteProceed(RunManager.Instance.EventSynchronizer, option, index);
    }
}

[HarmonyPatch(typeof(EventSynchronizer), MethodType.Constructor, new Type[]
{
    typeof(RunLocationTargetedMessageBuffer), typeof(INetGameService), typeof(IPlayerCollection),
    typeof(ulong), typeof(uint),
})]
internal static class ShinGetterTrialSyncInitPatch
{
    private static void Postfix(EventSynchronizer __instance, RunLocationTargetedMessageBuffer messageBuffer,
        INetGameService netService, IPlayerCollection playerCollection, uint seed) =>
        ShinGetterTrialCombatSync.Attach(__instance, messageBuffer, netService, playerCollection, seed);
}

[HarmonyPatch(typeof(EventSynchronizer), nameof(EventSynchronizer.BeginEvent))]
internal static class ShinGetterTrialSyncBeginPatch
{
    private static void Postfix(EventSynchronizer __instance, bool isPrefinished) =>
        ShinGetterTrialCombatSync.BeginEvent(__instance, isPrefinished);
}

[HarmonyPatch(typeof(EventSynchronizer), "ChooseOptionForEvent")]
internal static class ShinGetterTrialSyncChoicePatch
{
    private static void Prefix(EventSynchronizer __instance, Player player) =>
        ShinGetterTrialCombatSync.BeforeChoice(__instance, player);

    private static void Postfix(EventSynchronizer __instance, Player player) =>
        ShinGetterTrialCombatSync.AfterChoice(__instance, player);

    private static void Finalizer(EventSynchronizer __instance, Player player, Exception? __exception)
    {
        if (__exception != null)
            ShinGetterTrialCombatSync.ChoiceFailed(__instance, player);
    }
}

[HarmonyPatch(typeof(EventRoom), nameof(EventRoom.Exit))]
internal static class ShinGetterTrialSyncExitPatch
{
    private static void Prefix() =>
        ShinGetterTrialCombatSync.EndEvent(RunManager.Instance.EventSynchronizer);
}

[HarmonyPatch(typeof(EventSynchronizer), nameof(EventSynchronizer.Dispose))]
internal static class ShinGetterTrialSyncDisposePatch
{
    private static void Postfix(EventSynchronizer __instance) => ShinGetterTrialCombatSync.Detach(__instance);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.CleanUp))]
internal static class ShinGetterTrialSyncRunCleanupPatch
{
    private static void Prefix(RunManager __instance)
    {
        if (__instance.EventSynchronizer is { } synchronizer)
            ShinGetterTrialCombatSync.Detach(synchronizer);
    }
}
