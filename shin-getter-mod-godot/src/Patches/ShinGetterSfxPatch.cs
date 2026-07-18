#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CharacterModel), "get_AttackSfx")]
internal static class ShinGetterAttackSfxPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
            __result = "event:/sfx/characters/ironclad/ironclad_attack";
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_CastSfx")]
internal static class ShinGetterCastSfxPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
            __result = "event:/sfx/characters/ironclad/ironclad_cast";
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_DeathSfx")]
internal static class ShinGetterDeathSfxPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is ShinGetter)
            __result = "event:/sfx/characters/ironclad/ironclad_die";
    }
}

[HarmonyPatch(typeof(MonsterModel), "get_DeathSfx")]
internal static class SnappingJaxfruitDeathSfxPatch
{
    private static void Postfix(MonsterModel __instance, ref string __result)
    {
        if (__instance is SnappingJaxfruit)
            __result = "event:/sfx/enemy/enemy_attacks/vine_shambler/vine_shambler_die";
    }
}
