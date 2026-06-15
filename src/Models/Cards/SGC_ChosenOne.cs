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
/// 天选之子 | 能力 | 罕见 | 1费 | 钢之魂流/变形流
/// 每变形 3 次，获得 2 气力
/// </summary>
public sealed class SGC_ChosenOne : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[] { HoverTipFactory.FromPower<SGP_ChosenOne>() });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_ChosenOne>(2m) };

    public SGC_ChosenOne()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<SGP_ChosenOne>(choiceContext, Owner.Creature, DynamicVars["SGP_ChosenOne"].BaseValue, Owner.Creature, this);
        power?.SetThreshold(3);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_ChosenOne"].UpgradeValueBy(1m);
    }
}
