#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 战士奖章。回合开始每1气力获再生+覆甲。
/// </summary>
public sealed class SGP_WarriorMedal : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
            return;

        int ki = player.Creature.GetPower<SGP_Ki>()?.Amount ?? 0;
        if (ki <= 0 || base.Amount <= 0) return;
        int gain = ki * base.Amount;
        Flash();
        var ctx = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<RegenPower>(ctx, player.Creature, gain, player.Creature, null);
        await PowerCmd.Apply<PlatingPower>(ctx, player.Creature, gain, player.Creature, null);
    }
}
