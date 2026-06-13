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
/// 飓风打击 | 攻击 | 普通 | 1费 | 二号/过牌
/// 对所有敌人造成 6 伤害，每命中 1 目标抽 1 张
/// 二号机：获得 1 敏捷
/// </summary>
public sealed class SGC_HurricaneStrike : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

    public SGC_HurricaneStrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 对所有敌人造成 6 伤害，每命中 1 目标抽 1 张
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        // TODO: 二号机获得 1 敏捷
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
