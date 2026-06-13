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
/// 铁壁 | 技能 | 稀有 | 2费 | 钢之魂流
/// 保留。【精神 2】下回合开始前，受到的所有伤害减 5。消耗
/// 三号机：受到伤害获得 1 覆甲
/// </summary>
public sealed class SGC_IronWall : ShinGetterCardBase
{
    public override int SpiritRequirement => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_IronWall()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 下回合开始前，受到的所有伤害减 5
        // TODO: 三号机：受到伤害获得 1 覆甲
    }

    protected override void OnUpgrade()
    {
        // 2→1 费
    }
}
