using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class ShinGetterDrillMissilePatch
{
    private static void Prefix(CardModel __instance)
    {
        if (__instance is SGC_Annotations annotations) annotations.BeginPlaySequence();
    }
    private static void Postfix(CardModel __instance, ref Task __result)
    {
        if (__instance is SGC_DrillMissile missile) __result = Complete(__result, missile);
        else if (__instance is SGC_Annotations annotations) __result = CompleteAnnotations(__result, annotations);
    }
    private static async Task CompleteAnnotations(Task original, SGC_Annotations annotations)
    {
        try { await original; }
        finally { annotations.EndPlaySequence(); }
    }
    private static async Task Complete(Task original, SGC_DrillMissile missile)
    {
        await original;
        await missile.ReplacePlayedCombatInstance();
    }
}
