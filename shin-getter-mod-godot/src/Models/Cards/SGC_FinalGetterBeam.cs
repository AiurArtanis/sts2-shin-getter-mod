using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Nodes.Vfx;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 终极盖塔射线 | 攻击 | 稀有 | 3费
/// 造成 25 伤害，施加 4 衰退，并强化衰退受伤后的增长量
/// </summary>
public sealed class SGC_FinalGetterBeam : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(25m, ValueProp.Move),
        new PowerVar<SGP_Wane>(4m),
        new PowerVar<SGP_FinalGetterBeam>(2m),
    };

    public SGC_FinalGetterBeam()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
            .WithAttackerAnim("Cast", 0.5f)
            .BeforeDamage(() => ShinGetterBeamVfx.Play(Owner.Creature, new[] { cardPlay.Target }, ShinGetterBeamStyle.FinalGetterBeam))
            .Execute(choiceContext);
        if (!cardPlay.Target.IsAlive)
            return;

        await PowerCmd.Apply<SGP_Wane>(choiceContext, cardPlay.Target,
            DynamicVars["SGP_Wane"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<SGP_FinalGetterBeam>(choiceContext, cardPlay.Target,
            DynamicVars["SGP_FinalGetterBeam"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_FinalGetterBeam"].UpgradeValueBy(1m);
    }
}
