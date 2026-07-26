#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Events;

internal static class ShinGetterEventInvasionService
{
    private const string LocPrefix = "SHIN_GETTER_EVENT_INVASION";

    private static readonly MethodInfo SetEventFinishedMethod =
        AccessTools.Method(typeof(EventModel), "SetEventFinished", new[] { typeof(LocString) })
        ?? throw new MissingMethodException(typeof(EventModel).FullName, "SetEventFinished");

    internal static IEnumerable<EventOption> AppendOptions(
        EventModel eventModel,
        IEnumerable<EventOption> eventOptions)
    {
        List<EventOption> options = eventOptions.ToList();
        if (!ShouldInject(eventModel, options))
            return options;

        IEnumerable<EventOption> invasions = eventModel switch
        {
            TeaMaster teaMaster => BuildTeaMasterOptions(teaMaster),
            SlipperyBridge slipperyBridge => BuildSlipperyBridgeOptions(slipperyBridge),
            SpiritGrafter spiritGrafter => BuildSpiritGrafterOptions(spiritGrafter),
            WoodCarvings woodCarvings => BuildWoodCarvingsOptions(woodCarvings),
            ThisOrThat thisOrThat => BuildThisOrThatOptions(thisOrThat),
            Amalgamator amalgamator => BuildAmalgamatorOptions(amalgamator),
            _ => Array.Empty<EventOption>(),
        };

        foreach (EventOption invasion in invasions)
        {
            if (options.All(option => option.TextKey != invasion.TextKey))
                options.Add(invasion);
        }

        return options;
    }

