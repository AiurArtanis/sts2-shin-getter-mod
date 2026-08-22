#nullable enable
using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

/// <summary>
/// Makes runtime avoidance use the same final, displayed per-hit damage as AttackIntent.
/// Hook.ModifyDamage(All) returns only after additive, multiplicative (including Weak), and
/// cap hooks (including Intangible/HardToKill) have all completed.
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class ShinGetterOpenGetFinalDamagePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        Creature? target,
        Creature? dealer,
        ValueProp props,
        ModifyDamageHookType modifyDamageHookType,
        ref decimal __result)
    {
        if (modifyDamageHookType != ModifyDamageHookType.All
            || ShinGetterOpenGetIntentPatch.IsCalculatingIntentDamage
            || target is null
            || target.GetPower<SGP_OpenGet>() is not { } openGet)
        {
            return;
        }

        int displayedDamagePerHit = GetDisplayedDamagePerHit(__result);
        if (openGet.TryAvoidFinalDamage(target, displayedDamagePerHit, props, dealer))
            __result = 0m;
    }

    internal static int GetDisplayedDamagePerHit(decimal finalDamage) =>
        Math.Max(0, (int)finalDamage);
}
