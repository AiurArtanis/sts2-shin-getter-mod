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
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new PowerVar<SGP_Wane>(2m) };

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (ReferenceEquals(card, this) && oldPileType != PileType.Hand && Pile?.Type == PileType.Hand)
            await PowerCmd.Apply<SGP_Wane>(new ThrowingPlayerChoiceContext(), Owner.Creature, 2m, Owner.Creature, this);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) => Task.CompletedTask;
    protected override void OnUpgrade() { }
}
