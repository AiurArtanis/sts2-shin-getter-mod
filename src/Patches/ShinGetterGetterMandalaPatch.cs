#nullable enable
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Events;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyNextEvent))]
internal static class ShinGetterGetterMandalaPatch
{
    private const int MandalaActIndex = 1;

    private static void Postfix(IRunState runState, ref EventModel __result)
    {
        if (runState.CurrentActIndex != MandalaActIndex)
            return;

        if (!runState.Players.Any(player => player.Character is ShinGetter))
            return;

        EventModel getterMandala = ModelDb.Event<SGE_GetterMandala>();
        if (runState is RunState concreteRunState
            && concreteRunState.VisitedEventIds.Contains(getterMandala.Id))
            return;

        __result = getterMandala;
    }
}
