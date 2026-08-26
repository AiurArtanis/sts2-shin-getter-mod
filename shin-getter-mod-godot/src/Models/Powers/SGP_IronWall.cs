#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 铁壁。下回合开始前，受到的所有伤害减去等同于层数的值。
/// </summary>
public sealed class SGP_IronWall : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && amount > 0m)
            Flash();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target == base.Owner)
            return -base.Amount;
        return 0m;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.TotalDamage > 0 && Owner.Player is { } player && ShinGetterCardBase.IsInForm(player, ShinGetterForm.Getter3))
        {
            Flash();
            await PowerCmd.Apply<PlatingPower>(choiceContext, Owner, 1m, Owner, null);
        }
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}
