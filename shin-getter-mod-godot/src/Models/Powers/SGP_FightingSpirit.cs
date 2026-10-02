#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
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
    private sealed class Data
    {
        public bool IsCountering;
        public Creature? CanceledAttacker;
        public ValueProp CanceledProps;
        public CardModel? CanceledCardSource;
    }

    // Private source tag, not Unpowered: Strength and normal damage modifiers still apply.
    internal const ValueProp CounterDamage = (ValueProp)(1 << 30);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == null || dealer.Side == Owner.Side
            || dealer.IsDead || Owner.IsDead || !props.IsPoweredAttack() || Amount <= 0
            || props.HasFlag(CounterDamage))
            return;

        Data data = GetInternalData<Data>();
        if (data.IsCountering)
            return;

        data.CanceledAttacker = null;
        data.CanceledCardSource = null;
        data.IsCountering = true;
        try
        {
            Flash();
            await CreatureCmd.Damage(
                choiceContext,
                dealer,
                Amount,
                ValueProp.Move | ValueProp.SkipHurtAnim | CounterDamage,
                Owner,
                null);

            if (dealer.IsDead)
            {
                data.CanceledAttacker = dealer;
                data.CanceledProps = props;
                data.CanceledCardSource = cardSource;
            }
        }
        finally
        {
            data.IsCountering = false;
        }
    }

    internal bool CancelsIncomingDamage(Creature target, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        Data data = GetInternalData<Data>();
        return target == Owner && dealer != null && dealer.IsDead
            && props.IsPoweredAttack() && !props.HasFlag(CounterDamage)
            && data.CanceledAttacker == dealer && data.CanceledProps == props
            && data.CanceledCardSource == cardSource;
    }

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return CancelsIncomingDamage(target, props, dealer, cardSource) ? 0m : amount;
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        return CancelsIncomingDamage(target, props, dealer, cardSource) ? 0m : amount;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class FightingSpiritIncomingHpLossPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void CancelCounteredHit(
        Creature target,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        ref decimal __result)
    {
        // CreatureCmd does not recheck a dealer killed inside BeforeDamageReceived.
        // Only this power's recorded counter kill overrides the final HP-loss result.
        if (target.GetPower<SGP_FightingSpirit>()?.CancelsIncomingDamage(target, props, dealer, cardSource) == true)
            __result = 0m;
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
