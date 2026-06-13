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
/// 连锁反应 | 能力 | 罕见 | 2费 | 变形流/进化流
/// 失去活力时，获得 1 再生和 1 覆甲
/// </summary>
public sealed class SGC_ChainReaction : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_ChainReaction()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 施加 Power — 失去活力时，获得 1 再生和 1 覆甲
    }

    protected override void OnUpgrade()
    {
        // TODO: 1→2 再生, 1→2 覆甲
    }
}
