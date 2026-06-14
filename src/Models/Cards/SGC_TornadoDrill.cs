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
/// 龙卷钻头 | 攻击 | 罕见 | 2费 | 二号/护盾特攻
/// 造成 18 伤害
/// 二号机加成：对格挡造成双倍伤害
/// </summary>
public sealed class SGC_TornadoDrill : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(18m, ValueProp.Move) };

    public SGC_TornadoDrill()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        decimal damage = DynamicVars.Damage.BaseValue;
        if (HasForm(Owner, ShinGetterForm.Getter2) && cardPlay.Target.Block > 0)
            damage *= 2m;
        await DamageCmd.Attack(damage).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(6m);
    }
}
