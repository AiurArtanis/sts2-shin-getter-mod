#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 进化引擎。进化层数每次增减时变形一次。
/// </summary>
public sealed class SGP_EvolutionEngine : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power is not SGP_Evolution
            || power.Owner != Owner
            || amount == 0m
            || Owner.IsDead
            || Owner.Player is not { } player)
        {
            return;
        }

        Flash();
        await ShinGetterCardBase.Transform(choiceContext, player, cardSource);
    }
}
