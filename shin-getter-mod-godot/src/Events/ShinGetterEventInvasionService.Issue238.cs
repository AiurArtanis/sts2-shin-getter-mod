#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Enchantments;
using ShinGetterMod.Models.Potions;
using ShinGetterMod.Models.Relics;
using ShinGetterMod.Nodes.Events;
using ShinGetterMod.Services;

namespace ShinGetterMod.Events;

internal static partial class ShinGetterEventInvasionService
{
    private sealed class FourthRuntime { internal bool Busy; }
    private static readonly ConditionalWeakTable<EventModel, FourthRuntime> FourthStates = new();
    private static readonly HashSet<string> FourthEvents = new(StringComparer.Ordinal)
    {
        "WHISPERING_HOLLOW", "SYMBIOTE", "AROMA_OF_CHAOS", "SAPPHIRE_SEED", "HUNGRY_FOR_MUSHROOMS",
        "BRAIN_LEECH", "FIELD_OF_MAN_SIZED_HOLES", "DOLL_ROOM", "ZEN_WEAVER", "WATERLOGGED_VAULT",
        "POTION_COURIER", "FORGOTTEN_GRAVE", "STONE_OF_ETERNITY", "TABLET_OF_TRUTH", "SELF_HELP_BOOK"
    };
    private static readonly MethodInfo InitialFourthOptions = AccessTools.Method(typeof(EventModel), "GenerateInitialOptionsWrapper")
        ?? throw new MissingMethodException("GenerateInitialOptionsWrapper");
    private static readonly PropertyInfo TabletDecipherCount = AccessTools.Property(typeof(TabletOfTruth), "DecipherCount")
        ?? throw new MissingMemberException("TabletOfTruth.DecipherCount");
    private static readonly FieldInfo NativeRunSaveManager = AccessTools.Field(typeof(SaveManager), "_runSaveManager")
        ?? throw new MissingFieldException("SaveManager._runSaveManager");

    private static string? FourthVisitKey(EventModel model)
    {
        Player owner = RequireOwner(model);
        IRunState run = owner.RunState;
        int act = run.CurrentActIndex;
        if (act < 0 || act >= run.MapPointHistory.Count || run.MapPointHistory[act].Count == 0
            || run.CurrentRoom is not EventRoom room || room.ModelId != model.Id) return null;
        var points = run.MapPointHistory[act];
        var rooms = points[^1].Rooms;
        int visit = ReferenceEquals(room, run.BaseRoom) ? 0
            : rooms.FindLastIndex(r => r.RoomType == RoomType.Event && r.ModelId == model.Id);
        if (visit < 0 || visit >= rooms.Count || rooms[visit].RoomType != RoomType.Event || rooms[visit].ModelId != model.Id) return null;
        return ShinGetterEventVisitIdentity.Create(run.Rng.StringSeed, owner.NetId, act,
            run.CurrentMapCoord.ToString(), points.Count - 1, visit, model.Id);
    }

    private static ShinGetterPlayerEventState.Visit? FourthVisit(EventModel model)
    {
        string? key = FourthVisitKey(model);
        if (key == null) return null;
        var data = ShinGetterPlayerEventState.Get(RequireOwner(model));
        if (!data.Visits.TryGetValue(key, out var visit))
        {
            if (data.Visits.Count >= ShinGetterPlayerEventState.MaxVisits)
                throw new InvalidOperationException("Event journal reached its safe limit.");
            data.Visits[key] = visit = new();
        }
        return visit;
    }

    internal static bool RestoreFourthCompleted(EventModel model)
    {
        if (!FourthEvents.Contains(model.Id.Entry) || model.Owner == null || model.Owner.RunState.Players.Count != 1
            || !ShinGetterPlayerEventState.IsShinGetter(model.Owner.Character.Id) || model.IsShared) return false;
        var visit = FourthVisit(model);
        if (visit == null || visit.CompletedRoute.Length == 0) return false;
        if (!FourthRouteNames(model.Id.Entry).Contains(visit.CompletedRoute)) throw new JsonException("Unknown completed event route.");
        Finish(model, PageKey(model.Id.Entry, visit.CompletedRoute + "_RESULT"));
        return true; // No cost, RNG, selection, or reward replay.
    }

