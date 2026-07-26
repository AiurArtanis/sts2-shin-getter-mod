using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 狮虎突击 | 攻击 | 稀有 | X费 | 二号/攻防一体
/// 造成 12 伤害 X 次
/// 二号机：获得 1 分身和 1 缓冲
/// </summary>
public sealed class SGC_LigerAssault : ShinGetterCardBase
{
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(12m, ValueProp.Move),
        new PowerVar<SGP_Shade>(1m),
        new PowerVar<BufferPower>(1m),
    };

    public SGC_LigerAssault()
        : base(-1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        int x = ResolveEnergyXValue() + (IsUpgraded ? 1 : 0);
        if (HasForm(Owner, ShinGetterForm.Getter2))
        {
            await PowerCmd.Apply<SGP_Shade>(choiceContext, Owner.Creature, DynamicVars["SGP_Shade"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<BufferPower>(choiceContext, Owner.Creature, DynamicVars["BufferPower"].BaseValue, Owner.Creature, this);
        }
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(x).FromCard(this)
            .Targeting(cardPlay.Target)
            .AfterAttackerAnim(AccelerateFollowupAnimations(x))
            .WithHitFx("vfx/vfx_scratch").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
    }
}
