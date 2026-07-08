#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_AlloyPlate : ShinGetterRelicBase
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override decimal ModifyHpLostAfterOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner.Creature || amount <= 0m)
            return amount;
        if (target.CombatState?.CurrentSide != CombatSide.Player)
            return amount;

        return amount * 0.5m;
    }

    public override Task AfterModifyingHpLostAfterOsty()
    {
        Flash();
        return Task.CompletedTask;
    }
}
