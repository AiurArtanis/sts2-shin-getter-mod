#nullable enable
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShinGetterMod.Services;

namespace ShinGetterMod.Patches;

internal static class ShinGetterControllerMappingCompatibility
{
    private sealed class State { internal Dictionary<StringName, StringName>? Normalized; }
    private static readonly ConditionalWeakTable<NInputManager, State> States = new();
    private static readonly AccessTools.FieldRef<NInputManager, Dictionary<StringName, StringName>> RuntimeMap =
        AccessTools.FieldRefAccess<NInputManager, Dictionary<StringName, StringName>>("_controllerInputMap");

    internal static void EnsureValid(NInputManager manager)
    {
        var map = RuntimeMap(manager);
        if (map == null || map.Count == 0 || manager.ControllerManager == null) return;
        var state = States.GetOrCreateValue(manager);
        if (ReferenceEquals(map, state.Normalized)) return;
        var normalized = ShinGetterControllerMappingPolicy.Normalize(map,
            manager.ControllerManager.GetDefaultControllerInputMap, InputMap.HasAction, out int repaired);
        RuntimeMap(manager) = normalized;
        state.Normalized = normalized;
        if (repaired != 0)
            GD.Print($"Shin Getter input compatibility: recovered {repaired} invalid runtime controller bindings; saved settings unchanged.");
    }

    internal static void Invalidate(NInputManager manager)
    {
        if (States.TryGetValue(manager, out var state)) state.Normalized = null;
    }
}

[HarmonyPatch]
internal static class ShinGetterControllerMappingConsumePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NInputManager), "_UnhandledInput");
        yield return AccessTools.Method(typeof(NInputManager), nameof(NInputManager.GetHotkeyIcon), new[] { typeof(string) });
    }
    private static void Prefix(NInputManager __instance) => ShinGetterControllerMappingCompatibility.EnsureValid(__instance);
}

[HarmonyPatch(typeof(NInputManager), nameof(NInputManager.ModifyControllerButton))]
internal static class ShinGetterControllerMappingRebindPatch
{
    // The native setter mutates its dictionary in place and emits InputRebound.
    // Invalidate before that emission; re-validation remains consumer-side.
    private static void Prefix(NInputManager __instance) => ShinGetterControllerMappingCompatibility.Invalidate(__instance);
}
