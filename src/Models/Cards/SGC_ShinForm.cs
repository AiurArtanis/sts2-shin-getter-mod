using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 真化形态 | 能力 | 先古 | 3费
/// 虚无。变形为真盖塔龙，同时视作 3 个形态
/// </summary>
public sealed class SGC_ShinForm : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_ShinForm()
        : base(3, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
        // TODO: IsEthereal = true;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = base.Owner.Creature;

        // 移除当前所有形态
        var one = creature.GetPower<SGP_ShinGetterOne>();
        var two = creature.GetPower<SGP_ShinGetterTwo>();
        var three = creature.GetPower<SGP_ShinGetterThree>();
        var shin = creature.GetPower<SGP_ShinForm>();

        if (one != null) await PowerCmd.Remove(one);
        if (two != null) await PowerCmd.Remove(two);
        if (three != null) await PowerCmd.Remove(three);
        if (shin != null) await PowerCmd.Remove(shin);

        // 变形为真化形态
        await PowerCmd.Apply<SGP_ShinForm>(choiceContext, creature, 1m, creature, this);
    }

    protected override void OnUpgrade()
    {
        // 3→2 费
    }
}
