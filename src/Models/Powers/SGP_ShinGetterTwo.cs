#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 二号机形态。+1能量+1抽牌，格挡减半。
/// </summary>
public sealed class SGP_ShinGetterTwo : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
            return;

        await PlayerCmd.GainEnergy(1, player);
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        return player.Creature == Owner ? count + 1m : count;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 格挡减半
        if (target == base.Owner)
            return 0.5m;
        return 1m;
    }
}
