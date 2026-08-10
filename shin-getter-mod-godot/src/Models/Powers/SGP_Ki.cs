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
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 气力。回合开始获得N活力，最终受到的伤害减少N，每次实际受伤后降低1点。
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

    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && amount > 0m && Amount > 0 && ShouldReduceDamage(props, cardSource))
            Flash();

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (!ShouldReduceDamage(props, cardSource))
            return 0m;

        if (target == Owner && amount > 0m && Amount > 0)
            return -Amount;

        return 0m;
    }

    private static bool ShouldReduceDamage(ValueProp props, CardModel? cardSource)
    {
        if (!props.HasFlag(ValueProp.Unpowered))
            return true;

        return cardSource?.Type == CardType.Status
            && !props.HasFlag(ValueProp.Unblockable);
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 || Amount <= 0)
            return;

        if (Owner.Player?.GetRelic<SGR_EmperorsFragment>() != null)
            return;

        Flash();
        await PowerCmd.Decrement(this);
    }
}