    private static bool ShouldInject(EventModel eventModel, IReadOnlyList<EventOption> options)
    {
        Player? owner = eventModel.Owner;
        if (options.Count == 0
            || eventModel.IsShared
            || owner == null
            || owner.Character is not ShinGetter
            || options.Any(option => option.TextKey.StartsWith(LocPrefix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!options.Any(option => option.TextKey.Contains(".pages.INITIAL.options.", StringComparison.Ordinal)))
            return false;

        return owner.GetRelic<SGR_GetterFurnace>()?.EventInvasionEnabled
            ?? owner.GetRelic<SGR_EmperorsFragment>()?.EventInvasionEnabled
            ?? false;
    }

    private static IEnumerable<EventOption> BuildTeaMasterOptions(TeaMaster eventModel)
    {
        Player owner = RequireOwner(eventModel);

        if (owner.Creature.CurrentHp > 5
            && HasAnyCard<SGC_FightingSpirit, SGC_Ki>(owner))
        {
            yield return new EventOption(
                    eventModel,
                    () => TeaMasterRyoma(eventModel),
                    Key("TEA_MASTER", "RYOMA"),
                    HoverTipFactory.FromRelicExcludingItself<EmberTea>())
                .ThatDoesDamage(5);
        }

        if (owner.Gold >= 100)
        {
            IHoverTip[] hovers = HoverTipFactory.FromRelicExcludingItself<BoneTea>()
                .Concat(HoverTipFactory.FromRelicExcludingItself<EmberTea>())
                .ToArray();
            yield return new EventOption(
                eventModel,
                () => TeaMasterMuqing(eventModel),
                Key("TEA_MASTER", "MUQING"),
                disableOnChosen: false,
                isProceed: false,
                hovers);
        }
    }

    private static IEnumerable<EventOption> BuildSlipperyBridgeOptions(SlipperyBridge eventModel)
    {
        Player owner = RequireOwner(eventModel);
        if (owner.Creature.CurrentHp > 5
            && HasAnyCard<SGC_Ki, SGC_Spirit, SGC_SuperKi>(owner))
        {
            yield return new EventOption(
                    eventModel,
                    () => SlipperyBridgeRyoma(eventModel),
                    Key("SLIPPERY_BRIDGE", "RYOMA"))
                .ThatDoesDamage(5);
        }

        if (owner.Deck.Cards.Any(card => card.IsRemovable)
            && HasAnyCard<SGC_Acceleration, SGC_ShedLoad>(owner))
        {
            yield return new EventOption(
                eventModel,
                () => SlipperyBridgeHayato(eventModel),
                Key("SLIPPERY_BRIDGE", "HAYATO"));
        }
    }

    private static IEnumerable<EventOption> BuildSpiritGrafterOptions(SpiritGrafter eventModel)
    {
        Player owner = RequireOwner(eventModel);
        if (Enum.GetValues<ShinGetterForm>()
            .Where(form => form != ShinGetterForm.None)
            .All(form => HasExclusiveFormCard(owner, form)))
        {
            yield return new EventOption(
                eventModel,
                () => SpiritGrafterTripleUnity(eventModel),
                Key("SPIRIT_GRAFTER", "TRIPLE_UNITY"),
                HoverTipFactory.FromCardWithCardHoverTips<SGC_TripleUnity>());
        }
    }

    private static IEnumerable<EventOption> BuildWoodCarvingsOptions(WoodCarvings eventModel)
    {
        yield return new EventOption(
            eventModel,
            () => WoodCarvingsTripleCarving(eventModel),
            Key("WOOD_CARVINGS", "TRIPLE_CARVING"),
            HoverTipFactory.FromRelicExcludingItself<SGR_TripleWoodCarving>());
    }

    private static IEnumerable<EventOption> BuildThisOrThatOptions(ThisOrThat eventModel)
    {
        yield return new EventOption(
            eventModel,
            () => ThisOrThatRyoma(eventModel),
            Key("THIS_OR_THAT", "RYOMA"));

        Player owner = RequireOwner(eventModel);
        if (owner.Gold >= 75
            && HasAnyCard<SGC_Insight, SGC_BackupPlan>(owner))
        {
            yield return new EventOption(
                eventModel,
                () => ThisOrThatHayato(eventModel),
                Key("THIS_OR_THAT", "HAYATO"));
        }
    }

    private static IEnumerable<EventOption> BuildAmalgamatorOptions(Amalgamator eventModel)
    {
        Player owner = RequireOwner(eventModel);
        if (owner.Deck.Cards.Any(IsRyomaExtraRemovalCandidate))
        {
            yield return new EventOption(
                eventModel,
                () => AmalgamatorRyoma(eventModel),
                Key("AMALGAMATOR", "RYOMA"));
        }

        if (owner.Gold >= 100
            && owner.Deck.Cards.Count(card => card is SGC_Strike && card.IsRemovable) >= 2
            && owner.Deck.Cards.Count(card => card is SGC_Defend && card.IsRemovable) >= 2)
        {
            IHoverTip[] hovers = HoverTipFactory.FromCardWithCardHoverTips<UltimateStrike>()
                .Concat(HoverTipFactory.FromCardWithCardHoverTips<UltimateDefend>())
                .ToArray();
            yield return new EventOption(
                eventModel,
                () => AmalgamatorMuqing(eventModel),
                Key("AMALGAMATOR", "MUQING"),
                hovers);
        }
    }

    private static async Task TeaMasterRyoma(TeaMaster eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 5);
        await RelicCmd.Obtain<EmberTea>(owner);
        Finish(eventModel, PageKey("TEA_MASTER", "RYOMA"));
    }

    private static async Task TeaMasterMuqing(TeaMaster eventModel)
    {
        Player owner = RequireOwner(eventModel);
        RelicModel? selected = await RelicSelectCmd.FromChooseARelicScreen(owner, new RelicModel[]
        {
            ModelDb.Relic<BoneTea>().ToMutable(),
            ModelDb.Relic<EmberTea>().ToMutable(),
        });
        if (selected == null)
            return;

        await PlayerCmd.LoseGold(100, owner, GoldLossType.Spent);
        await RelicCmd.Obtain(selected, owner);
        Finish(eventModel, PageKey("TEA_MASTER", "MUQING"));
    }

    private static async Task SlipperyBridgeRyoma(SlipperyBridge eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 5);

        List<CardModel> upgradeCandidates = owner.Deck.Cards
            .Where(card => card.IsUpgradable
                && card is SGC_Ki or SGC_Spirit or SGC_SuperKi)
            .ToList();
        if (upgradeCandidates.Count > 0)
            CardCmd.Upgrade(eventModel.Rng.NextItem(upgradeCandidates)!, CardPreviewStyle.EventLayout);

        Finish(eventModel, PageKey("SLIPPERY_BRIDGE", "RYOMA"));
    }

    private static async Task SlipperyBridgeHayato(SlipperyBridge eventModel)
    {
        Player owner = RequireOwner(eventModel);
        CardModel? selected = (await CardSelectCmd.FromDeckForRemoval(
            owner,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1)))
            .FirstOrDefault();
        if (selected == null)
            return;

