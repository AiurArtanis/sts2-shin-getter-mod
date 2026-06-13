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
/// 斩星斧 | 攻击 | 稀有 | 3费 | 烧牌/输出终端
/// 消耗抽牌堆 1 张卡，将数值叠加在此卡上，共造成 20 点伤害
/// 一号机：额外造成本场战斗获得的活力值
/// </summary>
public sealed class SGC_StarSlash : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(20m, ValueProp.Move) };

    public SGC_StarSlash()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        // TODO: 消耗抽牌堆 1 张卡，将数值叠加在此卡上
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        // TODO: 一号机额外造成本场战斗获得的活力值
    }

    protected override void OnUpgrade()
    {
        // 消耗 1→2 张卡
    }
}
