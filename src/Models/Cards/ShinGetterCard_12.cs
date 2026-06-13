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
/// 变形 | 技能 | 普通 | 1费 | 变形
/// 获得 4 格挡，变形成一号机/二号机/三号机（按顺序循环）
/// </summary>
public sealed class ShinGetterCard_12 : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(4m, ValueProp.Move) };

    public ShinGetterCard_12()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
		// TODO: Reduce energy cost
    }
}
