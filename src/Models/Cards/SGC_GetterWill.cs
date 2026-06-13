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
/// 盖塔意志 | 技能 | 稀有 | 0费
/// 选择抽牌堆 1 张能力卡加入手牌
/// 一号机：加入手牌的能力卡可以免费打出
/// </summary>
public sealed class SGC_GetterWill : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_GetterWill()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 选择抽牌堆 1 张能力卡加入手牌
        // TODO: 一号机：加入手牌的能力卡可以免费打出
    }

    protected override void OnUpgrade()
    {
        // 增加固有
    }
}
