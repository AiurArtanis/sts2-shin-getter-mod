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
/// 螺旋钻头 | 攻击 | 罕见 | 1费 | 二号/护盾特攻
/// 造成 3 伤害 4 次
/// 二号机加成：无视格挡造成伤害
/// </summary>
public sealed class SGC_SpiralDrill : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(3m, ValueProp.Move) };

    public SGC_SpiralDrill()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        if (HasForm(Owner, ShinGetterForm.Getter2))
        {
            const ValueProp damageProps = ValueProp.Move | ValueProp.Unblockable;
            for (int i = 0; i < 4 && cardPlay.Target.IsAlive; i++)
            {
                await CreatureCmd.Damage(choiceContext, cardPlay.Target, DynamicVars.Damage.BaseValue, damageProps, this);
            }
            if (Owner.Creature.GetPower<SGP_HotBlood>() is { } hotBlood)
                await hotBlood.ConsumeForCardDamage(choiceContext, this, ValueProp.Move | ValueProp.Unblockable);
        }
        else
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(4).FromCard(this)
                .Targeting(cardPlay.Target).WithHitFx("vfx/vfx_heavy_blunt").Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(1m);
    }
}
