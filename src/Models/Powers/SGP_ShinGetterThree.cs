#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 三号机形态。-2力-2敏，获得格挡时获得1覆甲。
/// </summary>
public sealed class SGP_ShinGetterThree : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (base.Owner != null && base.Amount > 0)
        {
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), base.Owner, -2m, base.Owner, null);
            await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), base.Owner, -2m, base.Owner, null);
        }
    }

    public override async Task AfterRemoved(Creature owner)
    {
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), owner, 2m, owner, null);
        await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), owner, 2m, owner, null);
    }

    public override async Task AfterBlockGained(
        Creature creature,
        decimal amount,
        ValueProp props,
        CardModel? cardSource)
    {
        if (creature != Owner || amount <= 0m)
            return;

        Flash();
        await PowerCmd.Apply<PlatingPower>(
            new ThrowingPlayerChoiceContext(),
            Owner,
            1m,
            Owner,
            cardSource);
    }
}
