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
/// 斗志 | 能力 | 罕见 | 2费 | 防杀终端
/// 保留。【精神 2】被攻击前先对敌人造成 6 伤害
/// </summary>
public sealed class SGC_FightingSpirit : ShinGetterCardBase
{
    public override int SpiritRequirement => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

    public SGC_FightingSpirit()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 施加 Power — 被攻击前先对敌人造成 6 伤害
    }

    protected override void OnUpgrade()
    {
        // TODO: 6→9 伤害
    }
}
