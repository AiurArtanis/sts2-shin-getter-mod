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
/// 盖塔新星 | 技能 | 稀有 | 3费 | 进化流
/// 获 15 活力，给予全体敌人 2 辐射
/// </summary>
public sealed class SGC_GetterNova : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_GetterNova()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 获 15 活力(VigorPower)
        // TODO: 给予全体敌人 2 辐射(RadiationPower)
    }

    protected override void OnUpgrade()
    {
        // 15→20 活力, 2→3 辐射
    }
}