    private static IEnumerable<EventOption> BuildFourthOptions(EventModel model)
    {
        Player owner = RequireOwner(model);
        if (owner.RunState.Players.Count != 1 || model.IsShared || !FourthEvents.Contains(model.Id.Entry)
            || FourthVisitKey(model) == null || model is TabletOfTruth && (int)TabletDecipherCount.GetValue(model)! != 0)
            yield break;
        foreach (string route in FourthRouteNames(model.Id.Entry))
        {
            bool available = FourthAvailable(model, route);
            EventOption option = CreateConditionalOption(model, available,
                () => RunFourth(model, route), model.Id.Entry, available ? route : FourthLockedName(model, route),
                FourthHoverTips(model.Id.Entry, route), disableOnChosen: false);
            int damage = FourthHpCost(model.Id.Entry, route);
            if (available && damage > 0) option.ThatDoesDamage(damage);
            yield return option;
        }
    }

    private static string[] FourthRouteNames(string name) => name switch
    {
        "SYMBIOTE" or "ZEN_WEAVER" => new[] { "BENKEI", "HAYATO" },
        "POTION_COURIER" => new[] { "RYOMA", "HAYATO" },
        "FORGOTTEN_GRAVE" => new[] { "TRIPLE_COORDINATION", "RYOMA" },
        "TABLET_OF_TRUTH" => new[] { "HAYATO", "BENKEI" },
        "BRAIN_LEECH" => new[] { "TRIPLE_COORDINATION" },
        "WHISPERING_HOLLOW" or "HUNGRY_FOR_MUSHROOMS" or "STONE_OF_ETERNITY" => new[] { "BENKEI" },
        "AROMA_OF_CHAOS" or "FIELD_OF_MAN_SIZED_HOLES" or "DOLL_ROOM" => new[] { "HAYATO" },
        _ => new[] { "RYOMA" }
    };
    private static bool FourthAvailable(EventModel model, string route)
    {
        Player owner = RequireOwner(model);
        var visit = FourthVisit(model);
        if (!owner.Creature.IsAlive || model.IsFinished || visit?.CompletedRoute.Length > 0) return false;
        if (visit?.Target.Length > 0 && model.Id.Entry is "AROMA_OF_CHAOS" or "HUNGRY_FOR_MUSHROOMS")
        {
            CardModel? target = FindFourthTarget(owner, visit.Target);
            if (target == null || (model.Id.Entry == "AROMA_OF_CHAOS" ? !target.IsTransformable : !target.IsUpgradable)) return false;
        }
        if (visit?.Candidates.Count > 0)
        {
            string routePrefix = route + "|";
            if (visit.Candidates.Any(c => !c.StartsWith(routePrefix, StringComparison.Ordinal))) return false;
            IEnumerable<AbstractModel> pool = model.Id.Entry switch
            {
                "BRAIN_LEECH" => FourthColorless(owner),
                "POTION_COURIER" => FourthPotions(owner),
                "AROMA_OF_CHAOS" when FindFourthTarget(owner, visit.Target) is CardModel target
                    => CardFactory.GetDefaultTransformationOptions(target, false),
                _ => Array.Empty<AbstractModel>()
            };
            HashSet<string> valid = pool.Select(c => routePrefix + c.Id).ToHashSet(StringComparer.Ordinal);
            if (visit.Candidates.Any(c => !valid.Contains(c))
                || model.Id.Entry is "BRAIN_LEECH" or "POTION_COURIER" && visit.Candidates.Count != 3) return false;
        }
        bool attackAdaptation(CardModel c) => c.Type == CardType.Attack && ModelDb.Enchantment<SGE_Adaptation>().CanEnchant(c);
        return (model.Id.Entry, route) switch
        {
            ("WHISPERING_HOLLOW", _) => HasCard<SGC_ShinForm>(owner) && !HasCard<SGC_TripleWhirlwind>(owner) && RemovableFourthRelics(owner).Any(),
            ("SYMBIOTE", "BENKEI") => owner.Creature.MaxHp > 5 && owner.Deck.Cards.Any(attackAdaptation),
            ("SYMBIOTE", _) => HasAnyCard<SGC_Meltdown, SGC_Annihilation>(owner) && owner.GetRelic<SGR_SymbioticFilter>() == null,
            ("AROMA_OF_CHAOS", _) => HasRole(owner, ShinGetterCardRole.Strategy) && owner.Creature.CurrentHp > 6 && owner.Deck.Cards.Any(c => c.IsTransformable),
            ("SAPPHIRE_SEED", _) => owner.GetRelic<SGR_ActivatedSapphire>() == null,
            ("HUNGRY_FOR_MUSHROOMS", _) => HasRole(owner, ShinGetterCardRole.GetterThreeDefense) && owner.Creature.CurrentHp > 15 && owner.Deck.Cards.Any(c => c.IsUpgradable),
            ("BRAIN_LEECH", _) => (HasCard<SGC_AwakenedSoul>(owner) || owner.GetRelic<SGR_KenIshikawaManuscript>() != null)
                && owner.Creature.MaxHp > 5 && FourthColorless(owner).Count >= 3,
            ("FIELD_OF_MAN_SIZED_HOLES", _) => HasAnyCard<SGC_Insight, SGC_BackupPlan>(owner) && owner.Creature.CurrentHp > 7 && owner.Deck.Cards.Any(FourthAdaptation),
            ("DOLL_ROOM", _) => HasAnyCard<SGC_Insight, SGC_BackupPlan>(owner) && owner.Gold >= 80,
            ("ZEN_WEAVER", "BENKEI") => HasAnyCard<SGC_Indomitable, SGC_IronWall, SGC_Guts>(owner) && owner.Creature.MaxHp > 4 && owner.Deck.Cards.Any(c => c.IsRemovable),
            ("ZEN_WEAVER", _) => HasCard<SGC_ShinForm>(owner) && !HasCard<SGC_DrillMissile>(owner) && owner.Creature.CurrentHp > 10,
            ("WATERLOGGED_VAULT", _) => HasCard<SGC_Spirit>(owner) && owner.Creature.CurrentHp > 8,
            ("POTION_COURIER", "RYOMA") => owner.GetRelic<SGR_GoodCitizenCard>() != null && owner.Gold >= 60 && owner.PotionSlots.Any(p => p == null) && FourthPotions(owner).Count >= 3,
            ("POTION_COURIER", _) => owner.GetRelic<SGR_ResearchNotes>() != null && owner.Gold >= 70,
            ("FORGOTTEN_GRAVE", "TRIPLE_COORDINATION") => HasAnyCard<SGC_AwakenedSoul, SGC_SuperKi>(owner) && owner.Creature.MaxHp > 6 && owner.Deck.Cards.Any(FourthSoulsPower),
            ("FORGOTTEN_GRAVE", _) => owner.GetRelic<SGR_Ember>() == null,
            ("STONE_OF_ETERNITY", _) => HasAnyCard<SGC_IronWall, SGC_Guts>(owner) && owner.Creature.CurrentHp > 12,
            ("TABLET_OF_TRUTH", "HAYATO") => HasAnyCard<SGC_Insight, SGC_BackupPlan>(owner) && owner.Creature.MaxHp > 5 && owner.Deck.Cards.Any(c => c.IsUpgradable),
            ("TABLET_OF_TRUTH", _) => !HasCard<SGC_TabletOfTruth>(owner),
            ("SELF_HELP_BOOK", _) => !HasCard<SGC_Annotations>(owner),
            _ => false
        };
    }
    private static string FourthLockedName(EventModel model, string route)
    {
        Player owner = RequireOwner(model);
        // Resource/slot/pool reasons are deliberately separate and ordered in the design.
        // CreateConditionalOption appends _LOCKED to the supplied name.
        switch (model.Id.Entry, route)
        {
            case ("WHISPERING_HOLLOW", _):
                if (!HasCard<SGC_ShinForm>(owner)) return route + "_FORM";
                if (HasCard<SGC_TripleWhirlwind>(owner)) return route + "_OWNED";
                if (!RemovableFourthRelics(owner).Any()) return route + "_RELIC";
                break;
            case ("SYMBIOTE", "BENKEI"):
                if (owner.Creature.MaxHp <= 5) return route + "_MAXHP";
                if (!owner.Deck.Cards.Any(c => c.Type == CardType.Attack && FourthAdaptation(c))) return route + "_TARGET";
                break;
            case ("AROMA_OF_CHAOS", _):
                if (!HasRole(owner, ShinGetterCardRole.Strategy)) return route + "_ROLE";
                if (!owner.Deck.Cards.Any(c => c.IsTransformable)) return route + "_TARGET";
                if (owner.Creature.CurrentHp <= 6) return route + "_HP";
                break;
            case ("HUNGRY_FOR_MUSHROOMS", _):
                if (!HasRole(owner, ShinGetterCardRole.GetterThreeDefense)) return route + "_ROLE";
                if (owner.Creature.CurrentHp <= 15) return route + "_HP";
                if (!owner.Deck.Cards.Any(c => c.IsUpgradable)) return route + "_TARGET";
                break;
            case ("FIELD_OF_MAN_SIZED_HOLES", _):
                if (!HasAnyCard<SGC_Insight, SGC_BackupPlan>(owner)) return route + "_ROLE";
                if (owner.Creature.CurrentHp <= 7) return route + "_HP";
                if (!owner.Deck.Cards.Any(FourthAdaptation)) return route + "_TARGET";
                break;
            case ("WATERLOGGED_VAULT", _):
                if (!HasCard<SGC_Spirit>(owner)) return route + "_ROLE";
                if (owner.Creature.CurrentHp <= 8) return route + "_HP";
                break;
            case ("STONE_OF_ETERNITY", _):
                if (!HasAnyCard<SGC_IronWall, SGC_Guts>(owner)) return route + "_ROLE";
                break;
            case ("POTION_COURIER", "HAYATO"):
                if (owner.GetRelic<SGR_ResearchNotes>() == null) return route + "_RELIC";
                break;
        }
        if (model.Id.Entry == "POTION_COURIER" && route == "RYOMA" && owner.GetRelic<SGR_GoodCitizenCard>() != null)
        {
            if (owner.Gold < 60) return route + "_GOLD";
            if (!owner.PotionSlots.Any(p => p == null)) return route + "_SLOT";
            if (FourthPotions(owner).Count < 3) return route + "_POOL";
        }
        if (model.Id.Entry == "POTION_COURIER" && route == "HAYATO" && owner.GetRelic<SGR_ResearchNotes>() != null && owner.Gold < 70)
            return route + "_GOLD";
        if (model.Id.Entry == "STONE_OF_ETERNITY" && HasAnyCard<SGC_IronWall, SGC_Guts>(owner) && owner.Creature.CurrentHp <= 12)
            return route + "_HP";
        return route;
    }
    private static bool HasCard<T>(Player owner) where T : CardModel => owner.Deck.Cards.Any(c => c is T);
    private static bool FourthAdaptation(CardModel card) => card.Type is CardType.Attack or CardType.Skill or CardType.Power
        && ModelDb.Enchantment<SGE_Adaptation>().CanEnchant(card);
    private static bool FourthSoulsPower(CardModel card) => card.Keywords.Contains(CardKeyword.Exhaust) && ModelDb.Enchantment<SoulsPower>().CanEnchant(card);
    private static IEnumerable<RelicModel> RemovableFourthRelics(Player owner) => owner.Relics.Where(r => !r.IsMelted && !r.SpawnsPets);
    private static List<CardModel> FourthColorless(Player owner) => ModelDb.CardPool<ColorlessCardPool>()
        .GetUnlockedCards(owner.UnlockState, owner.RunState.CardMultiplayerConstraint)
        // Equivalent to NoRarityModification | NoCardPoolModifications: canonical colorless pool,
        // no creation hook or rarity change; explicitly excludes nonstandard reward types.
        .Where(c => c.IsUpgradable && c.Type is CardType.Attack or CardType.Skill or CardType.Power
            && c.Rarity is not (CardRarity.Event or CardRarity.Ancient))
        .DistinctBy(c => c.Id).OrderBy(c => c.Id).ToList();
    private static List<PotionModel> FourthPotions(Player owner) => owner.Character.PotionPool.GetUnlockedPotions(owner.UnlockState)
        .Concat(ModelDb.PotionPool<SharedPotionPool>().GetUnlockedPotions(owner.UnlockState))
        .Where(p => p.Rarity == PotionRarity.Uncommon).DistinctBy(p => p.Id).OrderBy(p => p.Id).ToList();
    private static int FourthHpCost(string name, string route) => (name, route) switch
    {
        ("AROMA_OF_CHAOS", _) => 6, ("HUNGRY_FOR_MUSHROOMS", _) => 15,
        ("FIELD_OF_MAN_SIZED_HOLES", _) => 7, ("ZEN_WEAVER", "HAYATO") => 10,
        ("WATERLOGGED_VAULT", _) => 8, ("STONE_OF_ETERNITY", _) => 12, _ => 0
    };
    private static IEnumerable<IHoverTip> FourthHoverTips(string name, string route) => (name, route) switch
    {
        ("WHISPERING_HOLLOW", _) => HoverTipFactory.FromCardWithCardHoverTips<SGC_TripleWhirlwind>(),
        ("SYMBIOTE", "HAYATO") => HoverTipFactory.FromRelic<SGR_SymbioticFilter>(),
        ("SAPPHIRE_SEED", _) => HoverTipFactory.FromRelic<SGR_ActivatedSapphire>(),
        ("ZEN_WEAVER", "HAYATO") => HoverTipFactory.FromCardWithCardHoverTips<SGC_DrillMissile>(),
        ("POTION_COURIER", "HAYATO") => new[] { HoverTipFactory.FromPotion<SGR_MobilityCatalyst>() },
        ("FORGOTTEN_GRAVE", "RYOMA") => HoverTipFactory.FromRelic<SGR_Ember>(),
        ("TABLET_OF_TRUTH", "BENKEI") => HoverTipFactory.FromCardWithCardHoverTips<SGC_TabletOfTruth>(),
        ("SELF_HELP_BOOK", _) => HoverTipFactory.FromCardWithCardHoverTips<SGC_Annotations>(),
        _ => Array.Empty<IHoverTip>()
    };

