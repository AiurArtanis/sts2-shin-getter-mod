using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 不进化体 | 能力 | 稀有 | 1费
/// 封印敌我全体 1 回合
/// </summary>
public sealed class SGC_AntiEvolution : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_Seal>(1m) };

    public SGC_AntiEvolution()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        foreach (var creature in CombatState.Creatures.Where(creature => creature.IsAlive))
            await PowerCmd.Apply<SGP_Seal>(choiceContext, creature, DynamicVars["SGP_Seal"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_Seal"].UpgradeValueBy(1m);
    }
}
