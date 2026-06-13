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

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 气力。回合开始获得N活力，使精神指令卡可以免费打出。
/// </summary>
public sealed class SGP_Ki : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
            return;

        int amount = base.Amount;
        if (amount <= 0) return;
        await PowerCmd.Apply<VigorPower>(
            new ThrowingPlayerChoiceContext(),
            player.Creature,
            amount,
            player.Creature,
            null);
    }
}
