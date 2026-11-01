#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_BrokenDrill : ShinGetterEventCardBase
{
    public SGC_BrokenDrill() : base(-1, CardType.Status, TargetType.Self) { }
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Unplayable };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Wane>(2m), new PowerVar<SGP_Ki>(1m),
    };

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        // Generated/returned cards are not draws. A filter may already have exhausted this card.
        if (ReferenceEquals(card, this) && Pile?.Type == PileType.Hand)
            await PowerCmd.Apply<SGP_Wane>(choiceContext, Owner.Creature, DynamicVars["SGP_Wane"].BaseValue, Owner.Creature, this);
    }

    public override async Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (ReferenceEquals(card, this))
            await PowerCmd.Apply<SGP_Ki>(choiceContext, Owner.Creature, DynamicVars["SGP_Ki"].BaseValue, Owner.Creature, this);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
    protected override void OnUpgrade() { }
}
