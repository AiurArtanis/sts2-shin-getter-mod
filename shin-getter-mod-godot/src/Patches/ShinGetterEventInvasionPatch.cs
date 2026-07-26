using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Events;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(EventModel), "SetEventState")]
internal static class ShinGetterEventInvasionPatch
{
    private static void Prefix(
        EventModel __instance,
        LocString description,
        ref IEnumerable<EventOption> eventOptions)
    {
        eventOptions = ShinGetterEventInvasionService.AppendOptions(__instance, eventOptions);
    }
}
