#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 铁壁。下回合开始前，受到的所有伤害减去等同于层数的值。
/// </summary>
public sealed class SGP_IronWall : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == base.Owner)
            return -base.Amount;
        return 0m;
    }
}
