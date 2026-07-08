using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 进化引擎 | 能力 | 稀有 | 2费 | 进化流Key牌
/// 获得 2 进化，进化后下一回合获得 1 能量
/// </summary>
public sealed class SGC_EvolutionEngine : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Evolution>(2m),
        new PowerVar<SGP_EvolutionEngine>(1m),
        new EnergyVar("EvolutionEngineEnergy", 1),
    };

    public SGC_EvolutionEngine()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SGP_Evolution>(choiceContext, Owner.Creature, DynamicVars["SGP_Evolution"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<SGP_EvolutionEngine>(choiceContext, Owner.Creature, DynamicVars["SGP_EvolutionEngine"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_Evolution"].UpgradeValueBy(1m);
        DynamicVars["SGP_EvolutionEngine"].UpgradeValueBy(1m);
        DynamicVars["EvolutionEngineEnergy"].UpgradeValueBy(1m);
    }
}
