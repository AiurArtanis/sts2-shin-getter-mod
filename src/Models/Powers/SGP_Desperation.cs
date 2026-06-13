#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 绝境。战斗结束回复等同于层数的生命。
/// </summary>
public sealed class SGP_Desperation : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterCombatVictory(CombatRoom _)
    {
        if (!base.Owner.IsDead && base.Amount > 0)
        {
            await CreatureCmd.Heal(base.Owner, base.Amount);
        }
    }
}
