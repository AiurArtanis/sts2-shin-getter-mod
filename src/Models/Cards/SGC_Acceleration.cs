using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Acceleration : ShinGetterCardBase
{
    public override int SpiritRequirement => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1) };

    public SGC_Acceleration()
        : base(3, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 施加 Power：每回合获得 1 能量，额外抽 1 张
    }

    protected override void OnUpgrade()
    {
        // 1→2 能量, 1→2 张
    }
}
