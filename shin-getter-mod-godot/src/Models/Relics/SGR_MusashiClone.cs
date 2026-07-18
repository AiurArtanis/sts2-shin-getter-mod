using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_MusashiClone : ShinGetterRelicBase
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsUsedUp => TriggeredThisCombat;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new HealVar(30m) };

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool TriggeredThisCombat
    {
        get => _triggeredThisCombat;
        set
        {
            AssertMutable();
            _triggeredThisCombat = value;
            Status = _triggeredThisCombat ? RelicStatus.Disabled : RelicStatus.Normal;
        }
    }

    public override Task BeforeCombatStart()
    {
        TriggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (creature != Owner.Creature
            || TriggeredThisCombat
            || !ShinGetterCardBase.IsInForm(Owner, ShinGetterForm.Getter3))
            return true;

        return false;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        Flash();
        TriggeredThisCombat = true;
        decimal amount = Math.Max(1m, creature.MaxHp * (DynamicVars.Heal.BaseValue / 100m));
        await CreatureCmd.Heal(creature, amount);
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        TriggeredThisCombat = false;
        return Task.CompletedTask;
    }
}
