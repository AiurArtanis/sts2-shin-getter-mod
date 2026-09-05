#nullable enable
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
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
    private static readonly AccessTools.FieldRef<NPower, PowerModel?> ModelRef =
        AccessTools.FieldRefAccess<NPower, PowerModel?>("_model");

    private static void Postfix(NPower __instance)
    {
        PowerModel? power = ModelRef(__instance);
        if (power == null || power.GetType().Namespace != typeof(SGP_Ki).Namespace)
            return;

        __instance.GetNode<CpuParticles2D>("%PowerFlash").Texture = power.Icon;
    }
}

[HarmonyPatch(typeof(NPower), "Reload")]
internal static class ShinGetterPowerIconTransitionPatch
{
    private const float TransitionSeconds = 0.28f;
    private static readonly ConditionalWeakTable<Creature, RemovedFormIconCache> RemovedFormIcons = new();
    private static readonly ConditionalWeakTable<NPowerContainer, RetainedFormPowerNode> RetainedFormPowerNodes = new();
    private static readonly AccessTools.FieldRef<NPowerContainer, List<NPower>> PowerNodesRef =
        AccessTools.FieldRefAccess<NPowerContainer, List<NPower>>("_powerNodes");

    private static void Postfix(NPower __instance)
    {
        if (!__instance.IsNodeReady())
            return;

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
            Position = icon.Position,
            Size = icon.Size,
            PivotOffset = icon.PivotOffset,
            Material = icon.Material,
            ZIndex = icon.ZIndex + 1,
        };

        __instance.AddChild(overlay);
        icon.Modulate = new Color(icon.Modulate, 0f);

        Tween tween = overlay.CreateTween();
        tween.TweenProperty(overlay, "modulate:a", 0f, TransitionSeconds)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        tween.TweenCallback(Callable.From(() => overlay.QueueFree()));

        Tween iconTween = icon.CreateTween();
        iconTween.TweenProperty(icon, "modulate:a", 1f, TransitionSeconds)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
    }

    internal static bool TryRetainRemovedFormPowerNode(NPowerContainer container, PowerModel power)
    {
        if (!ShinGetterCardFramePatch.IsFormTransitionActive || !IsShinGetterFormPower(power))
            return false;

        List<NPower> powerNodes = PowerNodesRef(container);
        NPower? retainedNode = powerNodes.FirstOrDefault(node => ReferenceEquals(node.Model, power));
        if (retainedNode == null)
            return false;

        RetainedFormPowerNodes.Remove(container);
        RetainedFormPowerNodes.Add(container, new RetainedFormPowerNode(retainedNode));
        CacheRemovedFormIcon(power);
        return true;
    }

    internal static bool TryReuseRetainedFormPowerNode(NPowerContainer container, PowerModel power)
    {
        if (!ShinGetterCardFramePatch.IsFormTransitionActive
            || !IsShinGetterFormPower(power)
            || !RetainedFormPowerNodes.TryGetValue(container, out RetainedFormPowerNode? retained)
            || retained == null
            || !GodotObject.IsInstanceValid(retained.Node))
        {
            return false;
        }

        NPower retainedNode = retained.Node;
        RetainedFormPowerNodes.Remove(container);
        retainedNode.Model = power;
        return true;
    }

    internal static void CacheRemovedFormIcon(PowerModel power)
    {
        bool canCache = CombatManager.Instance.IsInProgress
            && !CombatManager.Instance.IsEnding;
        if (!canCache)
            return;

        if (!IsShinGetterFormPower(power) || power.Owner == null || power.Icon == null)
            return;

        RemovedFormIcons.Remove(power.Owner);
        RemovedFormIcons.Add(power.Owner, new RemovedFormIconCache(power.Icon));
    }

    private static bool TryConsumeRemovedFormIcon(PowerModel power, out Texture2D? icon)
    {
        icon = null;
        if (power.Owner == null
            || !RemovedFormIcons.TryGetValue(power.Owner, out RemovedFormIconCache? cache)
            || cache == null)
            return false;

        icon = cache.Icon;
        RemovedFormIcons.Remove(power.Owner);
        return true;
    }

    internal static bool IsShinGetterFormPower(PowerModel power) =>
        power is SGP_ShinGetterOne or SGP_ShinGetterTwo or SGP_ShinGetterThree or SGP_ShinForm;

    private sealed record RemovedFormIconCache(Texture2D Icon);
    private sealed record RetainedFormPowerNode(NPower Node);
}

[HarmonyPatch(typeof(NPowerContainer), "Remove")]
internal static class ShinGetterPowerContainerRemovePatch
{
    private static bool Prefix(NPowerContainer __instance, PowerModel power)
    {
        return !ShinGetterPowerIconTransitionPatch.TryRetainRemovedFormPowerNode(__instance, power);
    }
}

[HarmonyPatch(typeof(NPowerContainer), "Add")]
internal static class ShinGetterPowerContainerAddPatch
{
    private static bool Prefix(NPowerContainer __instance, PowerModel power)
    {
        return !ShinGetterPowerIconTransitionPatch.TryReuseRetainedFormPowerNode(__instance, power);
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
        sprite.Texture = power.BigIcon;
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
