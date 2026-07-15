using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class ShinGetterCardPlayVoicePatch
{
    private static void Prefix(CardModel __instance)
    {
        ShinGetterVoiceService.TryPlayCardVoiceAtCardPlayStart(__instance);
    }
}
