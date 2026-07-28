#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CombatRoom), "StartCombat", new[] { typeof(IRunState) })]
internal static class ShinGetterEncounterMusicPatch
{
    [HarmonyPrefix]
    private static void Prefix(CombatRoom __instance, IRunState? runState)
    {
        ShinGetterEncounterMusicService.TryStart(__instance, runState);
    }
}
