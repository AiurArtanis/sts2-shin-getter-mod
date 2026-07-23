#nullable enable
using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
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
