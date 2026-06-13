#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 擒拿。受到的后N次伤害为0。
/// 例如怪物6×3，擒拿2层→第2、3次基础伤害按0算。
/// </summary>
public sealed class SGP_Grapple : PowerModel
{
    private class Data
    {
        public int hitsNegated;
    }

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => System.Math.Max(0, base.Amount - GetInternalData<Data>().hitsNegated);

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 仅对持有擒拿的目标生效
        if (target != base.Owner) return 0m;

        var data = GetInternalData<Data>();
        if (data.hitsNegated < base.Amount)
        {
            data.hitsNegated++;
            InvokeDisplayAmountChanged();
            return -amount; // 将本次伤害减为0
        }
        return 0m;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(base.Owner))
        {
            GetInternalData<Data>().hitsNegated = 0;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }
}
