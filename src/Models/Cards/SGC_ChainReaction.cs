using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 连锁反应 | 能力 | 稀有 | 2费 | 变形流/进化流
/// 失去活力时，获得 1 再生和 1 覆甲
/// </summary>
public sealed class SGC_ChainReaction : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_ChainReaction>(),
        HoverTipFactory.FromPower<VigorPower>(),
        HoverTipFactory.FromPower<RegenPower>(),
        HoverTipFactory.FromPower<PlatingPower>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_ChainReaction>(1m) };

    public SGC_ChainReaction()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SGP_ChainReaction>(choiceContext, Owner.Creature, DynamicVars["SGP_ChainReaction"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_ChainReaction"].UpgradeValueBy(1m);
    }
}
