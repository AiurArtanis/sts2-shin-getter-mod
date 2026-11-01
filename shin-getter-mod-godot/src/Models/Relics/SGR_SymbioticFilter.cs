#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_SymbioticFilter : ShinGetterPlaceholderEventRelic
{
    private bool _used;
    public override Task BeforeCombatStart()
    {
        _used = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (_used || card.Owner != Owner || card.Type != CardType.Status
            || oldPileType == PileType.Hand || card.Pile?.Type != PileType.Hand) return;
        _used = true; // Set before Exhaust's nested hooks.
        Status = RelicStatus.Disabled;
        Flash();
        var context = new ThrowingPlayerChoiceContext();
        await CardCmd.Exhaust(context, card);
        await PowerCmd.Apply<SGP_Evolution>(context, Owner.Creature, 1m, Owner.Creature, null);
    }
}
