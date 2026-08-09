#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Potions;

public sealed class SGR_LuminescentPulse : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<SGP_Radiation>(1m) };

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<SGP_Radiation>() };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        foreach (Creature creature in Owner.Creature.CombatState.Creatures.Where(creature => creature.IsAlive))
        {
            await PowerCmd.Apply<SGP_Radiation>(
                choiceContext,
                creature,
                DynamicVars["SGP_Radiation"].BaseValue,
                Owner.Creature,
                null);
        }
    }
}
