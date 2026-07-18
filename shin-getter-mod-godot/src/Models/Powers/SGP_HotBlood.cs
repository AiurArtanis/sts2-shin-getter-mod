#nullable enable
using System.Threading.Tasks;
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
/// 热血。打出的下1张攻击卡伤害翻倍。
/// </summary>
public sealed class SGP_HotBlood : PowerModel
{
    private class Data
    {
        public AttackCommand? commandToModify;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.ModelSource is not CardModel card) return Task.CompletedTask;
        if (card.Owner.Creature != base.Owner) return Task.CompletedTask;
        if (card.Type != CardType.Attack) return Task.CompletedTask;
        if (!command.DamageProps.IsPoweredAttack()) return Task.CompletedTask;
        var data = GetInternalData<Data>();
        if (data.commandToModify != null) return Task.CompletedTask;
        data.commandToModify = command;
        Flash();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (cardSource == null) return 1m;
        if (cardSource.Owner.Creature != base.Owner) return 1m;
        if (!props.IsPoweredAttack()) return 1m;
        var data = GetInternalData<Data>();
        if (data.commandToModify == null && Amount > 0 && cardSource.Type == CardType.Attack)
            return 2m;
        if (data.commandToModify != null && cardSource == data.commandToModify.ModelSource)
            return 2m;
        return 1m;
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        var data = GetInternalData<Data>();
        if (command == data.commandToModify)
        {
            data.commandToModify = null;
            await PowerCmd.Decrement(this);
        }
    }

    public async Task ConsumeForCardDamage(PlayerChoiceContext choiceContext, CardModel card, ValueProp props)
    {
        if (Amount <= 0) return;
        if (card.Owner.Creature != base.Owner) return;
        if (card.Type != CardType.Attack) return;
        if (!props.IsPoweredAttack()) return;

        Flash();
        await PowerCmd.Decrement(this);
    }
}
