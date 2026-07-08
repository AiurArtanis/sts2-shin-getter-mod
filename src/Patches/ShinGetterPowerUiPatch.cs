#nullable enable
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

[HarmonyPatch(typeof(NPower), "Reload")]
internal static class ShinGetterPowerIconTransitionPatch
{
    private const float TransitionSeconds = 0.28f;
    private static readonly Dictionary<Creature, Texture2D> RemovedFormIcons = new();

    private static void Postfix(NPower __instance)
    {
        PowerModel power = __instance.Model;
        if (!IsShinGetterFormPower(power) || !TryConsumeRemovedFormIcon(power, out Texture2D? previousIcon))
            return;

        TextureRect icon = __instance.GetNode<TextureRect>("%Icon");
        if (previousIcon == null || icon.Texture == previousIcon)
            return;

        var overlay = new TextureRect
        {
            Name = "PreviousFormIconOverlay",
            Texture = previousIcon,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = icon.ExpandMode,
            StretchMode = icon.StretchMode,
            CustomMinimumSize = icon.CustomMinimumSize,
            ZIndex = icon.ZIndex + 1,
        };

        icon.AddChild(overlay);
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.OffsetLeft = 0f;
        overlay.OffsetTop = 0f;
        overlay.OffsetRight = 0f;
        overlay.OffsetBottom = 0f;

        Tween tween = overlay.CreateTween();
        tween.TweenProperty(overlay, "modulate:a", 0f, TransitionSeconds)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        tween.TweenCallback(Callable.From(() => overlay.QueueFree()));
    }

    internal static void CacheRemovedFormIcon(PowerModel power)
    {
        if (!IsShinGetterFormPower(power) || power.Owner == null || power.Icon == null)
            return;

        RemovedFormIcons[power.Owner] = power.Icon;
    }

    private static bool TryConsumeRemovedFormIcon(PowerModel power, out Texture2D? icon)
    {
        icon = null;
        if (power.Owner == null || !RemovedFormIcons.TryGetValue(power.Owner, out icon))
            return false;

        RemovedFormIcons.Remove(power.Owner);
        return true;
    }

    private static bool IsShinGetterFormPower(PowerModel power) =>
        power is SGP_ShinGetterOne or SGP_ShinGetterTwo or SGP_ShinGetterThree or SGP_ShinForm;
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
        sprite.Texture = power.BigIcon;
    }
}

[HarmonyPatch(typeof(NPowerRemovedVfx), nameof(NPowerRemovedVfx.Create))]
internal static class ShinGetterPowerRemovedVfxPatch
{
    private static bool Prefix(PowerModel power, ref NPowerRemovedVfx? __result)
    {
        ShinGetterPowerIconTransitionPatch.CacheRemovedFormIcon(power);

        if (power.GetType().Namespace != typeof(SGP_Ki).Namespace)
            return true;

        __result = null;
        return false;
    }
}
