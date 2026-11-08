#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace ShinGetterMod.Services;

// Only Annotations uses this adapter. Native Hand/SimpleGrid selection has no Back
// entry; the native Deck screen accepts an explicit list and has a real Back button.
internal static class ShinGetterAnnotationsCardChoice
{
    internal sealed class RunAbortedException : OperationCanceledException { }

    internal static async Task<CardModel?> Select(PlayerChoiceContext context, Player owner,
        LocString prompt, Func<CardModel, bool> legal)
    {
        if (CombatManager.Instance.IsOverOrEnding || owner.PlayerCombatState == null) return null;
        var hand = owner.PlayerCombatState.Hand;
        CardModel[] cards = hand.Cards.Where(legal).ToArray();
        if (cards.Length == 0) return null;
        var prefs = new CardSelectorPrefs(prompt, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
        };
        // Preserve selectors supplied by native automatic effects; never install one here.
        if (CardSelectCmd.Selector is { } selector)
            return Valid((await selector.GetSelectedCards(cards, prefs.MinSelect, prefs.MaxSelect)).FirstOrDefault());

        RunManager manager = RunManager.Instance;
        var run = manager.DebugOnlyGetState();
        var combat = owner.Creature.CombatState;
        GameAction? action = context switch
        {
            GameActionPlayerChoiceContext manual => manual.Action,
            HookPlayerChoiceContext hook => hook.GameAction,
            _ => null,
        };
        bool Live() => run != null && !manager.IsCleaningUp && ReferenceEquals(manager.DebugOnlyGetState(), run)
            && ReferenceEquals(owner.Creature.CombatState, combat)
            && action?.State is not (GameActionState.Canceled or GameActionState.Finished);
        void EnsureLive() { if (!Live()) throw new RunAbortedException(); }

        EnsureLive();
        var synchronizer = manager.PlayerChoiceSynchronizer;
        uint choiceId = synchronizer.ReserveChoiceId(owner);
        await context.SignalPlayerChoiceBegun(PlayerChoiceOptions.CancelPlayCardActions);
        // Hook contexts create their action in SignalPlayerChoiceBegun.
        if (context is HookPlayerChoiceContext hookContext) action = hookContext.GameAction;
        EnsureLive();

        bool local = LocalContext.IsMe(owner) && manager.NetService.Type != NetGameType.Replay;
        NDeckCardSelectScreen? screen = null;
        NOverlayStack? stack = null;
        Action? handChanged = null;
        bool failed = false;
        try
        {
            CardModel[] result;
            SceneTree? tree = null;
            if (local)
            {
                NPlayerHand.Instance?.CancelAllCardPlay();
                var activeStack = NOverlayStack.Instance ?? throw new InvalidOperationException("Annotations has no overlay stack.");
                stack = activeStack;
                tree = activeStack.GetTree();
                var activeScreen = NDeckCardSelectScreen.Create(cards, prefs);
                screen = activeScreen;
                activeStack.Push(activeScreen);
                handChanged = () =>
                {
                    // Deck previews dereference Pile.Type. A yielded queue may remove a
                    // snapshotted Hand instance; close before it can become a stale preview.
                    if (GodotObject.IsInstanceValid(activeScreen) && !activeScreen.IsQueuedForDeletion()
                        && GodotObject.IsInstanceValid(activeStack) && activeScreen.GetParent() == activeStack
                        && cards.Any(card => card.Pile?.Type != PileType.Hand || card.Owner != owner))
                        activeStack.Remove(activeScreen);
                };
                hand.ContentsChanged += handChanged;
                try { result = (await screen.CardsSelected()).ToArray(); }
                catch (OperationCanceledException) when (Live())
                {
                    // Removing the overlay during the same live action is an empty choice.
                    // Run shutdown takes the separate abort path below and never requests resume.
                    result = Array.Empty<CardModel>();
                }
                EnsureLive();
                result = result.Length == 1 && cards.Contains(result[0]) && Valid(result[0]) != null
                    ? result : Array.Empty<CardModel>();
                synchronizer.SyncLocalChoice(owner, choiceId, PlayerChoiceResult.FromMutableCombatCards(result));
            }
            else
            {
                result = (await synchronizer.WaitForRemoteChoice(owner, choiceId)).AsCombatCards().ToArray();
                EnsureLive();
                tree = NOverlayStack.Instance?.GetTree();
            }

            await EndChoice(context, action, tree, EnsureLive);
            return result.Length == 1 && cards.Contains(result[0]) ? Valid(result[0]) : null;
        }
        catch (OperationCanceledException) when (!Live())
        {
            failed = true;
            throw new RunAbortedException();
        }
        catch { failed = true; throw; }
        finally
        {
            if (handChanged != null) hand.ContentsChanged -= handChanged;
            // Normal native Close/Confirm removes its own overlay after completing the
            // selection task. Removing it again here would race that native continuation.
            if (failed && screen != null && GodotObject.IsInstanceValid(screen) && !screen.IsQueuedForDeletion()
                && stack != null && GodotObject.IsInstanceValid(stack) && screen.GetParent() == stack)
                stack.Remove(screen);
        }

        CardModel? Valid(CardModel? card) => card != null && card.Owner == owner
            && card.Pile?.Type == PileType.Hand && legal(card) ? card : null;
    }

    private static async Task EndChoice(PlayerChoiceContext context, GameAction? action, SceneTree? tree, Action ensureLive)
    {
        ensureLive();
        if (action == null)
        {
            // Blocking contexts end synchronously; custom contexts retain their own protocol.
            await context.SignalPlayerChoiceEnded();
            return;
        }
        if (action.OwnerId == LocalContext.NetId)
            RunManager.Instance.ActionQueueSynchronizer.RequestResumeActionAfterPlayerChoice(action);
        // Native End immediately awaits a private resume source that Reset does not cancel.
        // Observe the actual native resumption first. Shutdown can abort this local wait
        // without leaving an unobserved native End task behind.
        while (action.State != GameActionState.Executing)
        {
            ensureLive();
            if (tree == null || !GodotObject.IsInstanceValid(tree)) throw new RunAbortedException();
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        ensureLive();
        await action.WaitForActionToResumeExecutingAfterPlayerChoice();
    }
}
