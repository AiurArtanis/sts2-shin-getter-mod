#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using ShinGetterMod.Events;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(EventSynchronizer), "ChooseOptionForEvent")]
internal static class ShinGetterFourthOptionInteractionPatch
{
    private static void Prefix(EventSynchronizer __instance, Player player, int optionIndex,
        out ShinGetterEventInteractionCheckpoint? __state)
        => __state = ShinGetterEventInvasionService.PrepareFourthInteraction(__instance, player, optionIndex);

    private static Exception? Finalizer(Exception? __exception, ShinGetterEventInteractionCheckpoint? __state)
    {
        if (__exception != null)
        {
            __state?.HistoryFailed(__exception);
            __state?.Dispose();
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(EventSynchronizer), "SaveEventOptionToHistory")]
internal static class ShinGetterFourthOptionHistoryPatch
{
    private static void Postfix(EventSynchronizer __instance, Player player, EventOption option)
        => ShinGetterEventInteractionCheckpoint.HistoryCompleted(__instance.GetEventForPlayer(player), __instance, player, option);
}

// Adapt only these two native command call sites, preserving their UI, discovery,
// synchronization and choice-ended code. No global screen or SaveAndQuit patch.
[HarmonyPatch]
internal static class ShinGetterFourthSelectionPresentationPatch
{
    private static readonly MethodInfo Choose = AccessTools.Method(typeof(NChooseACardSelectionScreen), nameof(NChooseACardSelectionScreen.CardsSelected));
    private static readonly MethodInfo Grid = AccessTools.Method(typeof(NCardGridSelectionScreen), nameof(NCardGridSelectionScreen.CardsSelected));
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { nameof(CardSelectCmd.FromChooseACardScreen), nameof(CardSelectCmd.FromSimpleGrid) })
        {
            MethodInfo command = AccessTools.Method(typeof(CardSelectCmd), name);
            Type machine = command.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                ?? throw new MissingMemberException("Native event selection async state machine");
            yield return AccessTools.Method(machine, "MoveNext");
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        if (code.Count(i => i.Calls(Choose) || i.Calls(Grid)) != 1)
            throw new InvalidOperationException("Expected exactly one native event selection-result call.");
        foreach (var instruction in code)
        {
            string? adapter = instruction.Calls(Choose) ? nameof(ChooseAfterPresentation)
                : instruction.Calls(Grid) ? nameof(GridAfterPresentation) : null;
            if (adapter == null) { yield return instruction; continue; }
            var replacement = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ShinGetterFourthSelectionPresentationPatch), adapter));
            replacement.labels.AddRange(instruction.labels);
            replacement.blocks.AddRange(instruction.blocks);
            yield return replacement;
        }
    }

    private static Task<IEnumerable<CardModel>> ChooseAfterPresentation(NChooseACardSelectionScreen screen)
        => ShinGetterEventInteractionCheckpoint.BeforeSelectionResult(screen.CardsSelected());
    private static Task<IEnumerable<CardModel>> GridAfterPresentation(NCardGridSelectionScreen screen)
        => ShinGetterEventInteractionCheckpoint.BeforeSelectionResult(screen.CardsSelected());
}
