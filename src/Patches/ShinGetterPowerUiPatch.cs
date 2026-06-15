#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NPower), "_Ready")]
internal static class ShinGetterPowerHoverPatch
{
    private static void Postfix(NPower __instance)
    {
        __instance.MouseFilter = Control.MouseFilterEnum.Stop;
        __instance.GetNode<Control>("%Icon").MouseFilter = Control.MouseFilterEnum.Ignore;
        __instance.GetNode<Control>("%AmountLabel").MouseFilter = Control.MouseFilterEnum.Ignore;
    }
}

[HarmonyPatch(typeof(NPowerRemovedVfx), nameof(NPowerRemovedVfx.Create))]
internal static class ShinGetterPowerRemovedVfxPatch
{
    private static bool Prefix(PowerModel power, ref NPowerRemovedVfx? __result)
    {
        if (power.GetType().Namespace != typeof(SGP_Ki).Namespace)
            return true;

        __result = null;
        return false;
    }
}
