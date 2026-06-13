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
/// 闪光爆裂 | 攻击 | 稀有 | 1费 | 钢之魂流/输出终端/护盾特攻
/// 获得 1 易伤、1 脆弱，造成 10 伤害。每有 1 点气力就对随机敌人造成 5 伤害
/// </summary>
public sealed class SGC_FlashBurst : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

    public SGC_FlashBurst()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        // TODO: 获得 1 易伤(VulnerablePower)、1 脆弱(FrailPower)
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        // TODO: 每有 1 点气力就对随机敌人造成 5 伤害
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(4m);
        // 5→8 伤害(气力加成部分)
    }
}
