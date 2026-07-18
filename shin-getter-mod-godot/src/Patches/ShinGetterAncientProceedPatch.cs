#nullable enable
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(AncientEventModel), "GenerateInitialOptionsWrapper")]
internal static class ShinGetterAncientProceedPatch
{
    private static void Postfix(AncientEventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (__result.Count != 0)
            return;

        if (__instance.Owner?.Character is not ShinGetter)
            return;

        __result = new[]
        {
            new EventOption(__instance, NEventRoom.Proceed, "PROCEED", disableOnChosen: false, isProceed: true),
        };
    }
}
