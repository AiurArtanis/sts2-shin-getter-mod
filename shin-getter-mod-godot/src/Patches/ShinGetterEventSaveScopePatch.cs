#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using ShinGetterMod.Events;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class ShinGetterEventNativeSnapshotPatch
{
    private static void Postfix(RunManager __instance, AbstractRoom? preFinishedRoom, SerializableRun __result)
        => ShinGetterEventMapSaveScope.CaptureNativeToSave(__instance, preFinishedRoom, __result);
}

// Continue increments and saves NumReloads before NetService is initialized and
// without calling ToSave. Adapt only this single native singleplayer call site.
[HarmonyPatch]
internal static class ShinGetterEventReloadSavePatch
{
    private static readonly MethodInfo NativeReload = AccessTools.Method(typeof(SaveManager), nameof(SaveManager.IncrementNumReloads));
    private static readonly MethodInfo ScopedReload = AccessTools.Method(typeof(ShinGetterEventReloadSavePatch), nameof(SaveReloadCheckpoint));

    private static MethodBase TargetMethod()
    {
        MethodInfo setup = AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedSingleplayer))
            ?? throw new MissingMethodException("RunManager.SetUpSavedSingleplayer");
        Type machine = setup.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMemberException("Singleplayer setup async state machine");
        return AccessTools.Method(machine, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var code = instructions.ToList();
        if (code.Count(i => i.Calls(NativeReload)) != 1)
            throw new InvalidOperationException("Expected one native singleplayer reload save call.");
        var fields = original.DeclaringType!.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo manager = fields.Single(f => f.FieldType == typeof(RunManager));
        FieldInfo state = fields.Single(f => f.FieldType == typeof(RunState) && f.Name == "state");
        foreach (CodeInstruction instruction in code)
        {
            if (!instruction.Calls(NativeReload)) { yield return instruction; continue; }
            var load = new CodeInstruction(OpCodes.Ldarg_0);
            load.labels.AddRange(instruction.labels);
            load.blocks.AddRange(instruction.blocks);
            yield return load;
            yield return new CodeInstruction(OpCodes.Ldfld, manager);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, state);
            yield return new CodeInstruction(OpCodes.Call, ScopedReload);
        }
    }

    private static async Task SaveReloadCheckpoint(SaveManager manager, SerializableRun save, NetGameType type, bool forceInTest,
        RunManager runManager, RunState state)
    {
        if (type != NetGameType.Singleplayer || !ReferenceEquals(runManager.DebugOnlyGetState(), state)
            || !ShinGetterEventInvasionService.OwnsFourthReloadCheckpoint(state, save))
        {
            await manager.IncrementNumReloads(save, type, forceInTest);
            return;
        }
        using var scope = ShinGetterEventMapSaveScope.InspectSnapshot(save);
        await manager.IncrementNumReloads(save, type, forceInTest);
    }
}
