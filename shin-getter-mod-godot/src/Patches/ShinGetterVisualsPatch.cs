#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Nodes.Screens;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectBg")]
internal static class ShinGetterBgPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
        {
            __result = "res://scenes/screens/char_select/char_select_bg_shin_getter.tscn";
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class ShinGetterCharacterSelectBgLayoutPatch
{
    private static void Postfix(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        if (characterModel is ShinGetter)
            ShinGetterCharacterSelectBgPatchHelpers.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), "PlayerChanged")]
internal static class ShinGetterRandomCharacterSelectBgLayoutPatch
{
    private static void Postfix(
        NCharacterSelectScreen __instance,
        StartRunLobbyPlayer player,
        bool isRandomCharacterResolution)
    {
        if (isRandomCharacterResolution && player.character is ShinGetter)
            ShinGetterCharacterSelectBgPatchHelpers.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), nameof(NMultiplayerLoadGameScreen.InitializeAsHost))]
internal static class ShinGetterMultiplayerLoadHostBgLayoutPatch
{
    private static void Postfix(NMultiplayerLoadGameScreen __instance) =>
        ShinGetterCharacterSelectBgPatchHelpers.Refresh(__instance);
}

[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), nameof(NMultiplayerLoadGameScreen.InitializeAsClient))]
internal static class ShinGetterMultiplayerLoadClientBgLayoutPatch
{
    private static void Postfix(NMultiplayerLoadGameScreen __instance) =>
        ShinGetterCharacterSelectBgPatchHelpers.Refresh(__instance);
}

[HarmonyPatch(typeof(NCharacterSelectScreenBg), "OnWindowChange")]
internal static class ShinGetterCharacterSelectBgResizePatch
{
    private static void Postfix(NCharacterSelectScreenBg __instance) =>
        NShinGetterCharacterSelectBackground.RefreshMarkedBackgrounds(__instance);
}

internal static class ShinGetterCharacterSelectBgPatchHelpers
{
    private static readonly AccessTools.FieldRef<NCharacterSelectScreen, Control> CharacterSelectBgContainerRef =
        AccessTools.FieldRefAccess<NCharacterSelectScreen, Control>("_bgContainer");

    private static readonly AccessTools.FieldRef<NMultiplayerLoadGameScreen, Control> MultiplayerLoadBgContainerRef =
        AccessTools.FieldRefAccess<NMultiplayerLoadGameScreen, Control>("_bgContainer");

    internal static void Refresh(NCharacterSelectScreen screen) =>
        NShinGetterCharacterSelectBackground.RefreshMarkedBackgrounds(CharacterSelectBgContainerRef(screen));

    internal static void Refresh(NMultiplayerLoadGameScreen screen) =>
        NShinGetterCharacterSelectBackground.RefreshMarkedBackgrounds(MultiplayerLoadBgContainerRef(screen));
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectTransitionPath")]
internal static class ShinGetterTransitionPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
        {
            __result = "res://materials/transitions/shin_getter_transition_mat.tres";
        }
    }
}
