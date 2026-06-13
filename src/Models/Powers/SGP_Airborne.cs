#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 腾空。受伤减半，每回合结束层数-1，失去状态时获得1易伤。
/// </summary>
public sealed class SGP_Airborne : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == base.Owner)
            return 0.5m;
        return 1m;
    }

    public override async Task AfterEnergyReset(Player player)
    {
        // 每回-1层
        await PowerCmd.Decrement(this);
    }

    public override async Task AfterRemoved(Creature owner)
    {
        // 失去时获得1易伤
        await PowerCmd.Apply<VulnerablePower>(new ThrowingPlayerChoiceContext(), owner, 1m, owner, null);
    }
}
