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
/// 战斧乱舞 | 攻击 | 罕见 | 2费 | 一号/输出终端
/// 获得 3 活力，对所有敌人造成 5 伤害 2 次
/// 一号机加成：斩杀时获得 2 活力
/// </summary>
public sealed class SGC_TomahawkFury : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(5m, ValueProp.Move) };

    public SGC_TomahawkFury()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // TODO: 获得 3 活力；对所有敌人造成 5 伤害 2 次
        // TODO: 一号机加成 — 斩杀时获得 2 活力
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        // TODO: 活力 3 → 5
    }
}
