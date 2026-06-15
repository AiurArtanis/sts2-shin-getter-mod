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
/// 狮虎突击 | 攻击 | 稀有 | X费 | 二号/攻防一体
/// 造成 12 伤害 X 次
/// 二号机：获得 1 分身
/// </summary>
public sealed class SGC_LigerAssault : ShinGetterCardBase
{
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(12m, ValueProp.Move) };

    public SGC_LigerAssault()
        : base(-1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        int x = ResolveEnergyXValue() + (IsUpgraded ? 1 : 0);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(x).FromCard(this)
            .Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (x > 0 && HasForm(Owner, ShinGetterForm.Getter2))
            await PowerCmd.Apply<SGP_Shade>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
    }
}
