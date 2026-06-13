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
/// 减负 | 技能 | 普通 | 2费 | 二号/防杀
/// 失去所有气力，每失去 1 点，获得 1 敏捷
/// 二号机：每失去 1 点，获得 1 再生
/// </summary>
public sealed class SGC_ShedLoad : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_ShedLoad()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 失去所有气力，每失去 1 点获得 1 敏捷(DexterityPower)
        // TODO: 二号机每失去 1 点获得 1 再生(RegenPower)
    }

    protected override void OnUpgrade()
    {
        // 2→1 费
    }
}
