#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 龙卷钻头 | 攻击 | 罕见 | 2费 | 二号/护盾特攻
/// 造成 18 伤害
/// 二号机加成：对格挡造成双倍伤害
/// </summary>
public sealed class SGC_TornadoDrill : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(18m),
        new ExtraDamageVar(18m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(GetBlockBreakerMultiplier),
    };

    public SGC_TornadoDrill()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_heavy_blunt").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(6m);
        DynamicVars.ExtraDamage.UpgradeValueBy(6m);
    }

    private static decimal GetBlockBreakerMultiplier(CardModel card, Creature? target) =>
        card is ShinGetterCardBase && IsInForm(card.Owner, ShinGetterForm.Getter2) && target?.Block > 0 ? 1m : 0m;
}
