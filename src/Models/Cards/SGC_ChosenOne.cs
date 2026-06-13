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
/// 天选之子 | 能力 | 罕见 | 1费 | 钢之魂流/变形流
/// 每变形 3 次，获得 1 气力
/// </summary>
public sealed class SGC_ChosenOne : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_ChosenOne()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 施加 Power — 每变形 3 次，获得 1 气力
    }

    protected override void OnUpgrade()
    {
        // TODO: 3→2 次
    }
}
