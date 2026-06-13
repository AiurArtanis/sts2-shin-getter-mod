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
/// 觉醒之魂 | 能力 | 稀有 | 3费 | 钢之魂流/输出Key牌
/// 保留。【精神 3】每回合前 1 张攻击牌伤害翻倍
/// </summary>
public sealed class SGC_AwakenedSoul : ShinGetterCardBase
{
    public override int SpiritRequirement => 3;
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_AwakenedSoul()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 施加 Power，每回合前 1 张攻击牌伤害翻倍
    }

    protected override void OnUpgrade()
    {
        // 1→2 张
    }
}
