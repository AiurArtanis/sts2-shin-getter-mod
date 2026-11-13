#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Patches;

// Two call-site adapters in the native manual action. The original action and
// OnPlayWrapper still own spending, replay count, history, hooks and result piles.
[HarmonyPatch]
internal static class ShinGetterAnnotationsPreparationPatch
{
    private static readonly MethodInfo NativeSpend = AccessTools.Method(typeof(CardModel), nameof(CardModel.SpendResources));
    private static readonly MethodInfo NativePlay = AccessTools.Method(typeof(CardModel), nameof(CardModel.OnPlayWrapper));
    private static readonly MethodInfo PrepareSpend = AccessTools.Method(typeof(ShinGetterAnnotationsPreparationPatch), nameof(PrepareResources));
    private static readonly MethodInfo PlayPrepared = AccessTools.Method(typeof(ShinGetterAnnotationsPreparationPatch), nameof(PlayPreparedCard));
    private static readonly PropertyInfo ActionChoiceContext = AccessTools.Property(typeof(PlayCardAction), nameof(PlayCardAction.PlayerChoiceContext));

    private static MethodBase TargetMethod()
    {
        MethodInfo execute = AccessTools.Method(typeof(PlayCardAction), "ExecuteAction");
        Type stateMachine = execute.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMemberException("PlayCardAction.ExecuteAction async state machine");
        return AccessTools.Method(stateMachine, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = instructions.ToList();
        if (code.Count(i => i.Calls(NativeSpend)) != 1 || code.Count(i => i.Calls(NativePlay)) != 1)
            throw new InvalidOperationException("Annotations preparation requires one native SpendResources and OnPlayWrapper call.");
        FieldInfo actionField = original.DeclaringType!.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(f => f.FieldType == typeof(PlayCardAction));
        foreach (CodeInstruction instruction in code)
        {
            if (instruction.Calls(NativePlay))
            {
                var call = new CodeInstruction(OpCodes.Call, PlayPrepared);
                call.labels.AddRange(instruction.labels);
                call.blocks.AddRange(instruction.blocks);
                yield return call;
                continue;
            }
            if (!instruction.Calls(NativeSpend)) { yield return instruction; continue; }
            var loadState = new CodeInstruction(OpCodes.Ldarg_0);
            loadState.labels.AddRange(instruction.labels);
            loadState.blocks.AddRange(instruction.blocks);
            yield return loadState;
            yield return new CodeInstruction(OpCodes.Ldfld, actionField);
            yield return new CodeInstruction(OpCodes.Call, PrepareSpend);
        }
    }

    private static Task PlayPreparedCard(CardModel card, PlayerChoiceContext context, Creature? target,
        bool isAutoPlay, ResourceInfo resources, bool skipCardPileVisuals)
    {
        // A canceled manual attempt must not even enter other OnPlayWrapper patches.
        if (card is SGC_Annotations annotations && annotations.ConsumeManualCancellation())
            return Task.CompletedTask;
        return card.OnPlayWrapper(context, target, isAutoPlay, resources, skipCardPileVisuals);
    }

    private static Task<(int, int)> PrepareResources(CardModel card, PlayCardAction action)
    {
        return card is SGC_Annotations annotations
            ? PrepareAnnotationsResources(annotations, action)
            : card.SpendResources();
    }

    private static async Task<(int, int)> PrepareAnnotationsResources(SGC_Annotations annotations, PlayCardAction action)
    {
        CardModel card = annotations;
        var context = new GameActionPlayerChoiceContext(action);
        ActionChoiceContext.SetValue(action, context);
        try
        {
            bool selected = await annotations.PrepareManualPlay(context);
            // Choosing yields the native action queue. Recheck hand, resources and target
            // after it resumes, before the original SpendResources can emit any hooks.
            if (!selected || card.Pile?.Type != PileType.Hand || !annotations.HasPreparedTarget
                || !card.CanPlay(out _, out _) || !card.IsValidTarget(action.Target))
            {
                annotations.CancelManualPreparation();
                RestorePreparationPresentation(card, action);
                return (0, 0);
            }
            return await card.SpendResources();
        }
        catch
        {
            annotations.CancelManualPreparation();
            try { RestorePreparationPresentation(card, action); }
            catch (Exception cleanupError)
            {
                // Native logging may already be disposed; preserve the original failure.
                try { Console.Error.WriteLine($"[ERROR] issue#287 annotations presentation cleanup failed: {cleanupError}"); }
                catch { /* Diagnostic failure must not replace the original exception. */ }
            }
            throw;
        }
    }

    private static void RestorePreparationPresentation(CardModel card, PlayCardAction action)
    {
        RunManager manager = RunManager.Instance;
        if (manager.IsCleaningUp || !ReferenceEquals(card.Owner, action.Player)
            || !ReferenceEquals(manager.DebugOnlyGetState(), action.Player.RunState)) return;
        var queue = NCardPlayQueue.Instance;
        var hand = NPlayerHand.Instance;
        if (queue == null || hand == null || !GodotObject.IsInstanceValid(queue) || !GodotObject.IsInstanceValid(hand)) return;
        // The choice has resumed this action. Canceling its state here would make
        // native Execute reject its completed Task; only undo the queued visuals.
        queue.RemoveCardFromQueueForCancellation(action);
        if (LocalContext.IsMe(action.Player)) hand.TryCancelCardPlay(card);
    }
}
