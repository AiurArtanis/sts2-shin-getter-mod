using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 三体同心 | 技能 | 罕见 | 1费 | 进化流
/// 接下来打出的 2 张牌，每张打出后变形
/// </summary>
public sealed class SGC_TripleUnity : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[] { HoverTipFactory.FromPower<SGP_TripleUnity>() });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_TripleUnity>(2m) };

    public SGC_TripleUnity()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<SGP_TripleUnity>(choiceContext, Owner.Creature, DynamicVars["SGP_TripleUnity"].BaseValue, Owner.Creature, this);
        power?.IgnoreNextTriggerFrom(this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_TripleUnity"].UpgradeValueBy(1m);
    }
}
