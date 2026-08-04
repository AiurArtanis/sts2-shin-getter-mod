using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 天选之子 | 能力 | 罕见 | 2费 | 钢之魂流/变形流
/// 每次变形获得 1 气力和 4 格挡
/// </summary>
public sealed class SGC_ChosenOne : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[] { HoverTipFactory.FromPower<SGP_ChosenOne>() });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_ChosenOne>(1m),
        new BlockVar(4m, ValueProp.Unpowered),
    };

    public SGC_ChosenOne()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<SGP_ChosenOne>(choiceContext, Owner.Creature, DynamicVars["SGP_ChosenOne"].BaseValue, Owner.Creature, this);
        power?.AddBlockPerTransform(DynamicVars.Block.BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
