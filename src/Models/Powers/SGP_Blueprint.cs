#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 蓝图。失去生命时获得等同于层数的进化。
/// </summary>
public sealed class SGP_Blueprint : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 失去HP时获得进化（不改变伤害量）
        if (target == base.Owner && amount > 0 && base.Amount > 0)
        {
            var ctx = new ThrowingPlayerChoiceContext();
            _ = PowerCmd.Apply<SGP_Evolution>(ctx, target, base.Amount, target, null);
        }
        return amount;
    }
}
