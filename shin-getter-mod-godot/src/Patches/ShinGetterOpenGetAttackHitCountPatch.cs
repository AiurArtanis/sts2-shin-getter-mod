#nullable enable
using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Hooks;

namespace ShinGetterMod.Patches;

/// <summary>
/// Captures the hit count only after every combat hook listener has run. Player powers are
/// visited before enemy powers in multiplayer, so SGP_OpenGet cannot safely retain the value
/// it sees in its own ModifyAttackHitCount override (SGP_Grapple may still reduce it later).
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyAttackHitCount))]
internal static class ShinGetterOpenGetAttackHitCountPatch
{
    private sealed record FinalHitCount(int Value);

    private static readonly ConditionalWeakTable<AttackCommand, FinalHitCount> FinalHitCounts = new();

    [HarmonyPostfix]
    private static void Postfix(AttackCommand attackCommand, decimal __result)
    {
        FinalHitCounts.Remove(attackCommand);
        FinalHitCounts.Add(attackCommand, new FinalHitCount(Math.Max(0, (int)__result)));
    }

    internal static int GetFinalHitCount(AttackCommand attackCommand) =>
        FinalHitCounts.TryGetValue(attackCommand, out FinalHitCount? hitCount)
            ? hitCount.Value
            : 1;
}
