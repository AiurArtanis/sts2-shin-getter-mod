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
/// 夺取未来 | 技能 | 罕见 | 1费 | 加费
/// 获 7 格挡，将 1 张手牌本回合耗费减 1
/// </summary>
public sealed class SGC_SeizeFuture : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(7m, ValueProp.Move) };

    public SGC_SeizeFuture()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
        // TODO: 将 1 张手牌本回合耗费减 1
    }

    protected override void OnUpgrade()
    {
        // TODO: 1→全部手牌
    }
}
