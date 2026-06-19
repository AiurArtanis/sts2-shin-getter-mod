using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
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
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(9m, ValueProp.Move) };

    public SGC_DiveStrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var dmg = base.DynamicVars.Damage.BaseValue;
        if (GetPowerAmount<SGP_Airborne>(base.Owner) > 0)
            dmg *= 2;

        await DamageCmd.Attack(dmg).FromCard(this)
            .WithNoAttackerAnim()
            .Targeting(cardPlay.Target)
            .BeforeDamage(() => ShinGetterCombatVfx.PlayDiveStrike(Owner.Creature, cardPlay.Target))
            .Execute(choiceContext);

        if (HasForm(base.Owner, ShinGetterForm.Getter1))
            await PowerCmd.Apply<SGP_Airborne>(choiceContext, base.Owner.Creature, 1m, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
