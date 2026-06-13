using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_GetterBeam : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<SGP_Wane>() };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(9m, ValueProp.Move), new PowerVar<SGP_Wane>(1m) };

    public SGC_GetterBeam()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await PowerCmd.Apply<SGP_Wane>(choiceContext, cardPlay.Target, 1m, base.Owner.Creature, this);
        // TODO: 一号机额外造成本场战斗获得的活力伤害
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
