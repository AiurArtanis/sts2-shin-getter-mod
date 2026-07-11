#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 天选之子。每次变形获得气力和格挡。
/// </summary>
public sealed class SGP_ChosenOne : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(0m, ValueProp.Unpowered) };

    public void AddBlockPerTransform(decimal block)
    {
        AssertMutable();
        DynamicVars.Block.BaseValue += block;
    }

    public async Task OnTransform(Creature owner)
    {
        var choiceContext = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<SGP_Ki>(choiceContext, owner, Amount, owner, null);
        await CreatureCmd.GainBlock(owner, DynamicVars.Block.BaseValue, ValueProp.Unpowered, null);
    }
}
