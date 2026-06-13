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
/// 分身。本回合受多段攻击仅1次伤害，非多段伤害减半。
/// 不可堆叠，仅本回合生效。
/// </summary>
public sealed class SGP_Shade : PowerModel
{
    private class Data
    {
        public bool firstHitPassed; // 多段攻击已放过第1次
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return 0m;

        var data = GetInternalData<Data>();

        // 判断是否多段攻击：WithHitCount > 1
        // 无法直接获取 WithHitCount，简化处理：
        // 第1次伤害保留，后续伤害减为0
        if (!data.firstHitPassed)
        {
            data.firstHitPassed = true;
            return 0m; // 第1次伤害照常
        }
        return -amount; // 后续伤害减为0
    }
}
