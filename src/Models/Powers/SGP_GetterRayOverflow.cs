#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 盖塔线爆发。名字中有"盖塔"的卡牌费用减1。
/// </summary>
public sealed class SGP_GetterRayOverflow : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (card.GetType().Name.StartsWith("SGC_Getter", System.StringComparison.Ordinal))
        {
            modifiedCost = System.Math.Max(0m, originalCost - Amount);
            return true;
        }
        modifiedCost = originalCost;
        return false;
    }
}
