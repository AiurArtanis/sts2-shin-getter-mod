#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class ShinGetterCombatLifecyclePatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        TaskHelper.RunSafely(ShinGetterExecutionMusicService.StopActiveAndRestore());
    }
}
