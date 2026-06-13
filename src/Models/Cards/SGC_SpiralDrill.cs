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
/// 螺旋钻头 | 攻击 | 罕见 | 1费 | 二号/护盾特攻
/// 造成 3 伤害 4 次
/// 二号机加成：无视格挡造成伤害
/// </summary>
public sealed class SGC_SpiralDrill : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(3m, ValueProp.Move) };

    public SGC_SpiralDrill()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        // TODO: 造成 3 伤害 4 次
        // TODO: 二号机加成 — 无视格挡造成伤害
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