    private static async Task RunFourth(EventModel model, string route)
    {
        FourthRuntime runtime = FourthStates.GetOrCreateValue(model);
        if (runtime.Busy || !FourthAvailable(model, route)) return;
        runtime.Busy = true;
        try
        {
            bool committed = await ExecuteFourth(model, route);
            if (!committed && !model.IsFinished) ReturnFourthInitial(model);
        }
        catch (Exception ex)
        {
            Log.Error($"issue#238 {model.Id}/{route}: {ex}");
            if (!model.IsFinished) ReturnFourthInitial(model);
            ShowFourthError();
        }
        finally { runtime.Busy = false; }
    }
    private static void ReturnFourthInitial(EventModel model) => SetEventStateMethod.Invoke(model,
        new object[] { model.InitialDescription, InitialFourthOptions.Invoke(model, null)! });
    private static void ShowFourthError()
    {
        if (NonInteractiveMode.IsActive) return;
        NErrorPopup? popup = NErrorPopup.Create(new LocString("events", LocPrefix + ".TRANSACTION_ERROR.title").GetFormattedText(),
            new LocString("events", LocPrefix + ".TRANSACTION_ERROR.description").GetFormattedText(), showReportBugButton: false);
        if (popup == null) return;
        NModalContainer? container = NModalContainer.Instance;
        if (container == null || container.OpenModal != null) { popup.QueueFree(); return; }
        container.Add(popup);
        if (!popup.IsInsideTree()) popup.QueueFree();
    }

