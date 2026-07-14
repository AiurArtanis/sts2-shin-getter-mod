using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 盖塔出击 | 技能 | 初始 | 1费 | 初始
/// 获得 1 气力，变形
/// </summary>
public sealed class SGC_GetterLaunch : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Ki>(1m),
    };

    public SGC_GetterLaunch()
        : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SGP_Ki>(
            choiceContext,
            base.Owner.Creature,
            DynamicVars["SGP_Ki"].BaseValue,
            base.Owner.Creature,
            this);

        // 变形到下一形态
        await Transform(choiceContext, base.Owner, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_Ki"].UpgradeValueBy(1m);
    }
}
