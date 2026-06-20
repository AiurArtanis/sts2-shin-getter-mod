#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 真化形态。同时视作三个形态。（由GetCurrentForms逻辑处理）
/// </summary>
public sealed class SGP_ShinForm : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner != null && Amount > 0)
        {
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner, -2m, Owner, null);
            await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), Owner, -2m, Owner, null);
        }
    }

    public override async Task AfterRemoved(Creature owner)
    {
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), owner, 2m, owner, null);
        await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), owner, 2m, owner, null);
    }

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner.Player)
            return amount;

        return amount + 1m;
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        return player.Creature == Owner ? count + 1m : count;
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
