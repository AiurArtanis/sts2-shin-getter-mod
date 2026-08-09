using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Radiated : ShinGetterCardBase
{
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_Evolution>(),
        HoverTipFactory.FromPower<SGP_Radiation>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(5m, ValueProp.Unpowered),
        new PowerVar<SGP_Evolution>(1m),
        new PowerVar<SGP_Radiation>(1m),
    };

    public SGC_Radiated()
        : base(0, CardType.Status, CardRarity.Status, TargetType.Self, false)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SGP_Evolution>(
            choiceContext,
            Owner.Creature,
            DynamicVars["SGP_Evolution"].BaseValue,
            Owner.Creature,
            this);

        var self = Owner.Creature;
        var creatures = CombatState.Creatures.Where(creature => creature.IsAlive).ToList();
        var otherTargets = creatures.Where(creature => creature != self).ToList();
        if (otherTargets.Count > 0)
        {
            await CreatureCmd.Damage(
                choiceContext,
                otherTargets,
                DynamicVars.Damage.BaseValue,
                DynamicVars.Damage.Props,
                self,
                this,
                cardPlay);
        }

        if (self.IsAlive)
        {
            await CreatureCmd.Damage(
                choiceContext,
                self,
                DynamicVars.Damage.BaseValue,
                DynamicVars.Damage.Props,
                null,
                this,
                cardPlay);
        }

        foreach (var creature in creatures.Where(creature => creature.IsAlive))
        {
            await PowerCmd.Apply<SGP_Radiation>(
                choiceContext,
                creature,
                DynamicVars["SGP_Radiation"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
