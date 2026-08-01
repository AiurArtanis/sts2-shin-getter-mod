using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Rooms;
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

[HarmonyPatch(typeof(EventModel), "get_IsShared")]
internal static class ShinGetterSinglePlayerEventCombatPatch
{
    private static bool Prefix(EventModel __instance, ref bool __result)
    {
        if (!ShinGetterEventInvasionService.IsEnteringSinglePlayerEventCombat(__instance))
            return true;

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.Resume))]
internal static class ShinGetterByrdonisNestResumePatch
{
    private static void Postfix(
        EventModel __instance,
        AbstractRoom exitedRoom,
        ref Task __result)
    {
        if (__instance is ByrdonisNest byrdonisNest)
        {
            __result = ShinGetterEventInvasionService.ResumeByrdonisNest(
                byrdonisNest,
                exitedRoom,
                __result);
        }
    }
}

[HarmonyPatch(typeof(NEventOptionButton), nameof(NEventOptionButton._Ready))]
internal static class ShinGetterEventOptionIconPatch
{
    private const string Prefix = "SHIN_GETTER_EVENT_INVASION.";
    private const float IconRightInset = 18f;
    private const float IconTextGap = 12f;
    private const float SingleIconSize = 40.5f;
    private const float TripleIconSize = 27f;
    private const float TripleIconGap = 5f;
    private const string GetterOneIcon = "res://images/atlases/power_atlas.sprites/s_g_p_shin_getter_one.tres";
    private const string GetterTwoIcon = "res://images/atlases/power_atlas.sprites/s_g_p_shin_getter_two.tres";
    private const string GetterThreeIcon = "res://images/atlases/power_atlas.sprites/s_g_p_shin_getter_three.tres";

    private static void Postfix(NEventOptionButton __instance)
    {
        string key = __instance.Option.TextKey;
        if (!key.StartsWith(Prefix, StringComparison.Ordinal)
            || __instance.GetNodeOrNull<Control>("ShinGetterOptionIcon") != null)
        {
            return;
        }

        bool isTriple = key.Contains(".SPIRIT_GRAFTER.", StringComparison.Ordinal)
            || key.Contains(".WOOD_CARVINGS.", StringComparison.Ordinal);
        Control icon = CreateIconLayer(key, isTriple);
        icon.Name = "ShinGetterOptionIcon";
        icon.ZIndex = 8;
        icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (__instance.Option.IsLocked)
            icon.Modulate = new Color(0.62f, 0.62f, 0.62f, 0.58f);
        __instance.AddChild(icon);

        MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel label =
            __instance.GetNode<MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel>("%Text");
        float reservedWidth = isTriple
            ? IconRightInset + TripleIconSize * 3f + TripleIconGap * 2f + IconTextGap
            : IconRightInset + SingleIconSize + IconTextGap;
        label.AnchorRight = 1f;
        label.OffsetRight = -reservedWidth;
    }

    private static Control CreateIconLayer(string key, bool isTriple)
    {
        var layer = new Control();
        layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        if (isTriple)
        {
            string[] paths = { GetterOneIcon, GetterTwoIcon, GetterThreeIcon };
            for (int i = 0; i < paths.Length; i++)
            {
                float iconsToRight = paths.Length - 1 - i;
                float rightOffset = IconRightInset + iconsToRight * (TripleIconSize + TripleIconGap);
                layer.AddChild(CreateRightAnchoredIcon(paths[i], rightOffset, TripleIconSize));
            }
        }
        else
        {
            string path = key.Contains(".HAYATO", StringComparison.Ordinal)
                ? GetterTwoIcon
                : key.Contains(".MUQING", StringComparison.Ordinal)
                    ? GetterThreeIcon
                    : GetterOneIcon;
            layer.AddChild(CreateRightAnchoredIcon(path, IconRightInset, SingleIconSize));
        }

        return layer;
    }

    private static TextureRect CreateRightAnchoredIcon(string path, float rightOffset, float size)
    {
        var icon = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(path),
            AnchorLeft = 1f,
            AnchorTop = 0.5f,
            AnchorRight = 1f,
            AnchorBottom = 0.5f,
            OffsetLeft = -rightOffset - size,
            OffsetTop = -size * 0.5f,
            OffsetRight = -rightOffset,
            OffsetBottom = size * 0.5f,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        return icon;
    }
}

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.AddOptions))]
internal static class ShinGetterEventContentCenterPatch
{
    private const string Prefix = "SHIN_GETTER_EVENT_INVASION.";
    private const float FallbackTopBarHeight = 100f;
    private static readonly StringName ResizeConnectedMeta = "shin_getter_event_center_resize_connected";

    private static void Postfix(NEventLayout __instance)
    {
        VBoxContainer options = __instance.GetNodeOrNull<VBoxContainer>("%OptionsContainer");
        if (options == null)
            return;

        bool containsInvasionOption = options.GetChildren().OfType<NEventOptionButton>()
            .Any(button => button.Option.TextKey.StartsWith(Prefix, StringComparison.Ordinal));
        bool managesCentering = __instance.HasMeta(ResizeConnectedMeta);
        if (containsInvasionOption && !managesCentering)
        {
            __instance.SetMeta(ResizeConnectedMeta, true);
            __instance.Resized += () => QueueRecenter(__instance);
            managesCentering = true;
        }

        if (managesCentering)
            QueueRecenter(__instance);
    }

    private static void QueueRecenter(NEventLayout layout)
    {
        Callable.From(() => Recenter(layout)).CallDeferred();
    }

    private static void Recenter(NEventLayout layout)
    {
        if (!GodotObject.IsInstanceValid(layout))
            return;

        VBoxContainer options = layout.GetNodeOrNull<VBoxContainer>("%OptionsContainer");
        if (options?.GetParent() is not VBoxContainer content)
            return;

        float contentHeight = content.GetCombinedMinimumSize().Y;
        float availableTop = GetAvailableTop(layout);
        float availableHeight = Mathf.Max(0f, layout.Size.Y - availableTop);
        float top = availableTop + Mathf.Max(0f, (availableHeight - contentHeight) * 0.5f);
        content.Position = new Vector2(content.Position.X, top);
    }

    private static float GetAvailableTop(NEventLayout layout)
    {
        Control topBarBackground = NRun.Instance?.GlobalUi?.TopBar.GetNodeOrNull<Control>("BgImage");
        Transform2D layoutInverse = layout.GetGlobalTransformWithCanvas().AffineInverse();
        if (topBarBackground != null && GodotObject.IsInstanceValid(topBarBackground))
        {
            Vector2 globalBottom = topBarBackground.GetGlobalTransformWithCanvas()
                * new Vector2(0f, topBarBackground.Size.Y);
            return Mathf.Max(0f, (layoutInverse * globalBottom).Y);
        }

        Vector2 fallbackGlobalBottom = new(layout.GlobalPosition.X, FallbackTopBarHeight);
        return Mathf.Max(0f, (layoutInverse * fallbackGlobalBottom).Y);
    }
}
