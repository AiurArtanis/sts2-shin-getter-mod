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

[HarmonyPatch(typeof(NPower), "Reload")]
internal static class ShinGetterPowerIconFlashPatch
{
    private static void Postfix(NPower __instance)
    {
        if (__instance.Model.GetType().Namespace != typeof(SGP_Ki).Namespace)
            return;

        __instance.GetNode<CpuParticles2D>("%PowerFlash").Texture = __instance.Model.Icon;
    }
}

[HarmonyPatch(typeof(NPowerFlashVfx), "StartVfx")]
internal static class ShinGetterPowerBigFlashPatch
{
    private static readonly AccessTools.FieldRef<NPowerFlashVfx, PowerModel> PowerRef =
        AccessTools.FieldRefAccess<NPowerFlashVfx, PowerModel>("_power");

    private static readonly AccessTools.FieldRef<NPowerFlashVfx, Sprite2D> SpriteRef =
        AccessTools.FieldRefAccess<NPowerFlashVfx, Sprite2D>("_sprite");

    private static void Postfix(NPowerFlashVfx __instance)
    {
        PowerModel power = PowerRef(__instance);
        if (power.GetType().Namespace != typeof(SGP_Ki).Namespace)
            return;

        Sprite2D sprite = SpriteRef(__instance);
        sprite.Texture = power.Icon;
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
