#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Enchantments;
using ShinGetterMod.Models.Potions;
using ShinGetterMod.Models.Relics;
using LostWispRelic = MegaCrit.Sts2.Core.Models.Relics.LostWisp;

namespace ShinGetterMod.Events;

internal static partial class ShinGetterEventInvasionService
{
    private sealed class Issue89EventState
    {
        internal bool HasRerolledTinkerTypes { get; set; }
        internal bool HasTakenRescheduleTicket { get; set; }
    }

    private static readonly ConditionalWeakTable<EventModel, Issue89EventState> Issue89States = new();

    private static readonly MethodInfo TinkerChooseCardTypeMethod =
        AccessTools.Method(typeof(TinkerTime), "ChooseCardType")
        ?? throw new MissingMethodException(typeof(TinkerTime).FullName, "ChooseCardType");

    private static readonly MethodInfo EndlessConveyorGenerateOptionsMethod =
        AccessTools.Method(typeof(EndlessConveyor), "GenerateInitialOptions")
        ?? throw new MissingMethodException(typeof(EndlessConveyor).FullName, "GenerateInitialOptions");

    private static readonly MethodInfo RelicTraderGenerateOptionsMethod =
        AccessTools.Method(typeof(RelicTrader), "GenerateInitialOptions")
        ?? throw new MissingMethodException(typeof(RelicTrader).FullName, "GenerateInitialOptions");

    private static readonly FieldInfo ColossalFlowerDigCountField =
        AccessTools.Field(typeof(ColossalFlower), "_numberOfDigs")
        ?? throw new MissingFieldException(typeof(ColossalFlower).FullName, "_numberOfDigs");

    private static readonly RelicModel[] TrashHeapRelics =
    {
        ModelDb.Relic<DarkstonePeriapt>(),
        ModelDb.Relic<DreamCatcher>(),
        ModelDb.Relic<HandDrill>(),
        ModelDb.Relic<MawBank>(),
        ModelDb.Relic<TheBoot>(),
    };

    private static void ApplyIssue89OptionReplacements(
        EventModel eventModel,
        List<EventOption> options)
    {
        if (eventModel is not WelcomeToWongos)
            return;

        Player owner = RequireOwner(eventModel);
        SGR_GoodCitizenCard? citizenCard = owner.GetRelic<SGR_GoodCitizenCard>();
        if (citizenCard == null || citizenCard.FreePurchaseActIndices.Count == 0)
            return;

        int sealedTransactions = Math.Min(3, Math.Min(options.Count, citizenCard.FreePurchaseActIndices.Count));
        for (int i = 0; i < sealedTransactions; i++)
        {
            int actIndex = Math.Clamp(citizenCard.FreePurchaseActIndices[i], 0, owner.RunState.Acts.Count - 1);
            LocString title = new("events", $"{LocPrefix}.WELCOME_TO_WONGOS.pages.INITIAL.options.TRANSACTION_SEALED.title");
            LocString description = new("events", $"{LocPrefix}.WELCOME_TO_WONGOS.pages.INITIAL.options.TRANSACTION_SEALED.description");
            description.Add("Act", owner.RunState.Acts[actIndex].Title.GetFormattedText());
            options[i] = new EventOption(
                eventModel,
                null,
                title,
                description,
                Key("WELCOME_TO_WONGOS", $"TRANSACTION_SEALED_{i + 1}"),
                Array.Empty<IHoverTip>()).ThatHasDynamicTitle();
        }
    }

    private static IEnumerable<EventOption> BuildWelcomeToWongosOptions(WelcomeToWongos eventModel)
    {
        Player owner = RequireOwner(eventModel);
        SGR_GoodCitizenCard? citizenCard = owner.GetRelic<SGR_GoodCitizenCard>();
        if (citizenCard == null || citizenCard.FreePurchaseActIndices.Count == 0)
            yield break;

        yield return CreateConditionalOption(
            eventModel,
            available: true,
            () => WelcomeToWongosCitizenExit(eventModel),
            "WELCOME_TO_WONGOS",
            "HAYATO",
            HoverTipFactory.FromRelic<SGR_GoodCitizenCard>());
    }

