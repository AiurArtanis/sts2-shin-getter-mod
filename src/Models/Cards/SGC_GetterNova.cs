using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 盖塔新星 | 技能 | 稀有 | 3费 | 进化流
/// 获 15 活力，给予全体敌人 2 辐射
/// </summary>
public sealed class SGC_GetterNova : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<VigorPower>(15m),
        new PowerVar<SGP_Radiation>(2m),
    };

    public SGC_GetterNova()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShinGetterCombatVfx.PlayGetterNova(Owner.Creature, CombatState.GetOpponentsOf(Owner.Creature));
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, DynamicVars["VigorPower"].BaseValue, Owner.Creature, this);
        foreach (var creature in CombatState.Creatures.Where(creature => creature.IsAlive))
            await PowerCmd.Apply<SGP_Radiation>(choiceContext, creature, DynamicVars["SGP_Radiation"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["VigorPower"].UpgradeValueBy(5m);
        DynamicVars["SGP_Radiation"].UpgradeValueBy(1m);
    }
}
