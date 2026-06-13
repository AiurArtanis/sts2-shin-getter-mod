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
/// 超强气 | 能力 | 稀有 | 3费 | 活力
/// 获得 10 活力，每回合开始将 1 张「气势」加入手牌 **一号机**：活力衰减而非消失 **二号机**：抽 3 张牌 **三号机**：给全体敌人 1 层易伤
/// </summary>
public sealed class ShinGetterCard_70 : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public ShinGetterCard_70()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
		// TODO: Implement effect
    }

    protected override void OnUpgrade()
    {
		// TODO: Implement upgrade
    }
}
