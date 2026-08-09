#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Potions;

public sealed class SGR_AdaptiveInk : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Evolution>(1m),
        new PowerVar<RegenPower>(3m),
    };

    public override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_Evolution>(),
        HoverTipFactory.FromPower<RegenPower>(),
    };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        await PowerCmd.Apply<SGP_Evolution>(
            choiceContext,
            Owner.Creature,
            DynamicVars["SGP_Evolution"].BaseValue,
            Owner.Creature,
            null);
        await PowerCmd.Apply<RegenPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["RegenPower"].BaseValue,
            Owner.Creature,
            null);
    }
}
