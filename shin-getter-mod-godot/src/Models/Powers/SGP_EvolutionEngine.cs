#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 进化引擎。触发进化后的下一回合获得1能量。
/// </summary>
public sealed class SGP_EvolutionEngine : PowerModel
{
    private class Data
    {
        public bool pendingEnergyGain;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override async Task AfterPlayerTurnStartEarly(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner || Owner.IsDead)
            return;

        var data = GetInternalData<Data>();
        if (!data.pendingEnergyGain)
            return;

        data.pendingEnergyGain = false;
        Flash();
        await PlayerCmd.GainEnergy(Amount, player);
    }

    /// <summary>
    /// 由 SGP_Evolution 触发时调用，标记下回合获得能量。
    /// </summary>
    public void MarkPendingEnergyGain()
    {
        var data = GetInternalData<Data>();
        data.pendingEnergyGain = true;
    }
}
