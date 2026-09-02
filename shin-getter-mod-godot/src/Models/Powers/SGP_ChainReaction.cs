#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 连锁反应。失去活力时获得再生+覆甲。
/// 通过可等待的 Power 变化 Hook 监听内置 VigorPower 的层数变化。
/// </summary>
public sealed class SGP_ChainReaction : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not VigorPower
            || power.Owner != Owner
            || amount >= 0m
            || Owner.IsDead)
        {
            return;
        }

        // One vigor-loss event triggers once, regardless of how many vigor stacks were lost.
        int gain = Amount;
        if (gain <= 0)
            return;

        Flash();
        await PowerCmd.Apply<RegenPower>(choiceContext, Owner, gain, Owner, null);
        await PowerCmd.Apply<PlatingPower>(choiceContext, Owner, gain, Owner, null);
    }
}
