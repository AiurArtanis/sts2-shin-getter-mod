#nullable enable
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Services;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
internal static class ShinGetterCombatCardChangesPatch
{
    private static void Postfix(IRunState runState, ref Task __result) => __result = Restore(__result, runState);
    private static async Task Restore(Task original, IRunState runState)
    {
        try { await original; }
        finally { foreach (var player in runState.Players) ShinGetterCombatCardChanges.Restore(player); }
    }
}
