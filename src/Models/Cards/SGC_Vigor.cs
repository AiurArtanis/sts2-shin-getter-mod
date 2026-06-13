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
/// 气势 | 技能 | 普通 | 1费 | 钢之魂流
/// 保留。获得 1 气力，2 活力。消耗
/// </summary>
public sealed class SGC_Vigor : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_Vigor()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 获得 1 气力(FightingSpiritPower)
        // TODO: 获得 2 活力(VigorPower)
    }

    protected override void OnUpgrade()
    {
        // 1→0 费
    }
}
