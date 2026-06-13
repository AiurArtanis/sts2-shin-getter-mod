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
/// 烈阳闪光弹 | 攻击 | 先古 | 1费
/// 对所有敌人造成 10 伤害，给予 2 衰退，额外造成本场战斗获得的正面层数伤害
/// </summary>
public sealed class SGC_StonerShine : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

    public SGC_StonerShine()
        : base(1, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 对所有敌人造成 10 伤害，给予 2 衰退(DecayPower)
        // TODO: 额外造成本场战斗获得的正面层数伤害
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
