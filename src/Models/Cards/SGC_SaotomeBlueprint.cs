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
/// 早乙女蓝图 | 能力 | 罕见 | 1费 | 进化流
/// 失去生命时获得 1 进化
/// </summary>
public sealed class SGC_SaotomeBlueprint : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_SaotomeBlueprint()
        : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 施加 Power — 失去生命时获得 1 进化
    }

    protected override void OnUpgrade()
    {
        // TODO: 1→2 进化
    }
}
