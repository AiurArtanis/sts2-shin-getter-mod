using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 换挡打击 | 攻击 | 普通 | 1费 | 变形流
/// 造成 7 伤害，获得 1 活力、1 再生、1 覆甲，变形
/// </summary>
public sealed class SGC_ShiftStrike : ShinGetterCardBase
{
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(7m, ValueProp.Move) };

    public SGC_ShiftStrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        if (IsUpgraded)
            await Transform(choiceContext, Owner, this);

        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        await Transform(choiceContext, Owner, this);
    }

    protected override void OnUpgrade()
    {
    }
}
