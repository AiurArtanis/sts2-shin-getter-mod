using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Grapple : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<WeakPower>() };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<WeakPower>(1m) };

    public SGC_Grapple()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, 1m, base.Owner.Creature, this);
        // TODO: 使该敌人本回合多段攻击次数减 2 次
        // TODO: 三号机降低敌人 2 力量
    }

    protected override void OnUpgrade()
    {
        // 1→2 虚弱, 2→3 次
    }
}
