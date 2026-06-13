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
/// 绝境 | 能力 | 稀有 | 1费 | 钢之魂
/// 降低生命至 1，战斗结束时回复等量生命。获得 1 张随机钢之魂卡。保留 消耗 **一号机**：获得 5 攻 **二号机**：获得 2 💥，抽 3 张牌 **三号
/// </summary>
public sealed class ShinGetterCard_66 : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public ShinGetterCard_66()
        : base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
		// TODO: Implement effect
    }

    protected override void OnUpgrade()
    {
		// TODO: Reduce energy cost
    }
}
