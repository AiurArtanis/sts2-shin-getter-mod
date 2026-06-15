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
/// 战士奖章 | 能力 | 罕见 | 2费 | 钢之魂流/进化流
/// 回合开始时每有 1 气力，获得 1 再生，1 覆甲
/// </summary>
public sealed class SGC_WarriorMedal : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_WarriorMedal>(),
        HoverTipFactory.FromPower<SGP_Ki>(),
        HoverTipFactory.FromPower<RegenPower>(),
        HoverTipFactory.FromPower<PlatingPower>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_WarriorMedal>(1m) };

    public SGC_WarriorMedal()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SGP_WarriorMedal>(choiceContext, Owner.Creature, DynamicVars["SGP_WarriorMedal"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Innate);
    }
}
