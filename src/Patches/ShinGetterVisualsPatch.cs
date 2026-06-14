using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

/// <summary>
/// Postfix patches: 将 ShinGetter 尚未制作的视觉与音效资源重定向到 Ironclad 占位资源。
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

[HarmonyPatch(typeof(CharacterModel), "get_AttackSfx")]
internal static class ShinGetterAttackSfxPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
        {
            __result = "event:/sfx/characters/ironclad/ironclad_attack";
        }
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_CastSfx")]
internal static class ShinGetterCastSfxPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
        {
            __result = "event:/sfx/characters/ironclad/ironclad_cast";
        }
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_DeathSfx")]
internal static class ShinGetterDeathSfxPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
        {
            __result = "event:/sfx/characters/ironclad/ironclad_die";
        }
    }
}
