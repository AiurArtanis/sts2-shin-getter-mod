using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GetDamageResultsPreview))]
internal static class ShinGetterDamagePreviewPatch
{
    // The native preview is synchronous but calls the same HP-loss listeners as actual damage.
    [ThreadStatic]
    private static bool _active;

    internal static bool IsActive => _active;

    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
        __state = _active;
        _active = true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(bool __state) => _active = __state;
}
