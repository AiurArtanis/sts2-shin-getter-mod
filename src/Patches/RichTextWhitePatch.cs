using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using ShinGetterMod.RichTextTags;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(MegaRichTextLabel), "InstallEffectsIfNeeded")]
internal static class RichTextWhitePatch
{
    private static readonly RichTextWhite Effect = new();

    private static void Postfix(MegaRichTextLabel __instance)
    {
        if (__instance.BbcodeEnabled && !__instance.CustomEffects.Contains(Effect))
        {
            __instance.CustomEffects.Add(Effect);
        }
    }
}
