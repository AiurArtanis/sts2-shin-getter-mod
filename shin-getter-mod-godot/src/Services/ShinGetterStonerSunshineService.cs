#nullable enable
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Services;

/// <summary>
/// Owns the per-combat progress used by Stoner Sunshine's special arrival.
/// The combat-state key keeps progress isolated between players and releases it with the combat.
/// </summary>
internal static class ShinGetterStonerSunshineService
{
    private const decimal TurnChancePerCompletedTurn = 0.05m;
    private const decimal AllFormsChance = 0.10m;
    private const decimal TripleUnityChancePerPlay = 0.10m;
    private const decimal AllEnemiesLowHpChance = 0.15m;
    private const decimal SpiritCommandChancePerPlay = 0.05m;
    private const int AllAtomicFormsMask = 0b111;

    private static readonly ConditionalWeakTable<CombatState, CombatProgress> CombatStates = new();

    internal static void ResetCombat(Player owner)
    {
        if (owner.Creature.CombatState is CombatState combatState)
            GetProgress(combatState, owner).Reset();
    }

    internal static void RecordAtomicTransform(Player owner, ShinGetterForm form)
    {
        if (!TryGetActiveProgress(owner, out PlayerProgress progress))
            return;

        progress.AtomicFormsMask |= form switch
        {
            ShinGetterForm.Getter1 => 1 << 0,
            ShinGetterForm.Getter2 => 1 << 1,
            ShinGetterForm.Getter3 => 1 << 2,
            _ => 0,
        };
    }

    internal static void RecordShinDragonTransform(Player owner)
    {
        if (TryGetActiveProgress(owner, out PlayerProgress progress))
            progress.AtomicFormsMask = AllAtomicFormsMask;
    }

    internal static void RecordCardPlayed(Player owner, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card.Owner, owner)
            || !TryGetActiveProgress(owner, out PlayerProgress progress))
        {
            return;
        }

        switch (cardPlay.Card)
        {
            case SGC_TripleUnity:
                progress.TripleUnityPlayCount++;
                break;
            case SGC_Ki or SGC_Spirit or SGC_SuperKi:
                progress.SpiritCommandPlayCount++;
                break;
        }
    }

    internal static async Task TryGrantAfterHandDraw(Player owner)
    {
        if (owner.Creature.CombatState is not CombatState combatState
            || !CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        PlayerProgress progress = GetProgress(combatState, owner);
        if (progress.HasGrantedCard
            || DeckAlreadyContainsStonerSunshine(owner)
            || PileType.Hand.GetPile(owner).Cards.Count >= CardPile.MaxCardsInHand)
        {
            return;
        }

        decimal chance = CalculateAppearanceChance(owner, combatState, progress);
        float roll = owner.RunState.Rng.CombatCardSelection.NextFloat();
        if ((decimal)roll >= chance)
            return;

        CardModel card = combatState.CreateCard<SGC_StonerSunshine>(owner);
        CardPileAddResult result = await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, owner);
        if (!result.success)
            return;

        progress.HasGrantedCard = true;
        CardCmd.PreviewCardPileAdd(result, 1.5f);
        ShinGetterExecutionMusicService.TryStartFromStonerSunshineArrival(owner, card);
        ShinGetterVoiceService.PlayStonerSunshineArrival(owner);
    }

    internal static void RecordFinalKill(Player owner, AttackCommand attackCommand)
    {
        if (!ReferenceEquals(attackCommand.Attacker, owner.Creature)
            || attackCommand.ModelSource is not SGC_StonerSunshine cardSource
            || !ReferenceEquals(cardSource.Owner, owner)
            || owner.Creature.CombatState is not CombatState combatState
            || !attackCommand.Results.SelectMany(results => results).Any(
                result => result.WasTargetKilled && result.Receiver.Side == CombatSide.Enemy)
            || combatState.Enemies.Any(enemy => enemy.IsAlive))
        {
            return;
        }

        GetProgress(combatState, owner).FinishedCombatWithStonerSunshine = true;
    }

    internal static void AddVictoryReward(Player owner, CombatRoom room)
    {
        PlayerProgress progress = GetProgress(room.CombatState, owner);
        if (!progress.FinishedCombatWithStonerSunshine || progress.VictoryRewardAdded)
            return;

        progress.VictoryRewardAdded = true;
        CardModel rewardCard = owner.RunState.CreateCard<SGC_StonerSunshine>(owner);
        room.AddExtraReward(owner, new SpecialCardReward(rewardCard, owner));
    }

    private static decimal CalculateAppearanceChance(
        Player owner,
        CombatState combatState,
        PlayerProgress progress)
    {
        int completedTurns = Math.Max((owner.PlayerCombatState?.TurnNumber ?? 1) - 1, 0);
        decimal chance = completedTurns * TurnChancePerCompletedTurn;
        if ((progress.AtomicFormsMask & AllAtomicFormsMask) == AllAtomicFormsMask)
            chance += AllFormsChance;

        chance += progress.TripleUnityPlayCount * TripleUnityChancePerPlay;
        if (AreAllLivingEnemiesBelowThirtyPercent(combatState))
            chance += AllEnemiesLowHpChance;
        chance += progress.SpiritCommandPlayCount * SpiritCommandChancePerPlay;
        return chance;
    }

    private static bool AreAllLivingEnemiesBelowThirtyPercent(CombatState combatState)
    {
        Creature[] livingEnemies = combatState.Enemies.Where(enemy => enemy.IsAlive).ToArray();
        return livingEnemies.Length > 0
               && livingEnemies.All(enemy => enemy.CurrentHp * 100m < enemy.MaxHp * 30m);
    }

    private static bool DeckAlreadyContainsStonerSunshine(Player owner) =>
        owner.Deck.Cards.Any(card => card is SGC_StonerSunshine);

    private static bool TryGetActiveProgress(Player owner, out PlayerProgress progress)
    {
        progress = null!;
        if (!CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsOverOrEnding
            || owner.Creature.CombatState is not CombatState combatState)
        {
            return false;
        }

        progress = GetProgress(combatState, owner);
        return true;
    }

    private static PlayerProgress GetProgress(CombatState combatState, Player owner) =>
        CombatStates.GetValue(combatState, _ => new CombatProgress()).Players.GetValue(
            owner,
            _ => new PlayerProgress());

    private sealed class CombatProgress
    {
        internal readonly ConditionalWeakTable<Player, PlayerProgress> Players = new();
    }

    private sealed class PlayerProgress
    {
        internal int AtomicFormsMask;
        internal int TripleUnityPlayCount;
        internal int SpiritCommandPlayCount;
        internal bool HasGrantedCard;
        internal bool FinishedCombatWithStonerSunshine;
        internal bool VictoryRewardAdded;

        internal void Reset()
        {
            AtomicFormsMask = 0;
            TripleUnityPlayCount = 0;
            SpiritCommandPlayCount = 0;
            HasGrantedCard = false;
            FinishedCombatWithStonerSunshine = false;
            VictoryRewardAdded = false;
        }
    }
}
