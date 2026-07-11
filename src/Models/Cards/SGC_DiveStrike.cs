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
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 俯冲打击 | 攻击 | 普通 | 1费 | 一号
/// 造成 9 伤害。若腾空，伤害翻倍。
/// 一号机：获得 1 腾空。
/// </summary>
public sealed class SGC_DiveStrike : ShinGetterCardBase
{
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(9m),
        new ExtraDamageVar(9m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(GetAirborneMultiplier),
    };

    public SGC_DiveStrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this)
            .WithNoAttackerAnim()
            .Targeting(cardPlay.Target)
            .BeforeDamage(() => PlayMovementVfx(() => ShinGetterCombatVfx.PlayDiveStrike(Owner.Creature, cardPlay.Target)))
            .Execute(choiceContext);

        if (HasForm(base.Owner, ShinGetterForm.Getter1))
            await PowerCmd.Apply<SGP_Airborne>(choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(3m);
        DynamicVars.ExtraDamage.UpgradeValueBy(3m);
    }

    private static decimal GetAirborneMultiplier(CardModel card, Creature? _) =>
        card.Owner.Creature.GetPower<SGP_Airborne>() != null ? 1m : 0m;
}