    private static IEnumerable<EventOption> BuildTrashHeapOptions(TrashHeap eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.CurrentHp > 12
            && owner.Deck.Cards.Any(card => card is SGC_Indomitable or SGC_IronWall or SGC_Guts);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => TrashHeapBenkei(eventModel),
            "TRASH_HEAP",
            "BENKEI");
    }

    private static IEnumerable<EventOption> BuildTinkerTimeOptions(
        TinkerTime eventModel,
        IReadOnlyList<EventOption> options)
    {
        bool isCardTypePage = options.Any(option =>
            option.TextKey.StartsWith(
                "TINKER_TIME.pages.CHOOSE_CARD_TYPE.options.",
                StringComparison.Ordinal));
        if (!isCardTypePage)
            yield break;

        Player owner = RequireOwner(eventModel);
        Issue89EventState state = Issue89States.GetOrCreateValue(eventModel);
        if (state.HasRerolledTinkerTypes)
            yield break;

        bool available = owner.Gold >= 50
            && HasRole(owner, ShinGetterCardRole.ResearchEvolution);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => TinkerTimeHayato(eventModel),
            "TINKER_TIME",
            "HAYATO");
    }

    private static IEnumerable<EventOption> BuildReflectionsOptions(Reflections eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.MaxHp > 5
            && owner.Deck.Cards.Any(card => card is SGC_TripleUnity)
            && owner.Deck.Cards.Any(IsMirrorCopyCandidate);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => ReflectionsTripleUnity(eventModel),
            "REFLECTIONS",
            "TRIPLE_UNITY");
    }

    private static IEnumerable<EventOption> BuildDoorsOfLightAndDarkOptions(DoorsOfLightAndDark eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.CurrentHp > 6
            && HasRole(owner, ShinGetterCardRole.SpiritDrive)
            && owner.Deck.Cards.Any(card => card.Type != CardType.Quest && card.IsTransformable);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => DoorsOfLightAndDarkRyoma(eventModel),
            "DOORS_OF_LIGHT_AND_DARK",
            "RYOMA");
    }

    private static IEnumerable<EventOption> BuildWellspringOptions(Wellspring eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.MaxHp > 3
            && HasRole(owner, ShinGetterCardRole.GetterRay)
            && owner.Deck.Cards.Any(card => card.IsRemovable);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => WellspringRyoma(eventModel),
            "WELLSPRING",
            "RYOMA");
    }

    private static IEnumerable<EventOption> BuildRoomFullOfCheeseOptions(RoomFullOfCheese eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.CurrentHp > 7
            && owner.Deck.Cards.Any(card => card is SGC_Guts or SGC_Indomitable);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => RoomFullOfCheeseBenkei(eventModel),
            "ROOM_FULL_OF_CHEESE",
            "BENKEI");
    }

    private static IEnumerable<EventOption> BuildBugslayerOptions(Bugslayer eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.CurrentHp > 5
            && owner.Deck.Cards.Any(IsBugslayerRushCandidate);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => BugslayerBenkei(eventModel),
            "BUGSLAYER",
            "BENKEI",
            HoverTipFactory.FromEnchantment<Spiral>());
    }

    private static IEnumerable<EventOption> BuildRelicTraderOptions(RelicTrader eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Gold >= 75
            && owner.GetRelic<SGR_EmperorsFragment>() != null
            && GetIssue89TradeableOwnedRelics(owner).Any();
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => ShowRelicTraderOwnedPage(eventModel),
            "RELIC_TRADER",
            "HAYATO",
            HoverTipFactory.FromRelic<SGR_EmperorsFragment>(),
            disableOnChosen: false);
    }

    private static IEnumerable<EventOption> BuildEndlessConveyorOptions(EndlessConveyor eventModel)
    {
        Player owner = RequireOwner(eventModel);
        Issue89EventState state = Issue89States.GetOrCreateValue(eventModel);
        if (state.HasTakenRescheduleTicket)
            yield break;

        bool available = owner.Gold >= 20
            && HasRole(owner, ShinGetterCardRole.Strategy);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => EndlessConveyorHayato(eventModel),
            "ENDLESS_CONVEYOR",
            "HAYATO",
            HoverTipFactory.FromCardWithCardHoverTips<SGC_RescheduleTicket>());
    }

    private static IEnumerable<EventOption> BuildUnrestSiteOptions(UnrestSite eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool isInjured = owner.Creature.CurrentHp < owner.Creature.MaxHp;
        bool benkeiAvailable = isInjured
            && owner.Creature.MaxHp > 5
            && owner.Deck.Cards.Any(card => card is SGC_Indomitable or SGC_IronWall or SGC_Guts);
        yield return CreateConditionalOption(
            eventModel,
            benkeiAvailable,
            () => UnrestSiteBenkei(eventModel),
            "UNREST_SITE",
            "BENKEI");

        bool breathAvailable = isInjured
            && owner.Creature.MaxHp > 4
            && HasRole(owner, ShinGetterCardRole.GetterThreeDefense);
        yield return CreateConditionalOption(
            eventModel,
            breathAvailable,
            () => UnrestSiteBenkeiBreath(eventModel),
            "UNREST_SITE",
            "BENKEI_BREATH",
            HoverTipFactory.FromCardWithCardHoverTips<SGC_PressureBreath>());
    }

    private static IEnumerable<EventOption> BuildLostWispOptions(LostWisp eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool benkeiAvailable = owner.Creature.MaxHp > 5
            && HasRole(owner, ShinGetterCardRole.GetterThreeDefense);
        yield return CreateConditionalOption(
            eventModel,
            benkeiAvailable,
            () => LostWispBenkei(eventModel),
            "LOST_WISP",
            "BENKEI",
            HoverTipFactory.FromRelic<LostWispRelic>());

        bool hayatoAvailable = owner.Creature.MaxHp > 3
            && HasRole(owner, ShinGetterCardRole.Strategy);
        yield return CreateConditionalOption(
            eventModel,
            hayatoAvailable,
            () => LostWispHayato(eventModel),
            "LOST_WISP",
            "HAYATO",
            HoverTipFactory.FromCardWithCardHoverTips<SGC_WispCoordinate>());
    }

    private static IEnumerable<EventOption> BuildDrowningBeaconOptions(DrowningBeacon eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool hasThemeCard = HasRole(owner, ShinGetterCardRole.GetterThreeDefense);
        bool glowwaterAvailable = hasThemeCard
            && owner.Creature.CurrentHp > 12
            && owner.Deck.Cards.Any(card => card.IsUpgradable);
        yield return CreateConditionalOption(
            eventModel,
            glowwaterAvailable,
            () => DrowningBeaconGlowwater(eventModel),
            "DROWNING_BEACON",
            "BENKEI_GLOWWATER",
            HoverTipFactory.FromPotion(ModelDb.Potion<GlowwaterPotion>()));

        bool prismAvailable = hasThemeCard && owner.Creature.CurrentHp > 9;
        yield return CreateConditionalOption(
            eventModel,
            prismAvailable,
            () => DrowningBeaconPrism(eventModel),
            "DROWNING_BEACON",
            "BENKEI_PRISM",
            HoverTipFactory.FromRelic<SGR_BeaconPrism>());
    }

    private static IEnumerable<EventOption> BuildLuminousChoirOptions(LuminousChoir eventModel)
    {
        Player owner = RequireOwner(eventModel);
        int removableCount = owner.Deck.Cards.Count(card => card.IsRemovable);
        bool ryomaAvailable = owner.Creature.CurrentHp > 8
            && removableCount >= 2
            && HasRole(owner, ShinGetterCardRole.GetterRay);
        yield return CreateConditionalOption(
            eventModel,
            ryomaAvailable,
            () => LuminousChoirRyoma(eventModel),
            "LUMINOUS_CHOIR",
            "RYOMA");

        bool hayatoAvailable = owner.Creature.CurrentHp > 5
            && removableCount >= 1
            && HasRole(owner, ShinGetterCardRole.Strategy);
        yield return CreateConditionalOption(
            eventModel,
            hayatoAvailable,
            () => LuminousChoirHayato(eventModel),
            "LUMINOUS_CHOIR",
            "HAYATO",
            HoverTipFactory.FromRelic<SGR_MycelialSilencer>());
    }

    private static IEnumerable<EventOption> BuildColossalFlowerOptions(ColossalFlower eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool hayatoAvailable = owner.Creature.MaxHp > 2
            && HasRole(owner, ShinGetterCardRole.GetterTwoSpeed);
        yield return CreateConditionalOption(
            eventModel,
            hayatoAvailable,
            () => ColossalFlowerHayato(eventModel),
            "COLOSSAL_FLOWER",
            "HAYATO");

        bool ryomaAvailable = owner.Creature.CurrentHp > 6
            && HasRole(owner, ShinGetterCardRole.GetterOneCharge);
        yield return CreateConditionalOption(
            eventModel,
            ryomaAvailable,
            () => ColossalFlowerRyoma(eventModel),
            "COLOSSAL_FLOWER",
            "RYOMA",
            HoverTipFactory.FromCardWithCardHoverTips<SGC_PetalBreakthrough>());
    }

    private static IEnumerable<EventOption> BuildTheFutureOfPotionsOptions(TheFutureOfPotions eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Gold >= 25
            && owner.Potions.Any()
            && (HasRole(owner, ShinGetterCardRole.ResearchEvolution)
                || owner.GetRelic<SGR_ResearchNotes>() != null);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => ShowFutureOfPotionsChoice(eventModel),
            "THE_FUTURE_OF_POTIONS",
            "HAYATO",
            HoverTipFactory.FromPotion(ModelDb.Potion<SGR_LuminescentPulse>()),
            disableOnChosen: false);
    }

    private static IEnumerable<EventOption> BuildAbyssalBathsOptions(AbyssalBaths eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool hasEvolution = HasRole(owner, ShinGetterCardRole.Evolution);
        yield return CreateConditionalOption(
            eventModel,
            hasEvolution && owner.Creature.CurrentHp > 1,
            () => AbyssalBathsTripleRefining(eventModel),
            "ABYSSAL_BATHS",
            "TRIPLE_REFINING",
            HoverTipFactory.FromCardWithCardHoverTips<SGC_Radiated>());
        yield return CreateConditionalOption(
            eventModel,
            hasEvolution && owner.Creature.CurrentHp > 3,
            () => AbyssalBathsTripleCoolant(eventModel),
            "ABYSSAL_BATHS",
            "TRIPLE_COOLANT",
            HoverTipFactory.FromPotion(ModelDb.Potion<SGR_PhaseCoolant>()));
    }

    private static IEnumerable<EventOption> BuildWaterloggedScriptoriumOptions(WaterloggedScriptorium eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool hasResearch = HasRole(owner, ShinGetterCardRole.ResearchEvolution)
            || owner.GetRelic<SGR_ResearchNotes>() != null;
        EnchantmentModel adaptation = ModelDb.Enchantment<SGE_Adaptation>();
        bool adaptationAvailable = hasResearch
            && owner.Gold >= 75
            && owner.Deck.Cards.Any(card => IsNormalCardType(card) && adaptation.CanEnchant(card));
        yield return CreateConditionalOption(
            eventModel,
            adaptationAvailable,
            () => WaterloggedScriptoriumAdaptation(eventModel),
            "WATERLOGGED_SCRIPTORIUM",
            "HAYATO_ADAPTATION",
            HoverTipFactory.FromEnchantment<SGE_Adaptation>());
        yield return CreateConditionalOption(
            eventModel,
            hasResearch && owner.Gold >= 45,
            () => WaterloggedScriptoriumInk(eventModel),
            "WATERLOGGED_SCRIPTORIUM",
            "HAYATO_INK",
            HoverTipFactory.FromPotion(ModelDb.Potion<SGR_AdaptiveInk>()));
    }

    private static Task WelcomeToWongosCitizenExit(WelcomeToWongos eventModel)
    {
        Finish(eventModel, PageKey("WELCOME_TO_WONGOS", "HAYATO"));
        return Task.CompletedTask;
    }

    private static async Task TrashHeapBenkei(TrashHeap eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 12);
        List<RelicModel> choices = TrashHeapRelics
            .ToList()
            .StableShuffle(eventModel.Rng)
            .Take(3)
            .Select(relic => relic.ToMutable())
            .ToList();
        RelicModel? selected = await RelicSelectCmd.FromChooseARelicScreen(owner, choices);
        if (selected != null)
            await RelicCmd.Obtain(selected, owner);
        Finish(eventModel, PageKey("TRASH_HEAP", "BENKEI"));
    }

    private static async Task TinkerTimeHayato(TinkerTime eventModel)
    {
        Player owner = RequireOwner(eventModel);
        Issue89EventState state = Issue89States.GetOrCreateValue(eventModel);
        await PlayerCmd.LoseGold(50m, owner, GoldLossType.Spent);
        state.HasRerolledTinkerTypes = true;
        await (Task)TinkerChooseCardTypeMethod.Invoke(eventModel, null)!;
    }

    private static async Task ReflectionsTripleUnity(Reflections eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseMaxHp(owner, 5);
        CardModel? selected = (await CardSelectCmd.FromDeckGeneric(
                owner,
                new CardSelectorPrefs(SelectionKey("REFLECTIONS", "TRIPLE_UNITY"), 1),
                IsMirrorCopyCandidate))
            .FirstOrDefault();
        if (selected != null)
        {
            CardModel copy = owner.RunState.CloneCard(selected);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(copy, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
        }
        Finish(eventModel, PageKey("REFLECTIONS", "TRIPLE_UNITY"));
    }

    private static async Task DoorsOfLightAndDarkRyoma(DoorsOfLightAndDark eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 6);
        IReadOnlyList<CardModel> colorlessOptions = ModelDb.CardPool<ColorlessCardPool>()
            .GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)
            .Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();
        CardModel? selected = (await CardSelectCmd.FromDeckForTransformation(
                owner,
                new CardSelectorPrefs(SelectionKey("DOORS_OF_LIGHT_AND_DARK", "RYOMA"), 1),
                card => new CardTransformation(card, colorlessOptions)))
            .FirstOrDefault();
        if (selected != null)
        {
            await CardCmd.Transform(
                new[] { new CardTransformation(selected, colorlessOptions) },
                eventModel.Rng,
                CardPreviewStyle.EventLayout);
        }
        Finish(eventModel, PageKey("DOORS_OF_LIGHT_AND_DARK", "RYOMA"));
    }

    private static async Task WellspringRyoma(Wellspring eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseMaxHp(owner, 3);
        List<CardModel> selected = (await CardSelectCmd.FromDeckForRemoval(
                owner,
                new CardSelectorPrefs(SelectionKey("WELLSPRING", "RYOMA"), 1)))
            .ToList();
        await CardPileCmd.RemoveFromDeck(selected);
        Finish(eventModel, PageKey("WELLSPRING", "RYOMA"));
    }

    private static async Task RoomFullOfCheeseBenkei(RoomFullOfCheese eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 7);
        CardCreationOptions creationOptions = CardCreationOptions
            .ForNonCombatWithUniformOdds(
                new[] { owner.Character.CardPool },
                card => card.Rarity == CardRarity.Common)
            .WithFlags(CardCreationFlags.NoRarityModification);
        List<CardCreationResult> choices = CardFactory.CreateForReward(owner, 10, creationOptions).ToList();
        IEnumerable<CardModel> selected = await CardSelectCmd.FromSimpleGridForRewards(
            new BlockingPlayerChoiceContext(),
            choices,
            owner,
            new CardSelectorPrefs(SelectionKey("ROOM_FULL_OF_CHEESE", "BENKEI"), 2));
        foreach (CardModel card in selected)
        {
            if (card.IsUpgradable)
                CardCmd.Upgrade(card);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck));
        }
        Finish(eventModel, PageKey("ROOM_FULL_OF_CHEESE", "BENKEI"));
    }

    private static async Task BugslayerBenkei(Bugslayer eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 5);
        CardModel? rush = (await CardSelectCmd.FromDeckGeneric(
                owner,
                new CardSelectorPrefs(SelectionKey("BUGSLAYER", "BENKEI"), 1),
                IsBugslayerRushCandidate))
            .FirstOrDefault();
        if (rush != null)
        {
            CardCmd.Upgrade(rush, CardPreviewStyle.EventLayout);
            ApplySpiralEnchantment(rush, 2m);
            CardCmd.Preview(rush, 1.2f, CardPreviewStyle.EventLayout);
        }

        CardModel[] eventCards =
        {
            owner.RunState.CreateCard<Exterminate>(owner),
            owner.RunState.CreateCard<Squash>(owner),
        };
        CardModel? selected = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(),
            eventCards,
            owner);
        if (selected != null)
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(selected, PileType.Deck), 2f);
        Finish(eventModel, PageKey("BUGSLAYER", "BENKEI"));
    }

    private static Task ShowRelicTraderOwnedPage(RelicTrader eventModel)
    {
        Player owner = RequireOwner(eventModel);
        List<EventOption> options = GetIssue89TradeableOwnedRelics(owner)
            .Select((relic, index) => CreateDynamicRelicOption(
                eventModel,
                relic,
                () => ShowRelicTraderCandidatePage(eventModel, relic),
                "RELIC_TRADER",
                "CHOOSE_OWNED",
                index))
            .ToList();
        options.Add(new EventOption(
            eventModel,
            () => RestoreRelicTraderInitialPage(eventModel),
            PageOptionKey("RELIC_TRADER", "CHOOSE_OWNED", "BACK")));
        SetState(eventModel, PageKey("RELIC_TRADER", "CHOOSE_OWNED"), options);
        return Task.CompletedTask;
    }

    private static Task ShowRelicTraderCandidatePage(RelicTrader eventModel, RelicModel ownedRelic)
    {
        Player owner = RequireOwner(eventModel);
        List<RelicModel> candidates = GetGrabBagCandidates(owner, ownedRelic.Rarity)
            .ToList()
            .StableShuffle(eventModel.Rng)
            .Take(3)
            .ToList();
        List<EventOption> options = candidates
            .Select((relic, index) => CreateDynamicRelicOption(
                eventModel,
                relic,
                () => CompleteRelicTrade(eventModel, ownedRelic, relic),
                "RELIC_TRADER",
                "CHOOSE_REPLACEMENT",
                index))
            .ToList();
        options.Add(new EventOption(
            eventModel,
            () => ShowRelicTraderOwnedPage(eventModel),
            PageOptionKey("RELIC_TRADER", "CHOOSE_REPLACEMENT", "BACK")));
        SetState(eventModel, PageKey("RELIC_TRADER", "CHOOSE_REPLACEMENT"), options);
        return Task.CompletedTask;
    }

    private static async Task CompleteRelicTrade(
        RelicTrader eventModel,
        RelicModel ownedRelic,
        RelicModel replacement)
    {
        Player owner = RequireOwner(eventModel);
        await PlayerCmd.LoseGold(75m, owner, GoldLossType.Spent);
        await RelicCmd.Remove(ownedRelic);
        await RelicCmd.Obtain(replacement.ToMutable(), owner);
        Finish(eventModel, PageKey("RELIC_TRADER", "HAYATO"));
    }

    private static Task RestoreRelicTraderInitialPage(RelicTrader eventModel)
    {
        var options = (IReadOnlyList<EventOption>)RelicTraderGenerateOptionsMethod.Invoke(eventModel, null)!;
        SetState(eventModel, "RELIC_TRADER.pages.INITIAL.description", options);
        return Task.CompletedTask;
    }

    private static async Task EndlessConveyorHayato(EndlessConveyor eventModel)
    {
        Player owner = RequireOwner(eventModel);
        Issue89EventState state = Issue89States.GetOrCreateValue(eventModel);
        await PlayerCmd.LoseGold(20m, owner, GoldLossType.Spent);
        await AddEventCard<SGC_RescheduleTicket>(owner);
        state.HasTakenRescheduleTicket = true;
        var options = (IReadOnlyList<EventOption>)EndlessConveyorGenerateOptionsMethod.Invoke(eventModel, null)!;
        SetState(eventModel, "ENDLESS_CONVEYOR.pages.INITIAL.description", options);
    }

    private static async Task UnrestSiteBenkei(UnrestSite eventModel)
    {
        Player owner = RequireOwner(eventModel);
        int missingHp = owner.Creature.MaxHp - owner.Creature.CurrentHp;
        await LoseMaxHp(owner, 5);
        await CreatureCmd.Heal(owner.Creature, Math.Floor(missingHp * 0.5m));
        Finish(eventModel, PageKey("UNREST_SITE", "BENKEI"));
    }

    private static async Task UnrestSiteBenkeiBreath(UnrestSite eventModel)
    {
        Player owner = RequireOwner(eventModel);
        int missingHp = owner.Creature.MaxHp - owner.Creature.CurrentHp;
        await LoseMaxHp(owner, 4);
        await CreatureCmd.Heal(owner.Creature, Math.Floor(missingHp * 0.25m));
        await AddEventCard<SGC_PressureBreath>(owner);
        Finish(eventModel, PageKey("UNREST_SITE", "BENKEI_BREATH"));
    }

    private static async Task LostWispBenkei(LostWisp eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseMaxHp(owner, 5);
        await RelicCmd.Obtain<LostWispRelic>(owner);
        Finish(eventModel, PageKey("LOST_WISP", "BENKEI"));
    }

    private static async Task LostWispHayato(LostWisp eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseMaxHp(owner, 3);
        await AddEventCard<SGC_WispCoordinate>(owner);
        Finish(eventModel, PageKey("LOST_WISP", "HAYATO"));
    }

    private static async Task DrowningBeaconGlowwater(DrowningBeacon eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 12);
        await OfferPotion<GlowwaterPotion>(owner);
        List<CardModel> upgradable = owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        CardModel? selected = eventModel.Rng.NextItem(upgradable);
        if (selected != null)
            CardCmd.Upgrade(selected, CardPreviewStyle.EventLayout);
        Finish(eventModel, PageKey("DROWNING_BEACON", "BENKEI_GLOWWATER"));
    }

    private static async Task DrowningBeaconPrism(DrowningBeacon eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 9);
        await RelicCmd.Obtain<SGR_BeaconPrism>(owner);
        Finish(eventModel, PageKey("DROWNING_BEACON", "BENKEI_PRISM"));
    }

    private static async Task LuminousChoirRyoma(LuminousChoir eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 8);
        List<CardModel> cards = (await CardSelectCmd.FromDeckForRemoval(
                owner,
                new CardSelectorPrefs(SelectionKey("LUMINOUS_CHOIR", "RYOMA"), 2)))
            .ToList();
        await CardPileCmd.RemoveFromDeck(cards);
        Finish(eventModel, PageKey("LUMINOUS_CHOIR", "RYOMA"));
    }

    private static async Task LuminousChoirHayato(LuminousChoir eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 5);
        List<CardModel> cards = (await CardSelectCmd.FromDeckForRemoval(
                owner,
                new CardSelectorPrefs(SelectionKey("LUMINOUS_CHOIR", "HAYATO"), 1)))
            .ToList();
        await CardPileCmd.RemoveFromDeck(cards);
        await RelicCmd.Obtain<SGR_MycelialSilencer>(owner);
        Finish(eventModel, PageKey("LUMINOUS_CHOIR", "HAYATO"));
    }

    private static async Task ColossalFlowerHayato(ColossalFlower eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseMaxHp(owner, 2);
        await PlayerCmd.GainGold(90m, owner);
        ColossalFlowerDigCountField.SetValue(eventModel, 2);
        Finish(eventModel, PageKey("COLOSSAL_FLOWER", "HAYATO"));
    }

    private static async Task ColossalFlowerRyoma(ColossalFlower eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 6);
        await AddEventCard<SGC_PetalBreakthrough>(owner);
        Finish(eventModel, PageKey("COLOSSAL_FLOWER", "RYOMA"));
    }

    private static Task ShowFutureOfPotionsChoice(TheFutureOfPotions eventModel)
    {
        Player owner = RequireOwner(eventModel);
        List<EventOption> options = owner.Potions
            .Select((potion, index) =>
            {
                LocString title = new("events", $"{LocPrefix}.THE_FUTURE_OF_POTIONS.pages.CHOOSE_POTION.options.POTION.title");
                LocString description = new("events", $"{LocPrefix}.THE_FUTURE_OF_POTIONS.pages.CHOOSE_POTION.options.POTION.description");
                title.Add("Potion", potion.Title.GetFormattedText());
                description.Add("Potion", potion.Title.GetFormattedText());
                return new EventOption(
                    eventModel,
                    () => TheFutureOfPotionsHayato(eventModel, potion),
                    title,
                    description,
                    PageOptionKey("THE_FUTURE_OF_POTIONS", "CHOOSE_POTION", $"POTION_{index}"),
                    potion.HoverTips).ThatHasDynamicTitle();
            })
            .ToList();
        SetState(eventModel, PageKey("THE_FUTURE_OF_POTIONS", "CHOOSE_POTION"), options);
        return Task.CompletedTask;
    }

    private static async Task TheFutureOfPotionsHayato(
        TheFutureOfPotions eventModel,
        PotionModel potion)
    {
        Player owner = RequireOwner(eventModel);
        await PlayerCmd.LoseGold(25m, owner, GoldLossType.Spent);
        await PotionCmd.Discard(potion);
        await OfferPotion<SGR_LuminescentPulse>(owner);
        Finish(eventModel, PageKey("THE_FUTURE_OF_POTIONS", "HAYATO"));
    }

    private static async Task AbyssalBathsTripleRefining(AbyssalBaths eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await CreatureCmd.GainMaxHp(owner.Creature, 4m);
        await LoseHp(owner, 5);
        await AddEventCard<SGC_Radiated>(owner);
        Finish(eventModel, PageKey("ABYSSAL_BATHS", "TRIPLE_REFINING"));
    }

    private static async Task AbyssalBathsTripleCoolant(AbyssalBaths eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 3);
        await OfferPotion<SGR_PhaseCoolant>(owner);
        Finish(eventModel, PageKey("ABYSSAL_BATHS", "TRIPLE_COOLANT"));
    }

    private static async Task WaterloggedScriptoriumAdaptation(WaterloggedScriptorium eventModel)
    {
        Player owner = RequireOwner(eventModel);
        EnchantmentModel adaptation = ModelDb.Enchantment<SGE_Adaptation>();
        await PlayerCmd.LoseGold(75m, owner, GoldLossType.Spent);
        CardModel? selected = (await CardSelectCmd.FromDeckForEnchantment(
                owner,
                adaptation,
                1,
                card => card != null && IsNormalCardType(card),
                new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1)))
            .FirstOrDefault();
        if (selected != null)
        {
            CardCmd.Enchant<SGE_Adaptation>(selected, 1m);
            NCardEnchantVfx? vfx = NCardEnchantVfx.Create(selected);
            if (vfx != null)
                NRun.Instance?.GlobalUi?.CardPreviewContainer.AddChildSafely(vfx);
        }
        Finish(eventModel, PageKey("WATERLOGGED_SCRIPTORIUM", "HAYATO_ADAPTATION"));
    }

    private static async Task WaterloggedScriptoriumInk(WaterloggedScriptorium eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await PlayerCmd.LoseGold(45m, owner, GoldLossType.Spent);
        await OfferPotion<SGR_AdaptiveInk>(owner);
        Finish(eventModel, PageKey("WATERLOGGED_SCRIPTORIUM", "HAYATO_INK"));
    }

    private static bool HasRole(Player owner, ShinGetterCardRole role) =>
        ShinGetterCardRoleRegistry.Has(owner.Deck.Cards, role);

    private static bool IsMirrorCopyCandidate(CardModel card) =>
        card.Type is not CardType.Curse and not CardType.Status
        && card.Rarity is CardRarity.Basic or CardRarity.Common or CardRarity.Uncommon;

    private static bool IsBugslayerRushCandidate(CardModel card) =>
        card is SGC_GetterRush && !card.IsUpgraded && card.Enchantment == null;

    private static bool IsNormalCardType(CardModel card) =>
        card.Type is CardType.Attack or CardType.Skill or CardType.Power;

    private static IEnumerable<RelicModel> GetIssue89TradeableOwnedRelics(Player owner) =>
        owner.Relics.Where(relic =>
            relic is not SGR_EmperorsFragment
            && relic.IsTradable
            && relic.Rarity is RelicRarity.Common or RelicRarity.Uncommon or RelicRarity.Rare
            && GetGrabBagCandidates(owner, relic.Rarity).Take(3).Count() == 3);

    private static IEnumerable<RelicModel> GetGrabBagCandidates(Player owner, RelicRarity rarity)
    {
        var serialized = owner.RelicGrabBag.ToSerializable();
        if (!serialized.RelicIdLists.TryGetValue(rarity, out List<ModelId>? ids))
            return Array.Empty<RelicModel>();

        HashSet<ModelId> ownedIds = owner.Relics.Select(relic => relic.Id).ToHashSet();
        return ids
            .Select(id => ModelDb.GetByIdOrNull<RelicModel>(id))
            .Where(relic => relic != null
                && relic.Rarity == rarity
                && relic.IsAllowed(owner.RunState)
                && !ownedIds.Contains(relic.Id))
            .Cast<RelicModel>()
            .DistinctBy(relic => relic.Id)
            .ToList();
    }

    private static EventOption CreateDynamicRelicOption(
        EventModel eventModel,
        RelicModel relic,
        Func<Task> onChosen,
        string eventName,
        string pageName,
        int index)
    {
        LocString title = new("events", $"{LocPrefix}.{eventName}.pages.{pageName}.options.RELIC.title");
        LocString description = new("events", $"{LocPrefix}.{eventName}.pages.{pageName}.options.RELIC.description");
        title.Add("Relic", relic.Title.GetFormattedText());
        description.Add("Relic", relic.Title.GetFormattedText());
        return new EventOption(
            eventModel,
            onChosen,
            title,
            description,
            PageOptionKey(eventName, pageName, $"RELIC_{index}"),
            relic.HoverTips).ThatHasDynamicTitle();
    }

    private static async Task AddEventCard<T>(Player owner) where T : CardModel
    {
        CardModel card = owner.RunState.CreateCard<T>(owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, PileType.Deck), 1.2f, CardPreviewStyle.EventLayout);
    }

    private static Task LoseMaxHp(Player owner, int amount) =>
        CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            amount,
            isFromCard: false);

    private static Task OfferPotion<T>(Player owner) where T : PotionModel =>
        RewardsCmd.OfferCustom(owner, new List<Reward>
        {
            new PotionReward(ModelDb.Potion<T>().ToMutable(), owner),
        });

    private static void SetState(
        EventModel eventModel,
        string descriptionKey,
        IEnumerable<EventOption> options)
    {
        SetEventStateMethod.Invoke(
            eventModel,
            new object[] { new LocString("events", descriptionKey), options });
    }
}
