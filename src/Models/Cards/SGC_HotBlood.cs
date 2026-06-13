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
/// 热血 | 攻击 | 罕见 | 3费 | 钢之魂流
/// 保留。【精神 2】造成 15 伤害，下一次伤害翻倍。消耗
/// </summary>
public sealed class SGC_HotBlood : ShinGetterCardBase
{
    public override int SpiritRequirement => 2;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(15m, ValueProp.Move) };

    public SGC_HotBlood()
        : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        // TODO: 保留 + 消耗关键字；下一次伤害翻倍效果
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
