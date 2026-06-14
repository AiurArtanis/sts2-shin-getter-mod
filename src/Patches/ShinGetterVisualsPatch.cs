using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

/// <summary>
/// Postfix patches: 将 ShinGetter 尚未制作的选角视觉资源重定向到 Ironclad 占位资源。
/// </summary>
[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectBg")]
internal static class ShinGetterBgPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
        {
            __result = "res://scenes/screens/char_select/char_select_bg_ironclad.tscn";
        }
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectTransitionPath")]
internal static class ShinGetterTransitionPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
        {
            __result = "res://materials/transitions/ironclad_transition_mat.tres";
        }
    }
}
