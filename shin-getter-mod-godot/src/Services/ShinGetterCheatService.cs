#nullable enable
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Modifiers;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Services;

internal static class ShinGetterCheatService
{
    private static readonly ConditionalWeakTable<RunState, ActivationState> States = new();

    internal static CmdResult TryActivate(Player? owner)
    {
        if (owner?.Character is not ShinGetter || owner.RunState is not RunState run
            || owner.Creature.CombatState is not CombatState combat
            || owner.PlayerCombatState == null || !IsActive(owner, combat)
            || !ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), run))
            return new(false, "This command requires a living Shin Getter player in an active combat.");
        if (owner.Creature.GetPower<SGP_Seal>() != null)
            return new(false, "Transformation is sealed. No cheat effects were applied.");

        ActivationState state = States.GetOrCreateValue(run);
        if (state.Running)
            return new(false, "A Shin Getter cheat sequence is already running.");
        bool multiplayer = run.Players.Count > 1;
        if (multiplayer && run.Modifiers.Any(modifier => modifier is SGM_PhysicsOverride))
            return new(false, "The team's one cheat activation for this run has already been used.");

        // The native network queue serializes contenders. Claim synchronously before
        // any await, so a second submission cannot duplicate partially applied effects.
        state.Running = true;
        try
        {
            if (multiplayer)
            {
                var marker = ModelDb.Modifier<SGM_PhysicsOverride>().ToMutable();
                marker.OnRunCreated(run);
                run.AddModifierDebug(marker);
            }
        }
        catch (Exception ex)
        {
            state.Running = false;
            Log.Error($"ShinGetter cheat activation could not be reserved: {ex}");
            return new(false, "Could not reserve this activation. No cheat effects were applied.");
        }
        return new(Execute(owner, combat, state), true,
            "Defying physics. Multiplayer activations are shared once per run.");
    }

    private static bool IsActive(Player owner, CombatState combat) =>
        RunManager.Instance.IsInProgress && CombatManager.Instance.IsInProgress
        && !CombatManager.Instance.IsOverOrEnding && !owner.Creature.IsDead
        && ReferenceEquals(owner.Creature.CombatState, combat)
        && ReferenceEquals(CombatManager.Instance.DebugOnlyGetState(), combat);

    private static async Task Execute(Player owner, CombatState combat, ActivationState state)
    {
        try
        {
            // Exhaust hooks can draw/autoplay a Strike that opens a native card picker.
            // Match native console commands: retain choice synchronization while the
            // enclosing ConsoleCmdGameAction waits for this whole sequence.
            var context = new BlockingPlayerChoiceContext();
            if (owner.Creature.GetPower<SGP_ShinForm>() != null)
                await ShinGetterCardBase.Transform(context, owner, null, playVoice: false);
            else
                await ShinGetterCardBase.TransformTo(context, owner, ShinGetterForm.Getter1, null, playVoice: false);
            if (!IsActive(owner, combat)) return;
            await ShinGetterVoiceService.PlayCheatRoar(owner);
            if (!IsActive(owner, combat)) return;
            await PlayerCmd.GainEnergy(10m, owner);
            await PowerCmd.Apply<SGP_Ki>(context, owner.Creature, 10m, owner.Creature, null);
            if (!IsActive(owner, combat)) return;

            // Native exhaustion preserves card/relic hooks. Only the original hand
            // is exhausted; cards drawn by those hooks must not cause an endless loop.
            foreach (CardModel card in PileType.Hand.GetPile(owner).Cards.ToArray())
            {
                if (!IsActive(owner, combat)) return;
                if (card.Pile?.Type == PileType.Hand)
                    await CardCmd.Exhaust(context, card);
            }
            if (!IsActive(owner, combat)) return;

            CardModel[] rewards =
            {
                combat.CreateCard<SGC_Desperation>(owner),
                combat.CreateCard<SGC_Indomitable>(owner),
                combat.CreateCard<SGC_GetterNova>(owner),
                combat.CreateCard<SGC_AwakenedSoul>(owner),
                combat.CreateCard<SGC_StarSlash>(owner),
            };
            foreach (CardModel reward in rewards)
            {
                if (!IsActive(owner, combat)) return;
                // Preserve hook-drawn extras in discard, reserving room for the requested
                // cards instead of silently overflowing a cheat reward out of the hand.
                var hand = PileType.Hand.GetPile(owner);
                if (hand.Cards.Count >= CardPile.MaxCardsInHand)
                {
                    CardModel? extra = hand.Cards.LastOrDefault(card => !rewards.Contains(card));
                    if (extra != null)
                        await CardPileCmd.Add(extra, PileType.Discard);
                }
                await CardPileCmd.AddGeneratedCardToCombat(reward, PileType.Hand, owner);
            }
            if (IsActive(owner, combat))
                ShinGetterExecutionMusicService.TryStartFromCheat(owner);
        }
        catch (Exception ex)
        {
            // Effects may already have triggered native hooks. Never roll back the
            // shared claim or grant again after a partially executed sequence.
            Log.Error($"ShinGetter cheat sequence stopped; the multiplayer claim remains consumed: {ex}");
        }
        finally
        {
            state.Running = false;
        }
    }

    private sealed class ActivationState
    {
        public bool Running;
    }
}
