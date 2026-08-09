#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_BeaconPrism : ShinGetterRelicBase
{
    private int _potionsUsedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task BeforeCombatStart()
    {
        _potionsUsedThisCombat = 0;
        return Task.CompletedTask;
    }

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        if (potion.Owner != Owner || Owner.Creature.CombatState == null)
            return;

        _potionsUsedThisCombat++;
        Flash();
        var context = new ThrowingPlayerChoiceContext();
        await CreatureCmd.Damage(
            context,
            Owner.Creature,
            _potionsUsedThisCombat,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        if (!Owner.Creature.IsDead)
        {
            await PowerCmd.Apply<SGP_Ki>(
                context,
                Owner.Creature,
                _potionsUsedThisCombat,
                Owner.Creature,
                null);
        }
    }
}