    private static async Task SaveFourth(Player owner)
    {
        if (!RunManager.Instance.ShouldSave) return; // Practice/debug runs intentionally have no save.
        if (RunManager.Instance.NetService.Type != NetGameType.Singleplayer)
            throw new InvalidOperationException("Fourth batch persistence is single-player only.");
        SaveManager manager = SaveManager.Instance;
        if (manager.CurrentRunSaveTask != null) await manager.CurrentRunSaveTask;
        RunSaveManager native = (RunSaveManager)NativeRunSaveManager.GetValue(manager)!;
        bool saved = false;
        void OnSaved() => saved = true;
        native.Saved += OnSaved;
        try
        {
            SerializablePlayer snapshot = owner.ToSerializable();
            string expected = JsonSerializer.Serialize(snapshot, JsonSerializationUtility.GetTypeInfo<SerializablePlayer>());
            await ShinGetterEventSaveVerification.Verify(() => manager.SaveRun(null, saveProgress: false), () => saved, () =>
            {
                var readback = native.LoadRunSave();
                SerializablePlayer? readOwner = readback.SaveData?.Players.FirstOrDefault(p => p.NetId == owner.NetId);
                if (!readback.Success || readOwner == null
                    || JsonSerializer.Serialize(readOwner, JsonSerializationUtility.GetTypeInfo<SerializablePlayer>()) != expected)
                    throw new InvalidOperationException("Native event save readback differs from the complete player snapshot.");
            });
        }
        finally { native.Saved -= OnSaved; }
    }
    private static async Task<bool> CommitFourth(EventModel model, string route, Func<Task> apply)
    {
        Player owner = RequireOwner(model);
        if (!FourthAvailable(model, route)) return false;
        var visit = FourthVisit(model) ?? throw new InvalidOperationException("No stable event visit.");
        ShinGetterEventTransaction snapshot = new(owner);
        try
        {
            await apply();
            if (!owner.Creature.IsAlive) throw new InvalidOperationException("Event owner died before reward completion.");
            visit.CompletedRoute = route; // After every cost and reward, never before a pending command.
            await SaveFourth(owner);
        }
        catch (ShinGetterEventSaveVerification.ConfirmedWriteReadbackException ex)
        {
            // Once native Saved fired, a complete snapshot may already be on disk. Never reopen
            // a reward route by rolling that confirmed write back only in memory.
            Log.Error(ex.ToString());
            ShowFourthError();
        }
        catch { snapshot.Rollback(); throw; }
        Finish(model, PageKey(model.Id.Entry, route + "_RESULT"));
        return true;
    }

