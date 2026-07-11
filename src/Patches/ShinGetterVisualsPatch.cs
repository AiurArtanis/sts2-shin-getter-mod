using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Characters;

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
