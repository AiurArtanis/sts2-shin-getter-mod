#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class ShinGetterCreatureAnimationPatch
{
    private static void Prefix(NCreature __instance, string trigger)
    {
        if (__instance.Entity?.Player?.Character is not ShinGetter)
            return;

        if (trigger is not ("Attack" or "Cast"))
            return;

        NShinGetterStaticVisuals.TryPlayGetterActionAnimation(__instance, trigger);
    }
}
