#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

internal static class ShinGetterMultiAttackIntentPatch
{
    internal static int GetAdjustedRepeats(MultiAttackIntent intent, Creature owner) =>
        GetAdjustedRepeats(owner, intent.Repeats);

    private static int GetAdjustedRepeats(Creature owner, int repeats)
    {
        if (repeats <= 0)
            return 0;

        var grapple = owner.GetPower<SGP_Grapple>();
        if (grapple == null)
            return repeats;

        return Math.Max(0, repeats - grapple.Amount);
    }
}

[HarmonyPatch(typeof(MultiAttackIntent), nameof(MultiAttackIntent.GetIntentLabel))]
internal static class ShinGetterMultiAttackIntentLabelPatch
{
    private static void Postfix(MultiAttackIntent __instance, Creature owner, LocString __result)
    {
        __result.Add("Repeat", ShinGetterMultiAttackIntentPatch.GetAdjustedRepeats(__instance, owner));
    }
}

[HarmonyPatch(typeof(MultiAttackIntent), nameof(MultiAttackIntent.GetTotalDamage))]
internal static class ShinGetterMultiAttackIntentTotalDamagePatch
{
    private static void Postfix(
        MultiAttackIntent __instance,
        IEnumerable<Creature> targets,
        Creature owner,
        ref int __result)
    {
        int repeats = __instance.Repeats;
        int adjustedRepeats = ShinGetterMultiAttackIntentPatch.GetAdjustedRepeats(__instance, owner);
        if (adjustedRepeats == repeats)
            return;

        __result = __instance.GetSingleDamage(targets, owner) * adjustedRepeats;
    }
}

[HarmonyPatch(typeof(AttackIntent), "GetIntentDescription")]
internal static class ShinGetterMultiAttackIntentDescriptionPatch
{
    private static void Postfix(AttackIntent __instance, Creature owner, LocString __result)
    {
        if (__instance is not MultiAttackIntent multiAttackIntent)
            return;

        __result.Add("Repeat", ShinGetterMultiAttackIntentPatch.GetAdjustedRepeats(multiAttackIntent, owner));
    }
}
