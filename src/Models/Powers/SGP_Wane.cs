#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 衰退。受等同层数额外伤害，每次受未被格挡伤害层数+1，回合初层数减半。
/// </summary>
public sealed class SGP_Wane : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == base.Owner)
            return base.Amount;
        return 0m;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0)
            await PowerCmd.Apply<SGP_Wane>(choiceContext, Owner, 1m, dealer, cardSource);
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner))
            return;

        int retainedAmount = Amount / 2;
        int amountToRemove = Amount - retainedAmount;
        if (amountToRemove > 0)
            await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -amountToRemove, null, null);
    }
}
