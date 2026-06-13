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
/// 分身 | 技能 | 罕见 | 2费 | 变形
/// 本回合受到伤害减半，变形为二号机 **二号机**：获得 5 护盾
/// </summary>
public sealed class ShinGetterCard_08 : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public ShinGetterCard_08()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
		// TODO: Implement effect
    }

    protected override void OnUpgrade()
    {
		// TODO: Implement upgrade
    }
}
