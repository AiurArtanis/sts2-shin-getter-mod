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
/// 超强气 | 能力 | 稀有 | 3费 | 钢之魂流
/// 获 10 活力，每回开始将 1 张「气势」加入手牌
/// </summary>
public sealed class SGC_SuperKi : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_SuperKi()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 获 10 活力(VigorPower)
        // TODO: 施加 Power：每回开始将 1 张「气势」(SGC_Ki)加入手牌
    }

    protected override void OnUpgrade()
    {
        // 10→15 活力
    }
}
