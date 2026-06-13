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
/// 干扰器 | 技能 | 罕见 | 2费 | 过渡
/// 虚无，获得 1 层分身，变形直到变成二号机
/// </summary>
public sealed class SGC_Jammer : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_Jammer()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 虚无
        // TODO: 获得 1 层分身
        // TODO: 变形直到变成二号机
    }

    protected override void OnUpgrade()
    {
        // TODO: 去掉虚无
    }
}
