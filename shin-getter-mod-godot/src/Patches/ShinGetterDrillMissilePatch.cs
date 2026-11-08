using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Services;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class ShinGetterDrillMissilePatch
{
    private static void Prefix(CardModel __instance)
    {
        if (__instance is SGC_Annotations annotations) annotations.BeginPlaySequence();
    }
    private static void Postfix(CardModel __instance, PlayerChoiceContext choiceContext, ref Task __result)
    {
        if (__instance is SGC_DrillMissile missile) __result = Complete(__result, missile);
        else if (__instance is SGC_Annotations annotations) __result = CompleteAnnotations(__result, annotations, choiceContext);
    }
    private static async Task CompleteAnnotations(Task original, SGC_Annotations annotations, PlayerChoiceContext context)
    {
        try { await original; }
        catch (ShinGetterAnnotationsCardChoice.RunAbortedException)
        {
            // Native OnPlayWrapper pushes the card but pops it only on its normal path.
            // Release exactly our interrupted card frame, preserving any outer hook model.
            if (ReferenceEquals(context.LastInvolvedModel, annotations)) context.PopModel(annotations);
            throw;
        }
        finally { annotations.EndPlaySequence(); }
    }
    private static async Task Complete(Task original, SGC_DrillMissile missile)
    {
        await original;
        await missile.ReplacePlayedCombatInstance();
    }
}
