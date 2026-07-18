#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 分身。本回合受到的多段攻击只有第一次造成伤害，非多段攻击伤害减半。
/// </summary>
public sealed class SGP_Shade : PowerModel
{
    private sealed class Data
    {
        public AttackCommand? ActiveAttack;
        public int HitCount;
        public int OwnerHitsReceived;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount)
    {
        if (attack.TargetSide == Owner.Side)
        {
            var data = GetInternalData<Data>();
            data.ActiveAttack = attack;
            data.HitCount = hitCount;
            data.OwnerHitsReceived = 0;
        }

        return hitCount;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || !props.IsPoweredAttack())
            return 1m;

        var data = GetInternalData<Data>();
        if (data.ActiveAttack?.Attacker == dealer && data.HitCount > 1)
        {
            data.OwnerHitsReceived++;
            return data.OwnerHitsReceived == 1 ? 1m : 0m;
        }

        return 0.5m;
    }

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        var data = GetInternalData<Data>();
        if (data.ActiveAttack == command)
        {
            data.ActiveAttack = null;
            data.HitCount = 0;
            data.OwnerHitsReceived = 0;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}
