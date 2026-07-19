#nullable enable
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

/// <summary>
/// 监听 VigorPower(活力) 的层数变化，通知 SGP_ChainReaction(连锁反应)。
/// patch PowerModel.SetAmount 是最底层的层数修改入口，无论何种方式修改层数都会经过此方法。
/// </summary>
[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
internal static class VigorPowerSetAmountPatch
{
    private static readonly FieldInfo? InternalDataField = AccessTools.Field(typeof(PowerModel), "_internalData");

    /// <summary>
    /// 记录修改前的层数，用于 Postfix 中计算变化量。
    /// </summary>
    [HarmonyPriority(Priority.VeryHigh)]
    private static void Prefix(PowerModel __instance, out int __state)
    {
        __state = __instance.Amount;
    }

    /// <summary>
    /// 层数变化后，如果是 VigorPower 层数减少了，触发连锁反应。
    /// </summary>
    private static async void Postfix(PowerModel __instance, int amount, bool silent, int __state)
    {
        // 仅处理 VigorPower，且层数减少
        if (__instance is not VigorPower) return;
        int delta = __state - amount; // 正数=减少量
        if (delta <= 0) return;

        ResetAttackTracking(__instance);

        var owner = __instance.Owner;
        if (owner == null) return;

        // 查找连锁反应 Power
        var chain = owner.GetPower<SGP_ChainReaction>();
        if (chain == null || chain.Amount <= 0) return;

        // One vigor-loss event triggers once, regardless of how many vigor stacks were lost.
        int gain = chain.Amount;
        chain.FlashTrigger();
        var ctx = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<RegenPower>(ctx, owner, gain, owner, null);
        await PowerCmd.Apply<PlatingPower>(ctx, owner, gain, owner, null);
    }

    private static void ResetAttackTracking(PowerModel vigor)
    {
        object? internalData = InternalDataField?.GetValue(vigor);
        if (internalData == null)
            return;

        // Vigor normally disappears after an attack. Getter Flash can grant new Vigor
        // before the old stacks are consumed, so the same instance survives and must
        // release the completed AttackCommand before the next card is played.
        AccessTools.Field(internalData.GetType(), "commandToModify")?.SetValue(internalData, null);
        AccessTools.Field(internalData.GetType(), "amountWhenAttackStarted")?.SetValue(internalData, 0);
    }
}
