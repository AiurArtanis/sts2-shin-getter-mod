#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Potions;

public sealed class SGR_GetterColdBrew : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;

    public override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromKeyword(CardKeyword.Retain) };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        List<CardModel> cards = CardFactory.GetForCombat(
                Owner,
                Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
                    .Where(card => card is ShinGetterCardBase
                        && card.GetType().Name.Contains("Getter", System.StringComparison.Ordinal)
                        && card.CanBeGeneratedInCombat),
                3,
                Owner.RunState.Rng.CombatCardGeneration)
            .ToList();

        foreach (CardModel card in cards)
            CardCmd.ApplyKeyword(card, CardKeyword.Retain);

        await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, Owner);
    }
}
