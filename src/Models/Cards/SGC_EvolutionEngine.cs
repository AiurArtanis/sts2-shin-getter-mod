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
/// 进化引擎 | 能力 | 稀有 | 2费 | 进化流Key牌
/// 获得 2 进化，进化后下一回合获得 1 能量
/// </summary>
public sealed class SGC_EvolutionEngine : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_EvolutionEngine()
        : base(2, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 获得 2 进化(EvolutionPower)
        // TODO: 施加 Power：进化后下一回合获得 1 能量
    }

    protected override void OnUpgrade()
    {
        // 2→3 进化, 1→2 能量
    }
}
