#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Enchantments;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Models.Events;

public sealed class SGE_GetterMandala : EventModel
{
    private const int MandalaActIndex = 1;

    private Player EventOwner => Owner
        ?? throw new InvalidOperationException("Getter Mandala event requires an owner.");

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        List<EventOption> options = BuildOptionPool()
            .UnstableShuffle(Rng)
            .Take(3)
            .ToList();

        options.Add(new EventOption(this, IgnoreGetterWill, InitialOptionKey("IGNORE")));
        return options;
    }

    public override bool IsAllowed(IRunState runState)
    {
        bool hasVisited = runState is RunState concreteRunState
            && concreteRunState.VisitedEventIds.Contains(Id);

        return runState.CurrentActIndex == MandalaActIndex
            && runState.Players.Any(player => player.Character is ShinGetter)
            && !hasVisited;
    }

    private List<EventOption> BuildOptionPool()
    {
        var options = new List<EventOption>();
        Player owner = EventOwner;

        if (owner.GetRelic<SGR_GetterFurnace>() != null)
            options.Add(new EventOption(this, ReplaceGetterFurnace, InitialOptionKey("SOLAR_BATTLESHIP")));

        if (!DeckHasCard<SGC_ShinForm>())
            options.Add(new EventOption(this, AddShinForm, InitialOptionKey("GETTER_G_FUSION")));

        if (HasEnchantableCard<SGE_Devolution>(card => card.Type == CardType.Attack))
            options.Add(new EventOption(this, SelectDevolution, InitialOptionKey("PRIMAL_GETTER")));

        if (HasEnchantableCard<SGE_Adaptation>(card => card.Type is CardType.Attack or CardType.Skill or CardType.Power))
            options.Add(new EventOption(this, SelectAdaptation, InitialOptionKey("FIRST_EVOLUTION")));

        options.Add(new EventOption(this, AddHolyDragonRoar, InitialOptionKey("HOLY_DRAGON")));

        if (owner.Deck.Cards.Any(card => IsGetterNamedCard(card) && card.IsUpgradable))
            options.Add(new EventOption(this, UpgradeGetterCards, InitialOptionKey("GUARDIAN_GOD")));

        return options;
    }

    private async Task ReplaceGetterFurnace()
    {
        SGR_GetterFurnace? getterFurnace = EventOwner.GetRelic<SGR_GetterFurnace>();
        if (getterFurnace != null)
            await RelicCmd.Replace(getterFurnace, ModelDb.Relic<SGR_EmperorsFragment>().ToMutable());

        Finish("SOLAR_BATTLESHIP");
    }

    private async Task AddShinForm()
    {
        Player owner = EventOwner;
        CardModel card = owner.RunState.CreateCard<SGC_ShinForm>(owner);
        CardCmd.Upgrade(card);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
        Finish("GETTER_G_FUSION");
    }

    private Task SelectDevolution()
    {
        return SelectAndEnchant<SGE_Devolution>(
            card => card.Type == CardType.Attack,
            "PRIMAL_GETTER");
    }

    private Task SelectAdaptation()
    {
        return SelectAndEnchant<SGE_Adaptation>(
            card => card.Type is CardType.Attack or CardType.Skill or CardType.Power,
            "FIRST_EVOLUTION");
    }

    private async Task AddHolyDragonRoar()
    {
        Player owner = EventOwner;
        CardModel card = owner.RunState.CreateCard<SGC_HolyDragonRoar>(owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
        Finish("HOLY_DRAGON");
    }

    private Task UpgradeGetterCards()
    {
        List<CardModel> cards = EventOwner.Deck.Cards
            .Where(card => IsGetterNamedCard(card) && card.IsUpgradable)
            .ToList();

        CardCmd.Upgrade(cards, CardPreviewStyle.EventLayout);
        Finish("GUARDIAN_GOD");
        return Task.CompletedTask;
    }

    private async Task IgnoreGetterWill()
    {
        Player owner = EventOwner;
        CardModel card = owner.RunState.CreateCard<SGC_InsectVirus>(owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 2f);
        Finish("IGNORE");
    }

    private async Task SelectAndEnchant<T>(Func<CardModel, bool> cardFilter, string pageName)
        where T : EnchantmentModel
    {
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1);
        EnchantmentModel enchantment = ModelDb.Enchantment<T>();
        Player owner = EventOwner;
        CardModel? card = (await CardSelectCmd.FromDeckForEnchantment(
            owner,
            enchantment,
            1,
            card => card != null && cardFilter(card),
            prefs)).FirstOrDefault();

        if (card != null)
            await ApplyEnchantment<T>(card, 1);

        Finish(pageName);
    }

    private Task ApplyEnchantment<T>(CardModel card, int amount)
        where T : EnchantmentModel
    {
        CardCmd.Enchant<T>(card, amount);
        NCardEnchantVfx? vfx = NCardEnchantVfx.Create(card);
        if (vfx != null)
            NRun.Instance?.GlobalUi?.CardPreviewContainer.AddChildSafely(vfx);

        return Task.CompletedTask;
    }

    private bool HasEnchantableCard<T>(Func<CardModel, bool> cardFilter)
        where T : EnchantmentModel
    {
        EnchantmentModel enchantment = ModelDb.Enchantment<T>();
        return EventOwner.Deck.Cards.Any(card => cardFilter(card) && enchantment.CanEnchant(card));
    }

    private bool DeckHasCard<T>()
        where T : CardModel
    {
        return EventOwner.Deck.Cards.Any(card => card.Id == ModelDb.Card<T>().Id);
    }

    private static bool IsGetterNamedCard(CardModel card)
    {
        return card.Title.Contains("盖塔", StringComparison.Ordinal)
            || card.Id.Entry.Contains("GETTER", StringComparison.Ordinal);
    }

    private void Finish(string pageName)
    {
        SetEventFinished(L10NLookup($"S_G_E_GETTER_MANDALA.pages.{pageName}.description"));
    }
}
