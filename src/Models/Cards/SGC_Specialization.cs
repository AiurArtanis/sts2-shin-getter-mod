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
/// 专精 | 技能 | 稀有 | 0费 | 变形流
/// 将随机 1 张专属形态卡加入手牌。这张卡牌在本回合可以免费打出。消耗
/// 二号机：获得 1 能量，抽 1 张
/// </summary>
public sealed class SGC_Specialization : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_Specialization()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 将随机 1 张专属形态卡加入手牌，本回合可以免费打出
        // TODO: 二号机：获得 1 能量，抽 1 张
    }

    protected override void OnUpgrade()
    {
        // 去掉消耗
    }
}
