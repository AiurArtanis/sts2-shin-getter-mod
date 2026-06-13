#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 天选之子。每变形3次获得气力。
/// </summary>
public sealed class SGP_ChosenOne : PowerModel
{
    private class Data
    {
        public int transformCount;
    }

    private const int Threshold = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => System.Math.Max(0, Threshold - GetInternalData<Data>().transformCount % Threshold);

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    /// <summary>
    /// 每次变形时调用。达到阈值时获得气力。
    /// </summary>
    public async Task OnTransform(Creature owner)
    {
        var data = GetInternalData<Data>();
        data.transformCount++;
        InvokeDisplayAmountChanged();

        if (data.transformCount % Threshold == 0)
        {
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<SGP_Ki>(
                new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
                owner, base.Amount, owner, null);
        }
    }
}
