#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Powers;

public sealed class SGP_Enable : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public void FlashOnPlay() => Flash();

    public override bool ShouldTakeExtraTurn(Player player) => player == Owner.Player && Amount > 0;

    public override async Task AfterTakingExtraTurn(Player player)
    {
        if (player == Owner.Player)
        {
            Flash();
            await PowerCmd.Decrement(this);
        }
    }
}
