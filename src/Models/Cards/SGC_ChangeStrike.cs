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
/// 变形打击 | 攻击 | 普通 | 1费 | 变形流
/// 造成 7 伤害，获得 1 活力、1 再生、1 覆甲，变形
/// </summary>
public sealed class SGC_ChangeStrike : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(7m, ValueProp.Move) };

    public SGC_ChangeStrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        // TODO: 获得 1 活力(VigorPower)、1 再生(RegenPower)、1 覆甲(PlatingPower)
        // TODO: 变形到下一形态
    }

    protected override void OnUpgrade()
    {
        // 先变形 1 次
    }
}
