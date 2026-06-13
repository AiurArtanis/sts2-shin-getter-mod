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
/// 进化引导 | 能力 | 普通 | 2费 | 进化
/// 每次获得 💥 时抽 1 张牌
/// </summary>
public sealed class ShinGetterCard_76 : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public ShinGetterCard_76()
        : base(2, CardType.Power, CardRarity.Common, TargetType.Self)
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
