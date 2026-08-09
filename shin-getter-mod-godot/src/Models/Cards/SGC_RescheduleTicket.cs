#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_RescheduleTicket : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    protected override bool IsPlayable =>
        Owner?.PlayerCombatState?.Hand.Cards.Any(card => card != this) == true;

    public SGC_RescheduleTicket()
        : base(1, CardType.Skill, CardRarity.Event, TargetType.Self, false)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 1),
                card => card != this,
                this))
            .FirstOrDefault();
        if (selected == null)
            return;

        CardType targetType = selected.Type;
        await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Bottom);

        while (true)
        {
            CardModel? drawn = (await CardPileCmd.Draw(choiceContext, 1, Owner)).FirstOrDefault();
            if (drawn == null || drawn.Type == targetType)
                break;
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
