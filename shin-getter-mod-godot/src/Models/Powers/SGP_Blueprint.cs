#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

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

    public override async Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature == Owner && delta < 0m && Amount > 0)
        {
            Flash();
            await PowerCmd.Apply<SGP_Evolution>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                Amount,
                Owner,
                null);
        }
    }
}
