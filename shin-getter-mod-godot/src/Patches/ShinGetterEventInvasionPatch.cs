using System.Collections.Generic;
using System;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
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

[HarmonyPatch(typeof(NEventOptionButton), nameof(NEventOptionButton._Ready))]
internal static class ShinGetterEventOptionIconPatch
{
    private const string Prefix = "SHIN_GETTER_EVENT_INVASION.";
    private const float OptionWidth = 800f;
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
        Control icon = isTriple ? CreateTripleIcon() : CreateSingleIcon(key);
        icon.Name = "ShinGetterOptionIcon";
        icon.ZIndex = 8;
        icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (__instance.Option.IsLocked)
            icon.Modulate = new Color(0.62f, 0.62f, 0.62f, 0.58f);
        __instance.AddChild(icon);

        MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel label =
            __instance.GetNode<MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel>("%Text");
        label.OffsetRight = isTriple ? OptionWidth - 142f : OptionWidth - 84f;
    }

    private static Control CreateSingleIcon(string key)
    {
        string path = key.Contains(".HAYATO", StringComparison.Ordinal)
            ? GetterTwoIcon
            : key.Contains(".MUQING", StringComparison.Ordinal)
                ? GetterThreeIcon
                : GetterOneIcon;
        return CreateIcon(path, new Rect2(OptionWidth - 68f, 18f, 54f, 54f));
    }

    private static Control CreateTripleIcon()
    {
        var container = new Control
        {
            Position = new Vector2(OptionWidth - 132f, 26f),
            Size = new Vector2(118f, 48f),
        };
        container.AddChild(CreateIcon(GetterOneIcon, new Rect2(0f, 0f, 36f, 36f)));
        container.AddChild(CreateIcon(GetterTwoIcon, new Rect2(41f, 0f, 36f, 36f)));
        container.AddChild(CreateIcon(GetterThreeIcon, new Rect2(82f, 0f, 36f, 36f)));
        return container;
    }

    private static TextureRect CreateIcon(string path, Rect2 rect) => new()
    {
        Texture = ResourceLoader.Load<Texture2D>(path),
        Position = rect.Position,
        Size = rect.Size,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
}

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.AddOptions))]
internal static class ShinGetterEventContentCenterPatch
{
    private const string Prefix = "SHIN_GETTER_EVENT_INVASION.";
    private const float MinimumTopMargin = 72f;
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
        float top = Mathf.Max(MinimumTopMargin, (layout.Size.Y - contentHeight) * 0.5f);
        content.Position = new Vector2(content.Position.X, top);
    }
}
