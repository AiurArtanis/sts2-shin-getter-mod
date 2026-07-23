#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Nodes.Combat;
using System;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class ShinGetterCreatureAnimationPatch
{
    private static void Prefix(NCreature __instance, string trigger)
    {
        if (__instance.Entity?.Player?.Character is not ShinGetter)
            return;

        if (!IsShinGetterActionTrigger(trigger))
            return;

        NShinGetterStaticVisuals.TryPlayGetterActionAnimation(__instance, trigger);
    }

    private static bool IsShinGetterActionTrigger(string trigger) =>
        trigger is "Attack" or "HeavyAttack" or "Cast" or "Dash" or "Block" or "Hit" or "Dead";
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class ShinGetterCreatureDeathAnimationPatch
{
    [HarmonyPrefix]
    private static void Prefix(NCreature __instance, out float __state)
    {
        __state = 0f;
        if (__instance.Entity?.Player?.Character is not ShinGetter
            || __instance.DeathAnimationTask is { IsCompleted: false })
        {
            return;
        }

        __state = NShinGetterStaticVisuals.PlayGetterDeathAnimation(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(ref float __result, float __state)
    {
        __result = Math.Max(__result, __state);
    }
}
