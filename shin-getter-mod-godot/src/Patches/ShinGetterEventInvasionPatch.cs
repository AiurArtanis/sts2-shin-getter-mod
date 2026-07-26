using System.Collections.Generic;
using System;
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
    }

    private static Control CreateSingleIcon(string key)
    {
        string path = key.Contains(".HAYATO", StringComparison.Ordinal)
            ? GetterTwoIcon
            : key.Contains(".MUQING", StringComparison.Ordinal)
                ? GetterThreeIcon
                : GetterOneIcon;
        return CreateIcon(path, new Rect2(-82f, 20f, 60f, 60f));
    }

    private static Control CreateTripleIcon()
    {
        var container = new Control
        {
            Position = new Vector2(-94f, 12f),
            Size = new Vector2(78f, 76f),
        };
        container.AddChild(CreateIcon(GetterOneIcon, new Rect2(24f, 0f, 30f, 30f)));
        container.AddChild(CreateIcon(GetterTwoIcon, new Rect2(4f, 40f, 30f, 30f)));
        container.AddChild(CreateIcon(GetterThreeIcon, new Rect2(44f, 40f, 30f, 30f)));
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
