#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Events;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(RunManager), "InitializeRunLobby")]
internal static class ShinGetterBs2MandalaPatch
{
    private static void Postfix()
    {
        // New runs and cold-start save loads both initialize the lobby after all mods have loaded.
        ShinGetterBs2MandalaCompatibility.TryInstall();
    }
}

internal static class ShinGetterBs2MandalaCompatibility
{
    private const string Bs2AssemblyName = "St2bs";
    private const string SmokeForestTypeName = "St2bs.St2bsCode.Acts.SmokeForest";
    private static bool _installationAttempted;
    private static bool _disabled;

    internal static void TryInstall()
    {
        if (_installationAttempted)
            return;
        _installationAttempted = true;

        try
        {
            Assembly? bs2 = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == Bs2AssemblyName);
            if (bs2 == null)
                return;

            Type? smokeForest = bs2.GetType(SmokeForestTypeName, throwOnError: false);
            MethodInfo? getter = smokeForest?.GetProperty("ExploreEvents",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)?.GetMethod;
            if (smokeForest == null || !typeof(ActModel).IsAssignableFrom(smokeForest)
                || getter == null || getter.IsStatic || getter.GetParameters().Length != 0
                || getter.ReturnType != typeof(IReadOnlyList<EventModel>))
            {
                Log.Warn("ShinGetterMod: BS2 Mandala compatibility skipped; SmokeForest.ExploreEvents interface changed.");
                return;
            }

            var harmony = new Harmony("Artanis.ShinGetterMod.Bs2Mandala");
            harmony.Patch(getter, postfix: new HarmonyMethod(typeof(ShinGetterBs2MandalaCompatibility),
                nameof(AfterExploreEvents)));
            Log.Info("ShinGetterMod: BS2 SmokeForest Mandala candidate enabled.");
        }
        catch (Exception exception)
        {
            Log.Warn($"ShinGetterMod: BS2 Mandala compatibility skipped: {exception.Message}");
        }
    }

    private static void AfterExploreEvents(ActModel __instance, ref IReadOnlyList<EventModel> __result)
    {
        if (_disabled || __result == null)
            return;

        try
        {
            RunState? runState = RunManager.Instance.DebugOnlyGetState();
            if (runState == null || !ReferenceEquals(runState.Act, __instance))
                return;

            EventModel mandala = ModelDb.Event<SGE_GetterMandala>();
            if (!mandala.IsAllowed(runState) || __result.Any(candidate => candidate?.Id == mandala.Id))
                return;

            // BS2 returns a fresh list on every read. Extend each result before BS2 filters and rolls it.
            // Do not replace PickExploreEvent's result: BS2 has already marked that event as visited.
            __result = new List<EventModel>(__result) { mandala };
        }
        catch (Exception exception)
        {
            _disabled = true;
            Log.Warn($"ShinGetterMod: BS2 Mandala candidate disabled: {exception.Message}");
        }
    }
}
