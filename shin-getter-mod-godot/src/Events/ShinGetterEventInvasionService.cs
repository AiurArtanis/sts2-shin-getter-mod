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
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Potions;
using ShinGetterMod.Models.Relics;
using ByrdpipRelic = MegaCrit.Sts2.Core.Models.Relics.Byrdpip;

namespace ShinGetterMod.Events;

internal static class ShinGetterEventInvasionService
{
    private enum PendingBattleSetup
    {
        ByrdonisNest,
        Trial,
    }

    private const string LocPrefix = "SHIN_GETTER_EVENT_INVASION";

    private static readonly MethodInfo SetEventFinishedMethod =
        AccessTools.Method(typeof(EventModel), "SetEventFinished", new[] { typeof(LocString) })
        ?? throw new MissingMethodException(typeof(EventModel).FullName, "SetEventFinished");

    private static readonly MethodInfo SetEventStateMethod =
        AccessTools.Method(typeof(EventModel), "SetEventState", new[]
        {
            typeof(LocString),
            typeof(IEnumerable<EventOption>),
        }) ?? throw new MissingMethodException(typeof(EventModel).FullName, "SetEventState");

    private static readonly MethodInfo EnterCombatWithoutExitingEventMethod =
        AccessTools.Method(typeof(EventModel), "EnterCombatWithoutExitingEvent", new[]
        {
            typeof(EncounterModel),
            typeof(IReadOnlyList<Reward>),
            typeof(bool),
        }) ?? throw new MissingMethodException(typeof(EventModel).FullName, "EnterCombatWithoutExitingEvent");

    private static readonly HashSet<EventModel> EventsEnteringSinglePlayerCombat = new();
    private static readonly Dictionary<Player, (PendingBattleSetup Setup, EncounterModel Encounter)>
        PendingBattleSetups = new();

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
            ByrdonisNest byrdonisNest => BuildByrdonisNestOptions(byrdonisNest),
            InfestedAutomaton infestedAutomaton => BuildInfestedAutomatonOptions(infestedAutomaton),
            TheLegendsWereTrue legendsWereTrue => BuildTheLegendsWereTrueOptions(legendsWereTrue),
            Trial trial => BuildTrialOptions(trial, options),
            SunkenStatue sunkenStatue => BuildSunkenStatueOptions(sunkenStatue),
            SpiralingWhirlpool spiralingWhirlpool => BuildSpiralingWhirlpoolOptions(spiralingWhirlpool),
            RoundTeaParty roundTeaParty => BuildRoundTeaPartyOptions(roundTeaParty),
            RanwidTheElder ranwidTheElder => BuildRanwidTheElderOptions(ranwidTheElder),
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

        bool isInitialPage = options.Any(option =>
            option.TextKey.Contains(".pages.INITIAL.options.", StringComparison.Ordinal));
        bool isTrialVerdictPage = eventModel is Trial && options.Any(option =>
            option.TextKey.StartsWith("TRIAL.pages.MERCHANT.options.", StringComparison.Ordinal)
            || option.TextKey.StartsWith("TRIAL.pages.NOBLE.options.", StringComparison.Ordinal)
            || option.TextKey.StartsWith("TRIAL.pages.NONDESCRIPT.options.", StringComparison.Ordinal));
        if (!isInitialPage && !isTrialVerdictPage)
            return false;

