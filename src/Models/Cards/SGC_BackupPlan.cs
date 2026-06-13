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
/// 备用方案 | 技能 | 罕见 | 2费 | 二号/过牌/烧牌
/// 消耗任意张手牌，每消耗一种类型的卡牌抽 1 张牌
/// 二号机加成：获得 1 能量
/// </summary>
public sealed class SGC_BackupPlan : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_BackupPlan()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 消耗任意张手牌，每消耗一种类型的卡牌抽 1 张牌
        // TODO: 二号机加成：获得 1 能量
    }

    protected override void OnUpgrade()
    {
        // TODO: 2→1 费
    }
}
