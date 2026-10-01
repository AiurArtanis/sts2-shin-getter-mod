#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 斗志。被攻击前先对敌人造成等同于层数的伤害。
/// </summary>
public sealed class SGP_FightingSpirit : PowerModel
{
    // Private source tag, not Unpowered: Strength and normal damage modifiers still apply.
    internal const ValueProp CounterDamage = (ValueProp)(1 << 30);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && dealer != null && props.IsPoweredAttack() && Amount > 0)
        {
            Flash();
            await CreatureCmd.Damage(
                choiceContext,
                dealer,
                Amount,
                ValueProp.Move | ValueProp.SkipHurtAnim | CounterDamage,
                Owner,
                null);
        }
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && dealer?.IsDead == true && props.IsPoweredAttack())
            return 0m;

        return amount;
    }
}

[HarmonyPatch(typeof(VigorPower), nameof(VigorPower.ModifyDamageAdditive))]
internal static class FightingSpiritVigorDamagePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        VigorPower __instance,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        ref decimal __result)
    {
        // Only this tagged counter loses Vigor's additive contribution; no stacks are changed.
        if (__instance.Owner == dealer && cardSource == null
            && props.HasFlag(SGP_FightingSpirit.CounterDamage))
        {
            __result = 0m;
        }
    }
}