        return owner.GetRelic<SGR_GetterFurnace>()?.EventInvasionEnabled
            ?? owner.GetRelic<SGR_EmperorsFragment>()?.EventInvasionEnabled
            ?? false;
    }

    internal static bool IsEnteringSinglePlayerEventCombat(EventModel eventModel) =>
        EventsEnteringSinglePlayerCombat.Contains(eventModel);

    internal static async Task ApplyPendingPreCombatSetup(Player owner)
    {
        if (!PendingBattleSetups.TryGetValue(
                owner,
                out (PendingBattleSetup Setup, EncounterModel Encounter) pending)
            || pending.Setup != PendingBattleSetup.ByrdonisNest)
            return;

        PendingBattleSetups.Remove(owner);
        var combatState = owner.Creature.CombatState;
        if (combatState == null || !ReferenceEquals(combatState.Encounter, pending.Encounter))
            return;

        if (combatState.Encounter is not ByrdonisElite)
            return;

        Creature? byrdonis = combatState.Enemies
            .FirstOrDefault(creature => creature.Monster is Byrdonis);
        if (byrdonis != null)
            await CreatureCmd.Stun(byrdonis, _ => Task.CompletedTask);
    }

    internal static async Task ApplyPendingTrialAfterHandDraw(
        PlayerChoiceContext choiceContext,
        Player owner)
    {
        if (!PendingBattleSetups.TryGetValue(
                owner,
                out (PendingBattleSetup Setup, EncounterModel Encounter) pending)
            || pending.Setup != PendingBattleSetup.Trial)
            return;

        PendingBattleSetups.Remove(owner);
        var combatState = owner.Creature.CombatState;
        PlayerCombatState? playerCombatState = owner.PlayerCombatState;
        if (combatState == null
            || !ReferenceEquals(combatState.Encounter, pending.Encounter)
            || combatState.Encounter is not KnightsElite
            || playerCombatState == null
            || playerCombatState.TurnNumber != 1)
            return;

        List<CardModel> cardsToPlay = owner.Deck.Cards
            .Where(IsTrialSpiritCommand)
            .Select(deckCard => playerCombatState.AllCards.FirstOrDefault(
                combatCard => ReferenceEquals(combatCard.DeckVersion, deckCard)))
            .Where(card => card != null)
            .Cast<CardModel>()
            .ToList();
        foreach (CardModel card in cardsToPlay)
        {
            card.SetToFreeThisCombat();
            await CardCmd.AutoPlay(
                choiceContext,
                card,
                null);
        }
    }

    internal static async Task ResumeByrdonisNest(
        ByrdonisNest eventModel,
        AbstractRoom exitedRoom,
        Task originalTask)
    {
        await originalTask;
        if (exitedRoom is not CombatRoom combatRoom
            || !combatRoom.IsPreFinished
            || !combatRoom.ShouldResumeParentEventAfterCombat
            || combatRoom.ParentEventId != eventModel.Id
            || combatRoom.Encounter is not ByrdonisElite)
        {
            return;
        }

        Player owner = RequireOwner(eventModel);
        if (!owner.Deck.Cards.Any(card => card is ByrdonisEgg))
            return;

        Finish(eventModel, PageKey("BYRDONIS_NEST", "RYOMA_HATCH"));
        await RelicCmd.Obtain<ByrdpipRelic>(owner);
    }

    private static IEnumerable<EventOption> BuildTeaMasterOptions(TeaMaster eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool ryomaAvailable = owner.Creature.CurrentHp > 5
            && HasAnyCard<SGC_FightingSpirit, SGC_Ki>(owner);
        EventOption ryoma = CreateConditionalOption(
            eventModel,
            ryomaAvailable,
            () => TeaMasterRyoma(eventModel),
            "TEA_MASTER",
            "RYOMA",
            HoverTipFactory.FromRelicExcludingItself<EmberTea>());
        if (ryomaAvailable)
            ryoma.ThatDoesDamage(5);
        yield return ryoma;

        bool benkeiAvailable = owner.Gold >= 100;
        IHoverTip[] hovers = HoverTipFactory.FromRelicExcludingItself<BoneTea>()
            .Concat(HoverTipFactory.FromRelicExcludingItself<EmberTea>())
            .ToArray();
        yield return CreateConditionalOption(
            eventModel,
            benkeiAvailable,
            () => TeaMasterBenkei(eventModel),
            "TEA_MASTER",
            "BENKEI",
            hovers,
            disableOnChosen: false);
    }

    private static IEnumerable<EventOption> BuildSlipperyBridgeOptions(SlipperyBridge eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool ryomaAvailable = owner.Creature.CurrentHp > 5
            && HasAnyCard<SGC_Ki, SGC_Spirit, SGC_SuperKi>(owner);
        EventOption ryoma = CreateConditionalOption(
            eventModel,
            ryomaAvailable,
            () => SlipperyBridgeRyoma(eventModel),
            "SLIPPERY_BRIDGE",
            "RYOMA");
        if (ryomaAvailable)
            ryoma.ThatDoesDamage(5);
        yield return ryoma;

        bool hayatoAvailable = owner.Deck.Cards.Any(card => card.IsRemovable)
            && HasAnyCard<SGC_Acceleration, SGC_ShedLoad>(owner);
        yield return CreateConditionalOption(
            eventModel,
            hayatoAvailable,
            () => SlipperyBridgeHayato(eventModel),
            "SLIPPERY_BRIDGE",
            "HAYATO");
    }

    private static IEnumerable<EventOption> BuildSpiritGrafterOptions(SpiritGrafter eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = Enum.GetValues<ShinGetterForm>()
            .Where(form => form != ShinGetterForm.None)
            .All(form => HasExclusiveFormCard(owner, form));
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => SpiritGrafterTripleUnity(eventModel),
            "SPIRIT_GRAFTER",
            "TRIPLE_UNITY",
            HoverTipFactory.FromCardWithCardHoverTips<SGC_TripleUnity>());
    }

    private static IEnumerable<EventOption> BuildWoodCarvingsOptions(WoodCarvings eventModel)
    {
        yield return new EventOption(
            eventModel,
            () => WoodCarvingsTripleCarving(eventModel),
            Key("WOOD_CARVINGS", "TRIPLE_CARVING"),
            HoverTipFactory.FromRelic<SGR_TripleWoodCarving>());
    }

    private static IEnumerable<EventOption> BuildThisOrThatOptions(ThisOrThat eventModel)
    {
        yield return new EventOption(
            eventModel,
            () => ThisOrThatRyoma(eventModel),
            Key("THIS_OR_THAT", "RYOMA"));

        Player owner = RequireOwner(eventModel);
        bool hayatoAvailable = owner.Gold >= 75
            && HasAnyCard<SGC_Insight, SGC_BackupPlan>(owner);
        yield return CreateConditionalOption(
            eventModel,
            hayatoAvailable,
            () => ThisOrThatHayato(eventModel),
            "THIS_OR_THAT",
            "HAYATO");
    }

    private static IEnumerable<EventOption> BuildAmalgamatorOptions(Amalgamator eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool ryomaAvailable = owner.Deck.Cards.Any(IsRyomaExtraRemovalCandidate);
        yield return CreateConditionalOption(
            eventModel,
            ryomaAvailable,
            () => AmalgamatorRyoma(eventModel),
            "AMALGAMATOR",
            "RYOMA");

        bool benkeiAvailable = owner.Gold >= 100
            && owner.Deck.Cards.Count(card => card is SGC_Strike && card.IsRemovable) >= 2
            && owner.Deck.Cards.Count(card => card is SGC_Defend && card.IsRemovable) >= 2;
        IHoverTip[] hovers = HoverTipFactory.FromCardWithCardHoverTips<UltimateStrike>()
            .Concat(HoverTipFactory.FromCardWithCardHoverTips<UltimateDefend>())
            .ToArray();
        yield return CreateConditionalOption(
            eventModel,
            benkeiAvailable,
            () => AmalgamatorBenkei(eventModel),
            "AMALGAMATOR",
            "BENKEI",
            hovers);
    }

    private static IEnumerable<EventOption> BuildByrdonisNestOptions(ByrdonisNest eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool benkeiAvailable = owner.Deck.Cards.Any(card => card.IsUpgradable);
        yield return CreateConditionalOption(
            eventModel,
            benkeiAvailable,
            () => ByrdonisNestBenkei(eventModel),
            "BYRDONIS_NEST",
            "BENKEI");

        bool ryomaAvailable = HasAnyCard<SGC_HotBlood, SGC_FightingSpirit, SGC_SuperKi>(owner);
        yield return CreateConditionalOption(
            eventModel,
            ryomaAvailable,
            () => ByrdonisNestRyoma(eventModel),
            "BYRDONIS_NEST",
            "RYOMA",
            HoverTipFactory.FromCardWithCardHoverTips<ByrdonisEgg>()
                .Concat(HoverTipFactory.FromRelic<ByrdpipRelic>()));
    }

    private static IEnumerable<EventOption> BuildInfestedAutomatonOptions(InfestedAutomaton eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.MaxHp > 4
            && HasAnyCard<SGC_Jammer, SGC_Insight>(owner);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => InfestedAutomatonHayato(eventModel),
            "INFESTED_AUTOMATON",
            "HAYATO");
    }

    private static IEnumerable<EventOption> BuildTheLegendsWereTrueOptions(TheLegendsWereTrue eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool ryomaAvailable = owner.GetRelic<SGR_GoodCitizenCard>() == null;
        yield return CreateConditionalOption(
            eventModel,
            ryomaAvailable,
            () => TheLegendsWereTrueRyoma(eventModel),
            "THE_LEGENDS_WERE_TRUE",
            "RYOMA",
            HoverTipFactory.FromRelic<SGR_GoodCitizenCard>());

        bool hayatoAvailable = owner.Gold >= 35
            && HasAnyCard<SGC_GetterClaw, SGC_SpiralDrill, SGC_TornadoDrill>(owner);
        yield return CreateConditionalOption(
            eventModel,
            hayatoAvailable,
            () => TheLegendsWereTrueHayato(eventModel),
            "THE_LEGENDS_WERE_TRUE",
            "HAYATO",
            new[] { HoverTipFactory.FromPotion<SGR_GetterColdBrew>() });
    }

    private static IEnumerable<EventOption> BuildTrialOptions(
        Trial eventModel,
        IReadOnlyList<EventOption> currentOptions)
    {
        bool isVerdictPage = currentOptions.Any(option =>
            option.TextKey.StartsWith("TRIAL.pages.MERCHANT.options.", StringComparison.Ordinal)
            || option.TextKey.StartsWith("TRIAL.pages.NOBLE.options.", StringComparison.Ordinal)
            || option.TextKey.StartsWith("TRIAL.pages.NONDESCRIPT.options.", StringComparison.Ordinal));
        if (!isVerdictPage)
            yield break;

        Player owner = RequireOwner(eventModel);
        bool available = owner.Deck.Cards.Any(IsTrialSpiritCommand);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => TrialRyoma(eventModel),
            "TRIAL",
            "RYOMA");
    }

    private static IEnumerable<EventOption> BuildSunkenStatueOptions(SunkenStatue eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.CurrentHp > 7
            && HasAnyCard<SGC_Indomitable, SGC_IronWall>(owner);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => SunkenStatueBenkei(eventModel),
            "SUNKEN_STATUE",
            "BENKEI");
    }

    private static IEnumerable<EventOption> BuildSpiralingWhirlpoolOptions(SpiralingWhirlpool eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = HasAnyCard<SGC_TornadoDrill, SGC_SpiralDrill>(owner)
            && owner.Deck.Cards.Any(card => card.IsUpgraded);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => SpiralingWhirlpoolHayato(eventModel),
            "SPIRALING_WHIRLPOOL",
            "HAYATO",
            HoverTipFactory.FromEnchantment<Spiral>());
    }

    private static IEnumerable<EventOption> BuildRoundTeaPartyOptions(RoundTeaParty eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Creature.CurrentHp > 11
            && owner.Deck.Cards.Any(card =>
                (card is SGC_Ki or SGC_FightingSpirit) && card.IsUpgradable);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => RoundTeaPartyRyoma(eventModel),
            "ROUND_TEA_PARTY",
            "RYOMA");
    }

    private static IEnumerable<EventOption> BuildRanwidTheElderOptions(RanwidTheElder eventModel)
    {
        Player owner = RequireOwner(eventModel);
        bool available = owner.Deck.Cards.Any(card => card is SGC_SaotomeBlueprint);
        yield return CreateConditionalOption(
            eventModel,
            available,
            () => RanwidTheElderRyoma(eventModel),
            "RANWID_THE_ELDER",
            "RYOMA");
    }

    private static async Task TeaMasterRyoma(TeaMaster eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 5);
        await RelicCmd.Obtain<EmberTea>(owner);
        Finish(eventModel, PageKey("TEA_MASTER", "RYOMA"));
    }

    private static async Task TeaMasterBenkei(TeaMaster eventModel)
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
        Finish(eventModel, PageKey("TEA_MASTER", "BENKEI"));
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

    private static async Task AmalgamatorBenkei(Amalgamator eventModel)
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
        Finish(eventModel, PageKey("AMALGAMATOR", "BENKEI"));
    }

    private static async Task ByrdonisNestBenkei(ByrdonisNest eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 6);
        List<CardModel> candidates = owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        int upgradeCount = Math.Min(3, candidates.Count);
        for (int index = 0; index < upgradeCount; index++)
        {
            CardModel card = eventModel.Rng.NextItem(candidates)!;
            candidates.Remove(card);
            CardCmd.Upgrade(card, CardPreviewStyle.EventLayout);
        }
        Finish(eventModel, PageKey("BYRDONIS_NEST", "BENKEI"));
    }

    private static async Task ByrdonisNestRyoma(ByrdonisNest eventModel)
    {
        Player owner = RequireOwner(eventModel);
        CardModel byrdonisEgg = owner.RunState.CreateCard<ByrdonisEgg>(owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(byrdonisEgg, PileType.Deck), 2f);
        BeginEventBattle(
            eventModel,
            "BYRDONIS_NEST",
            "RYOMA",
            ModelDb.Encounter<ByrdonisElite>().ToMutable(),
            Array.Empty<Reward>(),
            PendingBattleSetup.ByrdonisNest,
            shouldResumeAfterCombat: true);
    }

    private static async Task InfestedAutomatonHayato(InfestedAutomaton eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            4,
            isFromCard: false);

        IReadOnlyList<CardPoolModel> pools = new[] { owner.Character.CardPool };
        CardCreationOptions powerOptions = CardCreationOptions.ForNonCombatWithDefaultOdds(
            pools,
            card => card.Type == CardType.Power);
        List<CardCreationResult> candidates = CardFactory.CreateForReward(owner, 2, powerOptions).ToList();
        HashSet<ModelId> usedIds = candidates.Select(result => result.Card.Id).ToHashSet();

        CardCreationOptions zeroCostOptions = CardCreationOptions.ForNonCombatWithDefaultOdds(
                pools,
                card => card.EnergyCost is { Canonical: 0, CostsX: false }
                    && !usedIds.Contains(card.Id))
            .WithFlags(CardCreationFlags.NoCardPoolModifications);
        candidates.AddRange(CardFactory.CreateForReward(owner, 2, zeroCostOptions));

        CardSelectorPrefs prefs = new(
            SelectionKey("INFESTED_AUTOMATON", "HAYATO"),
            1);
        CardModel? selected = (await CardSelectCmd.FromSimpleGridForRewards(
            new BlockingPlayerChoiceContext(),
            candidates,
            owner,
            prefs)).FirstOrDefault();
        if (selected != null)
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(selected, PileType.Deck));

        Finish(eventModel, PageKey("INFESTED_AUTOMATON", "HAYATO"));
    }

    private static async Task TheLegendsWereTrueRyoma(TheLegendsWereTrue eventModel)
    {
        await RelicCmd.Obtain<SGR_GoodCitizenCard>(RequireOwner(eventModel));
        Finish(eventModel, PageKey("THE_LEGENDS_WERE_TRUE", "RYOMA"));
    }

    private static async Task TheLegendsWereTrueHayato(TheLegendsWereTrue eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await PlayerCmd.LoseGold(35, owner, GoldLossType.Spent);
        await RewardsCmd.OfferCustom(owner, new List<Reward>
        {
            new PotionReward(ModelDb.Potion<SGR_GetterColdBrew>().ToMutable(), owner),
        });
        Finish(eventModel, PageKey("THE_LEGENDS_WERE_TRUE", "HAYATO"));
    }

    private static Task TrialRyoma(Trial eventModel)
    {
        BeginEventBattle(
            eventModel,
            "TRIAL",
            "RYOMA",
            ModelDb.Encounter<KnightsElite>().ToMutable(),
            Array.Empty<Reward>(),
            PendingBattleSetup.Trial);
        return Task.CompletedTask;
    }

    private static async Task SunkenStatueBenkei(SunkenStatue eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await CreatureCmd.LoseMaxHp(
            new ThrowingPlayerChoiceContext(),
            owner.Creature,
            7,
            isFromCard: false);
        decimal gold = Math.Round(
            eventModel.DynamicVars.Gold.BaseValue * 1.8m,
            MidpointRounding.AwayFromZero);
        await PlayerCmd.GainGold(gold, owner);
        Finish(eventModel, PageKey("SUNKEN_STATUE", "BENKEI"));
    }

    private static async Task SpiralingWhirlpoolHayato(SpiralingWhirlpool eventModel)
    {
        Player owner = RequireOwner(eventModel);
        CardSelectorPrefs prefs = new(
            SelectionKey("SPIRALING_WHIRLPOOL", "HAYATO"),
            1);
        CardModel? selected = (await CardSelectCmd.FromDeckGeneric(
            owner,
            prefs,
            card => card.IsUpgraded)).FirstOrDefault();
        if (selected == null)
            return;

        CardCmd.Downgrade(selected);
        CardCmd.Preview(selected, 1.2f, CardPreviewStyle.EventLayout);

        List<CardModel> drillCards = owner.Deck.Cards
            .Where(card => (card is SGC_TornadoDrill or SGC_SpiralDrill)
                && card.Enchantment == null)
            .ToList();
        foreach (CardModel card in drillCards)
            ApplySpiralEnchantment(card);
        if (drillCards.Count > 0)
            CardCmd.Preview(drillCards, 1.2f, CardPreviewStyle.EventLayout);

        Finish(eventModel, PageKey("SPIRALING_WHIRLPOOL", "HAYATO"));
    }

    private static async Task RoundTeaPartyRyoma(RoundTeaParty eventModel)
    {
        Player owner = RequireOwner(eventModel);
        await LoseHp(owner, 11);
        List<CardModel> candidates = owner.Deck.Cards
            .Where(card => (card is SGC_Ki or SGC_FightingSpirit) && card.IsUpgradable)
            .ToList();
        CardModel? card = eventModel.Rng.NextItem(candidates);
        if (card != null)
            CardCmd.Upgrade(card, CardPreviewStyle.EventLayout);
        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(owner).ToMutable(), owner);
        Finish(eventModel, PageKey("ROUND_TEA_PARTY", "RYOMA"));
    }

    private static Task RanwidTheElderRyoma(RanwidTheElder eventModel)
    {
        EventOption chooseRelic = new(
            eventModel,
            () => RanwidTheElderChooseRelic(eventModel),
            PageOptionKey("RANWID_THE_ELDER", "RYOMA", "CHOOSE_RELIC"),
            disableOnChosen: true,
            isProceed: true);
        SetEventStateMethod.Invoke(
            eventModel,
            new object[]
            {
                new LocString("events", PageKey("RANWID_THE_ELDER", "RYOMA")),
                new[] { chooseRelic },
            });
        return Task.CompletedTask;
    }

    private static async Task RanwidTheElderChooseRelic(RanwidTheElder eventModel)
    {
        Player owner = RequireOwner(eventModel);
        SGC_SaotomeBlueprint? blueprint = owner.Deck.Cards
            .OfType<SGC_SaotomeBlueprint>()
            .OrderBy(card => card.IsUpgraded)
            .FirstOrDefault();
        if (blueprint == null)
            return;

        RelicModel[] choices =
        {
            RelicFactory.PullNextRelicFromFront(owner).ToMutable(),
            RelicFactory.PullNextRelicFromFront(owner).ToMutable(),
        };
        RelicModel? selected;
        do
        {
            selected = await RelicSelectCmd.FromChooseARelicScreen(owner, choices);
        }
        while (selected == null);

        await CardPileCmd.RemoveFromDeck(blueprint);
        await RelicCmd.Obtain(selected, owner);
        Finish(eventModel, PageKey("RANWID_THE_ELDER", "RYOMA_RESULT"));
    }

    private static void ApplySpiralEnchantment(CardModel card)
    {
        EnchantmentModel spiral = ModelDb.Enchantment<Spiral>().ToMutable();
        card.EnchantInternal(spiral, 1m);
        spiral.ModifyCard();
        card.FinalizeUpgradeInternal();
        card.Owner.RunState.CurrentMapPointHistoryEntry?
            .GetEntry(card.Owner.NetId)
            .CardsEnchanted.Add(new CardEnchantmentHistoryEntry(card, spiral.Id));
    }

    private static void BeginEventBattle(
        EventModel eventModel,
        string eventName,
        string pageName,
        EncounterModel encounter,
        IReadOnlyList<Reward> extraRewards,
        PendingBattleSetup setup,
        bool shouldResumeAfterCombat = false)
    {
        EventOption startFight = new(
            eventModel,
            () => StartEventBattle(
                eventModel,
                encounter,
                extraRewards,
                setup,
                shouldResumeAfterCombat),
            PageOptionKey(eventName, pageName, "START_FIGHT"),
            disableOnChosen: true,
            isProceed: true);
        SetEventStateMethod.Invoke(
            eventModel,
            new object[]
            {
                new LocString("events", PageKey(eventName, pageName)),
                new[] { startFight },
            });
    }

    private static Task StartEventBattle(
        EventModel eventModel,
        EncounterModel encounter,
        IReadOnlyList<Reward> extraRewards,
        PendingBattleSetup setup,
        bool shouldResumeAfterCombat)
    {
        Player owner = RequireOwner(eventModel);
        PendingBattleSetups[owner] = (setup, encounter);
        EventsEnteringSinglePlayerCombat.Add(eventModel);
        try
        {
            EnterCombatWithoutExitingEventMethod.Invoke(
                eventModel,
                new object[] { encounter, extraRewards, shouldResumeAfterCombat });
        }
        catch
        {
            PendingBattleSetups.Remove(owner);
            throw;
        }
        finally
        {
            EventsEnteringSinglePlayerCombat.Remove(eventModel);
        }
        return Task.CompletedTask;
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

    private static bool IsTrialSpiritCommand(CardModel card) =>
        card is SGC_Ki
            or SGC_Spirit
            or SGC_SuperKi
            or SGC_FightingSpirit
            or SGC_Indomitable;

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

    private static EventOption CreateConditionalOption(
        EventModel eventModel,
        bool available,
        Func<Task> onChosen,
        string eventName,
        string optionName,
        IEnumerable<IHoverTip>? hoverTips = null,
        bool disableOnChosen = true)
    {
        string key = Key(eventName, available ? optionName : $"{optionName}_LOCKED");
        return new EventOption(
            eventModel,
            available ? onChosen : null,
            key,
            disableOnChosen,
            isProceed: false,
            hoverTips: available ? (hoverTips ?? Array.Empty<IHoverTip>()).ToArray() : Array.Empty<IHoverTip>());
    }

    private static string Key(string eventName, string optionName) =>
        $"{LocPrefix}.{eventName}.pages.INITIAL.options.{optionName}";

    private static string PageKey(string eventName, string pageName) =>
        $"{LocPrefix}.{eventName}.pages.{pageName}.description";

    private static string PageOptionKey(string eventName, string pageName, string optionName) =>
        $"{LocPrefix}.{eventName}.pages.{pageName}.options.{optionName}";

    private static LocString SelectionKey(string eventName, string routeName) =>
        new("events", $"{LocPrefix}.{eventName}.pages.{routeName}.selectionPrompt");

    private static void Finish(EventModel eventModel, string descriptionKey)
    {
        SetEventFinishedMethod.Invoke(
            eventModel,
            new object[] { new LocString("events", descriptionKey) });
    }
}
