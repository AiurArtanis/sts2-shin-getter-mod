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
/// 战士奖章 | 能力 | 罕见 | 2费 | 钢之魂流/进化流
/// 回合开始时每有 1 气力，获得 1 再生，1 覆甲
/// </summary>
public sealed class SGC_WarriorMedal : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_WarriorMedal()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 施加 Power — 回合开始时每有 1 气力，获得 1 再生，1 覆甲
    }

    protected override void OnUpgrade()
    {
        // TODO: 增加固有
    }
}
