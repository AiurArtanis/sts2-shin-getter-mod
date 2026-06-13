using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 冲撞 — 造成 12 点伤害，施加 1 层虚弱。
/// </summary>
public sealed class ShinGetterCard_07 : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<WeakPower>() };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] {
        new DamageVar(12m, ValueProp.Move),
        new PowerVar<WeakPower>(1m)
    };

    public ShinGetterCard_07()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
        await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, 1m, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
