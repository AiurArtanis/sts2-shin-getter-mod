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
/// 大雪山崩落 | 攻击 | 稀有 | 2费 | 三号/防杀终端
/// 造成 15 伤害，消耗全部格挡，每消耗 1 点额外造成 1 伤害
/// 三号机：每有一层覆甲就额外造成 1 伤害
/// </summary>
public sealed class SGC_Avalanche : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(15m, ValueProp.Move) };

    public SGC_Avalanche()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        // TODO: 消耗全部格挡，每消耗 1 点额外造成 1 伤害
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        // TODO: 三号机每有一层覆甲就额外造成 1 伤害
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
