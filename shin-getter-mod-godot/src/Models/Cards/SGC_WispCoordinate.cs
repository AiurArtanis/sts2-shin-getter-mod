#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_WispCoordinate : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(4),
        new PowerVar<SGP_Radiation>(1m),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_Radiation>(),
    });

    protected override bool IsPlayable =>
        Owner?.PlayerCombatState?.DrawPile.IsEmpty == false;

    public SGC_WispCoordinate()
        : base(1, CardType.Skill, CardRarity.Event, TargetType.Self, false)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> revealed = PileType.Draw.GetPile(Owner).Cards
            .Take(DynamicVars.Cards.IntValue)
            .ToList();
        if (revealed.Count == 0)
            return;

        CardModel? selected = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                revealed,
                Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 1)))
            .FirstOrDefault();
        if (selected == null)
            return;

        await CardPileCmd.Add(selected, PileType.Hand);
        revealed.Remove(selected);

        List<CardModel> orderedTopToBottom = new();
        while (revealed.Count > 1)
        {
            LocString prompt = new("cards", "S_G_C_WISP_COORDINATE.orderSelectionScreenPrompt");
            CardModel? next = (await CardSelectCmd.FromSimpleGrid(
                    choiceContext,
                    revealed,
                    Owner,
                    new CardSelectorPrefs(prompt, 1)))
                .FirstOrDefault();
            if (next == null)
                return;

            orderedTopToBottom.Add(next);
            revealed.Remove(next);
        }

        orderedTopToBottom.AddRange(revealed);
        foreach (CardModel card in orderedTopToBottom.AsEnumerable().Reverse())
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top, skipVisuals: true);

        await PowerCmd.Apply<SGP_Radiation>(
            choiceContext,
            Owner.Creature,
            DynamicVars["SGP_Radiation"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(2m);
    }
}
