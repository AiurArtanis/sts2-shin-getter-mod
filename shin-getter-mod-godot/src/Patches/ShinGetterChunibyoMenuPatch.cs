#nullable enable
using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using ShinGetterMod.Config;
using ShinGetterMod.Nodes.Config;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NMainMenuSubmenuStack), nameof(NMainMenuSubmenuStack.GetSubmenuType), typeof(Type))]
internal static class ShinGetterChunibyoSubmenuPatch
{
    private static readonly ConditionalWeakTable<NMainMenuSubmenuStack, NChunibyoConfigSubmenu> Submenus = new();

    private static bool Prefix(NMainMenuSubmenuStack __instance, Type type, ref NSubmenu __result)
    {
        if (type != typeof(NChunibyoConfigSubmenu))
            return true;

        __result = Submenus.GetValue(__instance, stack =>
        {
            var submenu = new NChunibyoConfigSubmenu
            {
                Name = "ShinGetterChunibyoConfig",
                Visible = false,
            };
            stack.AddChild(submenu);
            return submenu;
        });
        return false;
    }
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class ShinGetterChunibyoMainMenuPatch
{
    private const string ButtonName = "ShinGetterChunibyoButton";
    private static readonly System.Reflection.FieldInfo? LastHitButtonField =
        AccessTools.Field(typeof(NMainMenu), "_lastHitButton");

    private static void Prefix(NMainMenu __instance)
    {
        ShinGetterChunibyoConfigService.Load();
        if (!ShinGetterChunibyoConfigService.Current.ShowInMainMenu)
            return;

        try
        {
            NMainMenuTextButton? settingsButton =
                __instance.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SettingsButton");
            if (settingsButton == null ||
                __instance.GetNodeOrNull<NMainMenuTextButton>($"MainMenuTextButtons/{ButtonName}") != null)
            {
                return;
            }

            var button = (NMainMenuTextButton)settingsButton.Duplicate();
            button.Name = ButtonName;
            button.UniqueNameInOwner = false;
            button.CustomMinimumSize = new Vector2(300f, button.CustomMinimumSize.Y);
            button.Connect(
                NClickableControl.SignalName.Released,
                Callable.From(new Action<NButton>(_ =>
                {
                    LastHitButtonField?.SetValue(__instance, button);
                    __instance.SubmenuStack.PushSubmenuType<NChunibyoConfigSubmenu>();
                })));

            settingsButton.AddSibling(button);
            button.SetLocalization("SHIN_GETTER_CHUNIBYO");
            var self = new NodePath(".");
            button.FocusNeighborLeft = self;
            button.FocusNeighborRight = self;
        }
        catch (Exception ex)
        {
            GD.PushError($"Shin Getter could not add the Chunibyo Config main-menu entry: {ex}");
        }
    }
}

[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]
internal static class ShinGetterChunibyoSettingsEntryPatch
{
    private const string EntryName = "ShinGetterChunibyoSettings";

    private static void Postfix(NSettingsScreen __instance)
    {
        try
        {
            var modding = __instance.GetNodeOrNull<MarginContainer>("%Modding");
            var sourceButton = __instance.GetNodeOrNull<NOpenModdingScreenButton>("%ModdingButton");
            if (modding?.GetParent() is not VBoxContainer content
                || sourceButton == null
                || content.GetNodeOrNull<MarginContainer>(EntryName) != null)
            {
                return;
            }

            var divider = (ColorRect)__instance.GetNode<ColorRect>("%ModdingDivider").Duplicate();
            divider.Name = EntryName + "Divider";
            divider.UniqueNameInOwner = false;
            divider.Visible = modding.Visible;

            var entry = (MarginContainer)modding.Duplicate((int)(
                Node.DuplicateFlags.Groups
                | Node.DuplicateFlags.Scripts
                | Node.DuplicateFlags.UseInstantiation));
            entry.Name = EntryName;
            entry.UniqueNameInOwner = false;
            entry.Visible = modding.Visible;
            entry.GetNode<MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel>("Label").Text =
                Localize("SHIN_GETTER_CHUNIBYO.SETTINGS_ENTRY", "Chunibyo Config (Shin Getter Mod)");

            var button = entry.GetNode<NOpenModdingScreenButton>("ModdingButton");
            button.Name = "OpenChunibyoConfigButton";
            button.UniqueNameInOwner = false;
            MakeButtonRed(button.GetNode<TextureRect>("Image"));
            button.Connect(
                NClickableControl.SignalName.Released,
                Callable.From<NButton>(_ => FindMainMenuStack(__instance)?.PushSubmenuType<NChunibyoConfigSubmenu>()));
            int insertAt = modding.GetIndex();
            content.AddChild(entry);
            content.MoveChild(entry, insertAt);
            content.AddChild(divider);
            content.MoveChild(divider, insertAt + 1);
            button.GetNode<MegaCrit.Sts2.addons.mega_text.MegaLabel>("Label").SetTextAutoSize(
                Localize("SHIN_GETTER_CHUNIBYO.OPEN_CONFIG", "Open Config"));

            if (!sourceButton.IsEnabled)
                Callable.From(button.Disable).CallDeferred();
        }
        catch (Exception ex)
        {
            GD.PushError($"Shin Getter could not add the settings-screen Chunibyo Config entry: {ex}");
        }
    }

    private static NMainMenuSubmenuStack? FindMainMenuStack(Node node)
    {
        for (Node? current = node.GetParent(); current != null; current = current.GetParent())
        {
            if (current is NMainMenuSubmenuStack stack)
                return stack;
        }

        return null;
    }

    private static void MakeButtonRed(TextureRect image)
    {
        if (image.Material is not ShaderMaterial source)
            return;

        var material = (ShaderMaterial)source.Duplicate(true);
        material.ResourceLocalToScene = true;
        material.SetShaderParameter("h", 0.0f);
        material.SetShaderParameter("s", 1.55f);
        material.SetShaderParameter("v", 1.05f);
        image.Material = material;
    }

    private static string Localize(string key, string fallback) =>
        LocString.GetIfExists("settings_ui", key)?.GetFormattedText() ?? fallback;
}
