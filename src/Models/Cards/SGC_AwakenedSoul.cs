using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 觉醒之魂 | 能力 | 稀有 | 3费 | 钢之魂流/输出Key牌
/// 保留。【精神 4】回合开始获得 6 活力，升级后 9
/// </summary>
public sealed class SGC_AwakenedSoul : ShinGetterCardBase
{
    public override int SpiritRequirement => 4;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_AwakenedSoul>(6m) };

    public SGC_AwakenedSoul()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShinGetterCombatVfx.PlayNewtypeFlash(Owner.Creature);
        await PowerCmd.Apply<SGP_AwakenedSoul>(choiceContext, Owner.Creature, DynamicVars["SGP_AwakenedSoul"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_AwakenedSoul"].UpgradeValueBy(3m);
    }
}
