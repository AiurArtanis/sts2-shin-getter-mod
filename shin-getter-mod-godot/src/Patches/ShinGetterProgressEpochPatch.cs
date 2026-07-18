#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Saves.Managers;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(ProgressSaveManager), "CheckFifteenElitesDefeatedEpoch")]
internal static class ShinGetterEliteEpochPatch
{
    private static bool Prefix(Player localPlayer) => !IsShinGetter(localPlayer);

    internal static bool IsShinGetter(Player localPlayer) => localPlayer.Character is ShinGetter;
}

[HarmonyPatch(typeof(ProgressSaveManager), "CheckFifteenBossesDefeatedEpoch")]
internal static class ShinGetterBossEpochPatch
{
    private static bool Prefix(Player localPlayer) => !ShinGetterEliteEpochPatch.IsShinGetter(localPlayer);
}

[HarmonyPatch(typeof(ProgressSaveManager), "ObtainCharUnlockEpoch")]
internal static class ShinGetterCharUnlockEpochPatch
{
    private static bool Prefix(Player localPlayer) => !ShinGetterEliteEpochPatch.IsShinGetter(localPlayer);
}
