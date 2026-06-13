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
/// 盖塔线爆发 | 攻击 | 罕见 | 1费 | 一号/输出终端
/// 造成 8 伤害，本回合名字中有"盖塔"的卡牌费用减 1，消耗
/// 一号机加成：获得 8 活力
/// </summary>
public sealed class SGC_GetterRayOverflow : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(8m, ValueProp.Move) };

    public SGC_GetterRayOverflow()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        // TODO: 本回合名字中有"盖塔"的卡牌费用减 1；消耗
        // TODO: 一号机加成 — 获得 8 活力
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // TODO: 费用 1 → 0
    }
}
