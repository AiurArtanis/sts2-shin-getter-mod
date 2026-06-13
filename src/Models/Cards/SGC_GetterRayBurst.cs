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
/// 盖塔射线爆发 | 能力 | 稀有 | 3费 | 通用
/// 所有名带"盖塔"的卡牌费用减 1
/// </summary>
public sealed class SGC_GetterRayBurst : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_GetterRayBurst()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 施加 Power，所有名带"盖塔"的卡牌费用减 1
    }

    protected override void OnUpgrade()
    {
        // 3→2 费
    }
}
