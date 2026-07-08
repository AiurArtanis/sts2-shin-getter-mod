using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Insight : ShinGetterCardBase
{
    public override int SpiritRequirement => 3;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_Insight>(),
        HoverTipFactory.FromPower<DexterityPower>(),
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<ThornsPower>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Insight>(1m),
        new PowerVar<StrengthPower>(1m),
        new EnergyVar(1),
        new PowerVar<ThornsPower>(2m),
    };

    public SGC_Insight()
        : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShinGetterCombatVfx.PlayNewtypeSense(Owner.Creature);
        await PowerCmd.Apply<SGP_Insight>(choiceContext, Owner.Creature, DynamicVars["SGP_Insight"].BaseValue, Owner.Creature, this);

        if (HasForm(Owner, ShinGetterForm.Getter1))
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, DynamicVars["StrengthPower"].BaseValue, Owner.Creature, this);
        if (HasForm(Owner, ShinGetterForm.Getter2))
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        if (HasForm(Owner, ShinGetterForm.Getter3))
            await PowerCmd.Apply<ThornsPower>(choiceContext, Owner.Creature, DynamicVars["ThornsPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_Insight"].UpgradeValueBy(1m);
    }
}
