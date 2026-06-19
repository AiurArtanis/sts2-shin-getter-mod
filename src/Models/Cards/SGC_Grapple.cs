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
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Grapple : ShinGetterCardBase
{
    protected override IEnumerable<string> ExtraRunAssetPaths => new[] { "res://scenes/vfx/monsters/vine_shambler_vines/vine_shambler_vines_vfx.tscn" };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<SGP_Grapple>(),
        HoverTipFactory.FromPower<StrengthPower>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<WeakPower>(1m),
        new PowerVar<SGP_Grapple>(2m),
        new PowerVar<StrengthPower>(2m),
    };

    public SGC_Grapple()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        await ShinGetterCombatVfx.PlayGrappleVines(cardPlay.Target);
        await PowerCmd.Apply<WeakPower>(choiceContext, cardPlay.Target, DynamicVars["WeakPower"].BaseValue, base.Owner.Creature, this);
        await PowerCmd.Apply<SGP_Grapple>(choiceContext, cardPlay.Target, DynamicVars["SGP_Grapple"].BaseValue, base.Owner.Creature, this);

        if (HasForm(Owner, ShinGetterForm.Getter3))
            await PowerCmd.Apply<StrengthPower>(choiceContext, cardPlay.Target, -DynamicVars["StrengthPower"].BaseValue, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["WeakPower"].UpgradeValueBy(1m);
        DynamicVars["SGP_Grapple"].UpgradeValueBy(1m);
    }
}
