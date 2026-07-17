#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 不屈。下1次伤害延至下回合结算。
/// </summary>
public sealed class SGP_Indomitable : PowerModel
{
    private class Data
    {
        public decimal delayedDamage;
        public int delayedRound;
        public int chargesConsumed;
        public bool isResolving;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => System.Math.Max(0, Amount - GetInternalData<Data>().chargesConsumed);

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("Remaining", DisplayAmount);
            return description;
        }
    }

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m)
            return amount;

        var data = GetInternalData<Data>();
        if (data.isResolving || data.chargesConsumed >= Amount)
            return amount;

        data.delayedDamage += amount;
        data.delayedRound = CombatState?.RoundNumber ?? 0;
        data.chargesConsumed++;
        InvokeDisplayAmountChanged();
        return 0m;
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
            return;

        var data = GetInternalData<Data>();
        int currentRound = CombatState?.RoundNumber ?? 0;
        if (data.delayedDamage > 0 && data.delayedRound < currentRound)
        {
            decimal delayedDamage = data.delayedDamage;
            int chargesConsumed = data.chargesConsumed;
            data.delayedDamage = 0m;
            data.chargesConsumed = 0;
            data.isResolving = true;
            InvokeDisplayAmountChanged();

            await CreatureCmd.Damage(
                choiceContext,
                Owner,
                delayedDamage,
                ValueProp.Unpowered | ValueProp.Unblockable,
                null,
                null);

            data.isResolving = false;
            await PowerCmd.ModifyAmount(
                new ThrowingPlayerChoiceContext(),
                this,
                -chargesConsumed,
                null,
                null);
        }
    }
}
