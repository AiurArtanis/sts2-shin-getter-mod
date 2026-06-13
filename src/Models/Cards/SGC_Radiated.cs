using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Radiated : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(5m, ValueProp.Move) };

    public SGC_Radiated()
        : base(0, CardType.Status, CardRarity.Status, TargetType.Self, false)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 获 1 进化 + 敌我 5 伤 + 1 辐射
    }

    protected override void OnUpgrade()
    {
    }
}
