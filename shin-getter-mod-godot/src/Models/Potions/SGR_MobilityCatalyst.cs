#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Potions;

public sealed class SGR_MobilityCatalyst : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Event;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;
    public override IEnumerable<IHoverTip> ExtraHoverTips => new[] { HoverTipFactory.FromPower<SGP_OpenGet>() };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        if (Owner.Creature.GetPower<SGP_OpenGet>() == null)
            await PowerCmd.Apply<SGP_OpenGet>(choiceContext, Owner.Creature, 1m, Owner.Creature, null);
        await PlayerCmd.GainEnergy(1m, Owner);
        await CardPileCmd.Draw(choiceContext, 2, Owner);
    }
}