        await CardPileCmd.RemoveFromDeck(selected);
        Finish(eventModel, PageKey("SLIPPERY_BRIDGE", "HAYATO"));
    }

    private static async Task SpiritGrafterTripleUnity(SpiritGrafter eventModel)
    {
        Player owner = RequireOwner(eventModel);
        CardModel card = owner.RunState.CreateCard<SGC_TripleUnity>(owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
        Finish(eventModel, PageKey("SPIRIT_GRAFTER", "TRIPLE_UNITY"));
    }

    private static async Task WoodCarvingsTripleCarving(WoodCarvings eventModel)
    {
        await RelicCmd.Obtain<SGR_TripleWoodCarving>(RequireOwner(eventModel));
        Finish(eventModel, PageKey("WOOD_CARVINGS", "TRIPLE_CARVING"));
    }

    private static Task ThisOrThatRyoma(ThisOrThat eventModel)
    {
        Finish(eventModel, PageKey("THIS_OR_THAT", "RYOMA"));
        return Task.CompletedTask;
    }

    private static async Task ThisOrThatHayato(ThisOrThat eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await PlayerCmd.LoseGold(75, owner, GoldLossType.Spent);
        RelicModel relic = RelicFactory.PullNextRelicFromFront(owner).ToMutable();
        await RelicCmd.Obtain(relic, owner);
        Finish(eventModel, PageKey("THIS_OR_THAT", "HAYATO"));
    }

    private static async Task AmalgamatorRyoma(Amalgamator eventModel)
    {
        Player owner = RequireOwner(eventModel);
        CardModel? extra = (await CardSelectCmd.FromDeckForRemoval(
            owner,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1),
            IsRyomaExtraRemovalCandidate))
            .FirstOrDefault();
        if (extra == null)
            return;

        List<CardModel> cardsToRemove = owner.Deck.Cards
            .Where(card => card.IsRemovable && card is SGC_Strike or SGC_Defend)
            .Append(extra)
            .ToList();
        await CardPileCmd.RemoveFromDeck(cardsToRemove);

        Finish(eventModel, PageKey("AMALGAMATOR", "RYOMA"));
    }

    private static async Task AmalgamatorMuqing(Amalgamator eventModel)
    {
        Player owner = RequireOwner(eventModel);
        List<CardModel> strikes = (await CardSelectCmd.FromDeckForRemoval(
            owner,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 2),
            card => card is SGC_Strike))
            .ToList();
        List<CardModel> defends = (await CardSelectCmd.FromDeckForRemoval(
            owner,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 2),
            card => card is SGC_Defend))
            .ToList();
        if (strikes.Count != 2 || defends.Count != 2)
            return;

        bool upgradeStrike = strikes.Any(card => card.IsUpgraded);
        bool upgradeDefend = defends.Any(card => card.IsUpgraded);

        await PlayerCmd.LoseGold(100, owner, GoldLossType.Spent);
        await CardPileCmd.RemoveFromDeck(strikes.Concat(defends).ToList());

        CardModel ultimateStrike = owner.RunState.CreateCard<UltimateStrike>(owner);
        CardModel ultimateDefend = owner.RunState.CreateCard<UltimateDefend>(owner);
        if (upgradeStrike)
            CardCmd.Upgrade(ultimateStrike, CardPreviewStyle.None);
        if (upgradeDefend)
            CardCmd.Upgrade(ultimateDefend, CardPreviewStyle.None);

        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(ultimateStrike, PileType.Deck));
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(ultimateDefend, PileType.Deck));
        Finish(eventModel, PageKey("AMALGAMATOR", "MUQING"));
    }

    private static Task LoseHp(Player owner, int amount) =>
        CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);

    private static Player RequireOwner(EventModel eventModel) =>
        eventModel.Owner ?? throw new InvalidOperationException(
            $"Event {eventModel.Id} does not have an owner.");

    private static bool HasAnyCard<T1, T2>(Player owner)
        where T1 : CardModel
        where T2 : CardModel =>
        owner.Deck.Cards.Any(card => card is T1 or T2);

    private static bool HasAnyCard<T1, T2, T3>(Player owner)
        where T1 : CardModel
        where T2 : CardModel
        where T3 : CardModel =>
        owner.Deck.Cards.Any(card => card is T1 or T2 or T3);

    private static bool HasExclusiveFormCard(Player owner, ShinGetterForm form)
    {
        HashSet<Type> targetTypes = SGC_Specialization.GetFormCards(form)
            .Select(card => card.GetType())
            .ToHashSet();
        HashSet<Type> sharedTypes = Enum.GetValues<ShinGetterForm>()
            .Where(other => other != ShinGetterForm.None && other != form)
            .SelectMany(SGC_Specialization.GetFormCards)
            .Select(card => card.GetType())
            .ToHashSet();
        targetTypes.ExceptWith(sharedTypes);
        return owner.Deck.Cards.Any(card => targetTypes.Contains(card.GetType()));
    }

    private static bool IsRyomaExtraRemovalCandidate(CardModel card) =>
        card.IsRemovable && card is not SGC_Strike && card is not SGC_Defend;

    private static string Key(string eventName, string optionName) =>
        $"{LocPrefix}.{eventName}.pages.INITIAL.options.{optionName}";

    private static string PageKey(string eventName, string pageName) =>
        $"{LocPrefix}.{eventName}.pages.{pageName}.description";

    private static void Finish(EventModel eventModel, string descriptionKey)
    {
        SetEventFinishedMethod.Invoke(
            eventModel,
            new object[] { new LocString("events", descriptionKey) });
    }
}
