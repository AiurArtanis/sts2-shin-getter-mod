using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 再动 | 能力 | 稀有 | 3费
/// 保留。【精神 5】结束当前回合，获得 1 个额外的回合
/// </summary>
public sealed class SGC_Enable : ShinGetterCardBase
{
    public override int SpiritRequirement => 5;
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_Enable()
        : base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 结束当前回合，获得 1 个额外的回合
    }

    protected override void OnUpgrade()
    {
        // 5→4 精神
    }
}
