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

public sealed class SGC_Indomitable : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<VulnerablePower>() };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<VulnerablePower>(1m) };

    public SGC_Indomitable()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<VulnerablePower>(choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
        // TODO: 下 1 次伤害延至下回合
    }

    protected override void OnUpgrade()
    {
        // 1→2 次
    }
}
