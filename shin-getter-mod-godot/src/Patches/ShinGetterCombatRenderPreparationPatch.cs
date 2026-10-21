#nullable enable
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Services;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(PreloadManager), nameof(PreloadManager.LoadRoomCombatAssets),
    new[] { typeof(EncounterModel), typeof(IRunState) })]
internal static class ShinGetterCombatRenderPreparationPatch
{
    [HarmonyPostfix]
    private static void Postfix(IRunState runState, ref Task __result)
    {
        __result = AfterOriginalAsync(__result, runState);
    }

    private static async Task AfterOriginalAsync(Task original, IRunState runState)
    {
        await original;
        await ShinGetterCombatRenderPreparation.PrepareAsync(runState);
    }
}
