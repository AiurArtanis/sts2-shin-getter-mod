#nullable enable
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_SymbioticFilter : ShinGetterEventRelic
{
    private static readonly ConditionalWeakTable<SGR_SymbioticFilter, HashSet<CardModel>> NewHandInstances = new();
    private bool _used;
    public override Task BeforeCombatStart()
    {
        _used = false;
        NewHandInstances.Remove(this);
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        // Transform emits Hand -> Hand for its new replacement. Record the new instance,
        // then consume at the usual pile-change boundary, after all entry hooks complete.
        if (!_used && card.Owner == Owner && card.Type == CardType.Status && card.Pile?.Type == PileType.Hand)
            NewHandInstances.GetValue(this, _ => new HashSet<CardModel>(ReferenceEqualityComparer.Instance)).Add(card);
        return Task.CompletedTask;
    }

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        bool newHandInstance = NewHandInstances.TryGetValue(this, out var entries) && entries.Remove(card);
        if (_used || card.Owner != Owner || card.Type != CardType.Status
            || oldPileType == PileType.Hand && !newHandInstance || card.Pile?.Type != PileType.Hand) return;
        _used = true; // Set before Exhaust's nested hooks.
        Status = RelicStatus.Disabled;
        Flash();
        // Exhaust can trigger draws and native card choices. This hook has no supplied context.
        var context = new BlockingPlayerChoiceContext();
        await CardCmd.Exhaust(context, card);
        await PowerCmd.Apply<SGP_Evolution>(context, Owner.Creature, 1m, Owner.Creature, null);
    }
}