    private static async Task<CardModel?> SelectFourthCard(EventModel model, string route, Func<CardModel, bool> legal, bool cancelable = true)
    {
        Player owner = RequireOwner(model);
        CardModel[] cards = owner.Deck.Cards.Where(legal).ToArray();
        if (cards.Length == 0) return null;
        CardModel? selected = (await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), cards, owner,
            new CardSelectorPrefs(SelectionKey(model.Id.Entry, route), 1) { Cancelable = cancelable, RequireManualConfirmation = true })).FirstOrDefault();
        return selected != null && selected.Owner == owner && selected.Pile?.Type == PileType.Deck && legal(selected) ? selected : null;
    }
    private static string FourthCardIdentity(Player owner, CardModel card)
    {
        int index = owner.Deck.Cards.ToList().IndexOf(card);
        string serialized = JsonSerializer.Serialize(card.ToSerializable(), JsonSerializationUtility.GetTypeInfo<SerializableCard>());
        return $"{index}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)))}";
    }
    private static CardModel? FindFourthTarget(Player owner, string identity) => owner.Deck.Cards.FirstOrDefault(c => FourthCardIdentity(owner, c) == identity);
    private static async Task<List<T>> CachedFourthChoices<T>(EventModel model, string route, IEnumerable<T> candidates) where T : AbstractModel
    {
        var visit = FourthVisit(model)!;
        // Independent route namespace prevents canceled alternative routes from sharing candidates.
        string prefix = route + "|";
        List<T> legal = candidates.DistinctBy(c => c.Id).OrderBy(c => c.Id).ToList();
        if (visit.Candidates.Count != 0)
        {
            if (visit.Candidates.Any(c => !c.StartsWith(prefix, StringComparison.Ordinal))) return new();
            return visit.Candidates.Select(id => legal.FirstOrDefault(c => prefix + c.Id == id)).OfType<T>().ToList();
        }
        List<T> picked = new();
        while (picked.Count < 3 && legal.Count > 0)
        {
            int index = model.Rng.NextInt(legal.Count);
            picked.Add(legal[index]); legal.RemoveAt(index);
        }
        visit.Candidates = picked.Select(c => prefix + c.Id).ToList();
        await SaveFourth(RequireOwner(model));
        return picked;
    }
    private static async Task AddFourthCard<T>(Player owner) where T : CardModel
    {
        CardModel card = owner.RunState.CreateCard<T>(owner);
        await CardPileCmd.Add(card, PileType.Deck);
        if (!owner.Deck.Cards.Contains(card)) throw new InvalidOperationException("Event card was not added.");
    }
    private static async Task FourthDamage(Player owner, int amount)
    {
        if (owner.Creature.CurrentHp <= amount) throw new InvalidOperationException("Event damage would be fatal.");
        await LoseHp(owner, amount);
        if (!owner.Creature.IsAlive) throw new InvalidOperationException("Event damage killed owner.");
    }
    private static async Task FourthMaxHp(Player owner, int amount)
    {
        if (owner.Creature.MaxHp <= amount) throw new InvalidOperationException("Maximum HP cost would be fatal.");
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), owner.Creature, amount, isFromCard: false);
    }
    private static void FourthEnchant(CardModel card, EnchantmentModel canonical)
    {
        if (!canonical.CanEnchant(card)) throw new InvalidOperationException("Enchantment target became invalid.");
        CardCmd.Enchant(canonical.ToMutable(), card, 1m);
    }

    private static async Task<bool> ExecuteFourth(EventModel model, string route)
    {
        Player owner = RequireOwner(model);
        string name = model.Id.Entry;
        switch (name, route)
        {
            case ("WHISPERING_HOLLOW", _):
            {
                RelicModel[] relics = RemovableFourthRelics(owner).ToArray();
                int index = await NShinGetterEventItemChoice.Select(owner, SelectionKey(name, route),
                    relics.Select(r => new NShinGetterEventItemChoice.Item(r.Title.GetFormattedText(), r.DynamicDescription.GetFormattedText(), r.IconPath)).ToArray(), true);
                if (index < 0 || index >= relics.Length || !RemovableFourthRelics(owner).Contains(relics[index])) return false;
                return await CommitFourth(model, route, async () => { await RelicCmd.Remove(relics[index]); await AddFourthCard<SGC_TripleWhirlwind>(owner); });
            }
            case ("SYMBIOTE", "BENKEI"):
            case ("FIELD_OF_MAN_SIZED_HOLES", _):
            {
                bool legal(CardModel c) => FourthAdaptation(c) && (name != "SYMBIOTE" || c.Type == CardType.Attack);
                CardModel? target = await SelectFourthCard(model, route, legal);
                if (target == null) return false;
                return await CommitFourth(model, route, async () =>
                {
                    if (!legal(target) || target.Pile?.Type != PileType.Deck) throw new InvalidOperationException("Invalid adaptation target.");
                    if (name == "SYMBIOTE") await FourthMaxHp(owner, 5); else await FourthDamage(owner, 7);
                    FourthEnchant(target, ModelDb.Enchantment<SGE_Adaptation>());
                });
            }
            case ("SYMBIOTE", "HAYATO"):
                return await CommitFourth(model, route, async () => { await RelicCmd.Obtain<SGR_SymbioticFilter>(owner); });
            case ("SAPPHIRE_SEED", _):
                return await CommitFourth(model, route, async () => { await RelicCmd.Obtain<SGR_ActivatedSapphire>(owner); });
            case ("AROMA_OF_CHAOS", _):
            {
                var visit = FourthVisit(model)!;
                CardModel? target;
                if (visit.Target.Length == 0)
                {
                    target = await SelectFourthCard(model, route, c => c.IsTransformable);
                    if (target == null) return false;
                    visit.Target = FourthCardIdentity(owner, target);
                    await SaveFourth(owner);
                }
                else target = FindFourthTarget(owner, visit.Target);
                if (target == null || !target.IsTransformable) return false;
                List<CardModel> canonical = await CachedFourthChoices(model, route, CardFactory.GetDefaultTransformationOptions(target, false));
                if (canonical.Count == 0) return false;
                CardModel[] choices = canonical.Select(c => c.ToMutable()).ToArray();
                foreach (CardModel choice in choices) choice.Owner = owner;
                CardModel? selected = (await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), choices, owner,
                    new CardSelectorPrefs(SelectionKey(name, route), 1) { Cancelable = true, RequireManualConfirmation = true })).FirstOrDefault();
                if (selected == null || !choices.Contains(selected)) return false;
                return await CommitFourth(model, route, async () =>
                {
                    await FourthDamage(owner, 6);
                    CardModel replacement = owner.RunState.CreateCard(ModelDb.GetById<CardModel>(selected.Id), owner);
                    if (await CardCmd.Transform(target, replacement, CardPreviewStyle.EventLayout) == null) throw new InvalidOperationException("Transformation failed.");
                });
            }
            case ("HUNGRY_FOR_MUSHROOMS", _):
            {
                var visit = FourthVisit(model)!;
                if (visit.Target.Length == 0)
                {
                    var candidates = owner.Deck.Cards.Where(c => c.IsUpgradable).ToArray();
                    if (candidates.Length == 0) return false;
                    visit.Target = FourthCardIdentity(owner, candidates[model.Rng.NextInt(candidates.Length)]);
                    await SaveFourth(owner);
                }
                CardModel? target = FindFourthTarget(owner, visit.Target);
                if (target == null || !target.IsUpgradable) return false;
                return await CommitFourth(model, route, async () => { await FourthDamage(owner, 15); await CreatureCmd.GainMaxHp(owner.Creature, 5); CardCmd.Upgrade(target); });
            }
            case ("BRAIN_LEECH", _):
            {
                List<CardModel> canonical = await CachedFourthChoices(model, route, FourthColorless(owner));
                if (canonical.Count != 3) return false;
                CardModel[] cards = canonical.Select(c => c.ToMutable()).ToArray();
                foreach (CardModel card in cards) { card.Owner = owner; CardCmd.Upgrade(card, CardPreviewStyle.None); }
                CardModel? selected = (await CardSelectCmd.FromSimpleGrid(new BlockingPlayerChoiceContext(), cards, owner,
                    new CardSelectorPrefs(SelectionKey(name, route), 1) { Cancelable = false, RequireManualConfirmation = true })).FirstOrDefault();
                if (selected == null || !cards.Contains(selected)) return false;
                return await CommitFourth(model, route, async () =>
                {
                    await FourthMaxHp(owner, 5);
                    owner.RunState.AddCard(selected, owner);
                    await CardPileCmd.Add(selected, PileType.Deck);
                    if (!owner.Deck.Cards.Contains(selected)) throw new InvalidOperationException("Colorless reward failed.");
                });
            }
            case ("DOLL_ROOM", _):
            {
                RelicModel[] dolls = { ModelDb.Relic<DaughterOfTheWind>(), ModelDb.Relic<MrStruggles>(), ModelDb.Relic<BingBong>() };
                int index = await NShinGetterEventItemChoice.Select(owner, SelectionKey(name, route),
                    dolls.Select(r => new NShinGetterEventItemChoice.Item(r.Title.GetFormattedText(), r.DynamicDescription.GetFormattedText(), r.IconPath)).ToArray(), false);
                if (index < 0 || index >= dolls.Length) return false;
                return await CommitFourth(model, route, async () => { await PlayerCmd.LoseGold(80, owner, GoldLossType.Spent); await RelicCmd.Obtain(dolls[index].ToMutable(), owner); });
            }
            case ("ZEN_WEAVER", "BENKEI"):
            case ("TABLET_OF_TRUTH", "HAYATO"):
            case ("FORGOTTEN_GRAVE", "TRIPLE_COORDINATION"):
            {
                bool legal(CardModel c) => name switch { "ZEN_WEAVER" => c.IsRemovable, "TABLET_OF_TRUTH" => c.IsUpgradable, _ => FourthSoulsPower(c) };
                CardModel? target = await SelectFourthCard(model, route, legal);
                if (target == null) return false;
                return await CommitFourth(model, route, async () =>
                {
                    if (!legal(target) || target.Pile?.Type != PileType.Deck) throw new InvalidOperationException("Target became invalid.");
                    await FourthMaxHp(owner, name == "ZEN_WEAVER" ? 4 : name == "TABLET_OF_TRUTH" ? 5 : 6);
                    if (name == "ZEN_WEAVER") await CardPileCmd.RemoveFromDeck(target);
                    else if (name == "TABLET_OF_TRUTH") CardCmd.Upgrade(target);
                    else FourthEnchant(target, ModelDb.Enchantment<SoulsPower>());
                });
            }
            case ("ZEN_WEAVER", "HAYATO"):
                return await CommitFourth(model, route, async () => { await FourthDamage(owner, 10); await AddFourthCard<SGC_DrillMissile>(owner); });
            case ("WATERLOGGED_VAULT", _):
                return await CommitFourth(model, route, async () => { await FourthDamage(owner, 8); await PlayerCmd.GainGold(150, owner); });
            case ("POTION_COURIER", "RYOMA"):
            {
                List<PotionModel> potions = await CachedFourthChoices(model, route, FourthPotions(owner));
                if (potions.Count != 3) return false;
                int index = await NShinGetterEventItemChoice.Select(owner, SelectionKey(name, route),
                    potions.Select(p => new NShinGetterEventItemChoice.Item(p.Title.GetFormattedText(), p.DynamicDescription.GetFormattedText(), p.ImagePath)).ToArray(), false);
                if (index < 0 || index >= potions.Count) return false;
                return await CommitFourth(model, route, async () =>
                {
                    await PlayerCmd.LoseGold(60, owner, GoldLossType.Spent);
                    if (!(await PotionCmd.TryToProcure(potions[index].ToMutable(), owner)).success) throw new InvalidOperationException("Potion delivery failed.");
                });
            }
            case ("POTION_COURIER", "HAYATO"):
            {
                int slot = owner.PotionSlots.ToList().FindIndex(p => p == null);
                PotionModel? discard = null;
                if (slot < 0)
                {
                    PotionModel[] potions = owner.Potions.ToArray();
                    int index = await NShinGetterEventItemChoice.Select(owner, SelectionKey(name, route),
                        potions.Select(p => new NShinGetterEventItemChoice.Item(p.Title.GetFormattedText(), p.DynamicDescription.GetFormattedText(), p.ImagePath)).ToArray(), true);
                    if (index < 0 || index >= potions.Length) return false;
                    discard = potions[index]; slot = owner.PotionSlots.ToList().IndexOf(discard);
                }
                PotionModel catalyst = ModelDb.Potion<SGR_MobilityCatalyst>().ToMutable();
                if (slot < 0 || !Hook.ShouldProcurePotion(owner.RunState, null, catalyst, owner)) return false;
                return await CommitFourth(model, route, async () =>
                {
                    if (discard != null)
                    {
                        if (!ReferenceEquals(owner.GetPotionAtSlotIndex(slot), discard)) throw new InvalidOperationException("Discard target changed.");
                        await PotionCmd.Discard(discard);
                    }
                    else if (owner.GetPotionAtSlotIndex(slot) != null) throw new InvalidOperationException("Potion slot was filled.");
                    await PlayerCmd.LoseGold(70, owner, GoldLossType.Spent);
                    if (!(await PotionCmd.TryToProcure(catalyst, owner, slot)).success) throw new InvalidOperationException("Catalyst delivery failed.");
                });
            }
            case ("FORGOTTEN_GRAVE", "RYOMA"):
                return await CommitFourth(model, route, async () => { await RelicCmd.Obtain<SGR_Ember>(owner); });
            case ("STONE_OF_ETERNITY", _):
                return await CommitFourth(model, route, async () => { int hpBefore = owner.Creature.CurrentHp; await CreatureCmd.GainMaxHp(owner.Creature, 10); await CreatureCmd.SetCurrentHp(owner.Creature, hpBefore - 12); });
            case ("TABLET_OF_TRUTH", "BENKEI"):
                return await CommitFourth(model, route, () => AddFourthCard<SGC_TabletOfTruth>(owner));
            case ("SELF_HELP_BOOK", _):
                return await CommitFourth(model, route, () => AddFourthCard<SGC_Annotations>(owner));
            default: return false;
        }
    }
}
