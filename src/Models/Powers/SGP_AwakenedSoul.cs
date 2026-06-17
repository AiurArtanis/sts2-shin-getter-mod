#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 觉醒之魂。每回合前N张攻击牌伤害翻倍。
/// </summary>
public sealed class SGP_AwakenedSoul : PowerModel
{
    private class Data
    {
        public int remaining = -1;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => Remaining;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        ResetCounter();
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner))
            ResetCounter();

        return Task.CompletedTask;
    }

    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.ModelSource is CardModel card &&
            card.Owner.Creature == Owner &&
            card.Type == CardType.Attack &&
            command.DamageProps.IsPoweredAttack() &&
            Remaining > 0)
        {
            Flash();
        }
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (cardSource == null) return 1m;
        if (cardSource.Owner.Creature != base.Owner) return 1m;
        if (cardSource.Type != CardType.Attack) return 1m;
        if (!props.IsPoweredAttack()) return 1m;
        if (Remaining <= 0) return 1m;
        return 2m;
    }

    public override Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        if (command.ModelSource is CardModel card &&
            card.Owner.Creature == Owner &&
            card.Type == CardType.Attack &&
            command.DamageProps.IsPoweredAttack() &&
            Remaining > 0)
        {
            GetInternalData<Data>().remaining--;
            InvokeDisplayAmountChanged();
        }

        return Task.CompletedTask;
    }

    private int Remaining
    {
        get
        {
            int remaining = GetInternalData<Data>().remaining;
            return remaining < 0 ? Amount : remaining;
        }
    }

    private void ResetCounter()
    {
        GetInternalData<Data>().remaining = Amount;
        InvokeDisplayAmountChanged();
    }
}
