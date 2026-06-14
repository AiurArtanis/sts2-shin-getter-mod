using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 再动 | 能力 | 稀有 | 3费
/// 保留。【精神 5】结束当前回合，获得 1 个额外的回合
/// </summary>
public sealed class SGC_Enable : ShinGetterCardBase
{
    public override int SpiritRequirement => IsUpgraded ? 4 : 5;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_Enable>(1m) };

    public SGC_Enable()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SGP_Enable>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override void OnUpgrade()
    {
    }
}
