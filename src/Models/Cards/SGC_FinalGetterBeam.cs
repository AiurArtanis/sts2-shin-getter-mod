using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 终极盖塔射线 | 攻击 | 稀有 | 4费 | 攻防一体
/// 造成 40 伤害，该敌人本回合失去 10 力量
/// </summary>
public sealed class SGC_FinalGetterBeam : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(40m, ValueProp.Move),
        new DynamicVar("StrengthLoss", 10m),
    };

    public SGC_FinalGetterBeam()
        : base(4, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await PowerCmd.Apply<SGP_FinalGetterBeamStrengthLoss>(choiceContext, cardPlay.Target, DynamicVars["StrengthLoss"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
