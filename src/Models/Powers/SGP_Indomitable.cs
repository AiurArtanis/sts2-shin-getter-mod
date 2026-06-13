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
/// 不屈。下1次伤害延至下回合结算。
/// </summary>
public sealed class SGP_Indomitable : PowerModel
{
    private class Data
    {
        public decimal delayedDamage;
        public int delayedRound;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return amount;
        var data = GetInternalData<Data>();
        data.delayedDamage += amount;
        data.delayedRound = base.CombatState?.RoundNumber ?? 0;
        return 0m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(base.Owner) || combatState == null) return;
        var data = GetInternalData<Data>();
        if (data.delayedDamage > 0 && data.delayedRound < combatState.RoundNumber)
        {
            await DamageCmd.Attack(data.delayedDamage).FromCard(null).Targeting(base.Owner).Execute(new ThrowingPlayerChoiceContext());
            data.delayedDamage = 0;
        }
    }
}
