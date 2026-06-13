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
/// 三体同心 | 技能 | 罕见 | 1费 | 进化流
/// 本回合接下来打出的 2 张牌后变形
/// </summary>
public sealed class SGC_TripleUnity : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_TripleUnity()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 本回合接下来打出的 2 张牌后变形
    }

    protected override void OnUpgrade()
    {
        // TODO: 2→3 张
    }
}
