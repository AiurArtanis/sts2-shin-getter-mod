#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace ShinGetterMod.Patches;

internal static class ShinGetterCardMutationVisualBatch
{
    private static readonly Dictionary<NCard, (PileType pileType, CardPreviewMode previewMode)> Pending = new();
    private static int _depth;

    internal static void BeginBatch()
    {
        _depth++;
    }

    internal static void EndBatch()
    {
        if (_depth == 0 || --_depth > 0)
            return;

        var pending = new List<KeyValuePair<NCard, (PileType pileType, CardPreviewMode previewMode)>>(Pending);
        Pending.Clear();
        foreach (var entry in pending)
        {
            if (GodotObject.IsInstanceValid(entry.Key) && !entry.Key.IsQueuedForDeletion())
                entry.Key.UpdateVisuals(entry.Value.pileType, entry.Value.previewMode);
        }
    }

    internal static bool TryDefer(NCard card, PileType pileType, CardPreviewMode previewMode)
    {
        if (_depth <= 0)
            return false;

        Pending[card] = (pileType, previewMode);
        return true;
    }
}

[HarmonyPatch]
internal static class ShinGetterCardMutationPowerPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(GalvanicPower), nameof(GalvanicPower.BeforeCombatStart));
        yield return AccessTools.Method(typeof(HexPower), nameof(HexPower.AfterApplied));
        yield return AccessTools.Method(typeof(HexPower), nameof(HexPower.AfterRemoved));
        yield return AccessTools.Method(typeof(DampenPower), nameof(DampenPower.AfterApplied));
        yield return AccessTools.Method(typeof(DampenPower), nameof(DampenPower.AfterRemoved));
    }

    private static void Prefix()
    {
        ShinGetterCardMutationVisualBatch.BeginBatch();
    }

    private static void Postfix(ref Task __result)
    {
        __result = EndBatchAfter(__result);
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        if (__exception != null)
            ShinGetterCardMutationVisualBatch.EndBatch();
        return __exception;
    }

    private static async Task EndBatchAfter(Task task)
    {
        try
        {
            await task;
        }
        finally
        {
            ShinGetterCardMutationVisualBatch.EndBatch();
        }
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class ShinGetterCardMutationVisualPatch
{
    private static bool Prefix(NCard __instance, PileType pileType, CardPreviewMode previewMode) =>
        !ShinGetterCardMutationVisualBatch.TryDefer(__instance, pileType, previewMode);
}
