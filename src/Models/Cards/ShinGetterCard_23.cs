using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 战术撤退 | 技能 | 普通 | 1费 | 通用/过牌
/// 获得 7 格挡，抽 1 张牌
/// </summary>
public sealed class ShinGetterCard_23 : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(7m, ValueProp.Move) };

    public ShinGetterCard_23()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
		base.DynamicVars.Block.UpgradeValueBy(3m);
    }
}
