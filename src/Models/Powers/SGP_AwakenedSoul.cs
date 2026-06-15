#nullable enable
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
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
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.ModelSource is CardModel card &&
            card.Owner.Creature == Owner &&
            card.Type == CardType.Attack &&
            command.DamageProps.IsPoweredAttack())
        {
            int attacksStarted = CombatManager.Instance.History.CardPlaysStarted
                .Count(e => e.Actor == Owner && e.CardPlay.IsFirstInSeries && e.HappenedThisTurn(CombatState));
            if (attacksStarted <= Amount)
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
        int attacksStarted = CombatManager.Instance.History.CardPlaysStarted
            .Count(e => e.Actor == base.Owner && e.CardPlay.IsFirstInSeries && e.HappenedThisTurn(base.CombatState));
        if (attacksStarted > base.Amount) return 1m;
        return 2m;
    }
}
