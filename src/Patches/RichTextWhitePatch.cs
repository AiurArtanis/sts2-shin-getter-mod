using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using ShinGetterMod.RichTextTags;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(MegaRichTextLabel), "InstallEffectsIfNeeded")]
internal static class RichTextWhitePatch
{
    private static readonly RichTextWhite WhiteEffect = new();
    private static readonly RichTextYellow YellowEffect = new();
    private static readonly RichTextGetterRay GetterRayEffect = new();

    private static void Postfix(MegaRichTextLabel __instance)
    {
        if (!__instance.BbcodeEnabled)
        {
            return;
        }

        if (!__instance.CustomEffects.Contains(WhiteEffect))
        {
            __instance.CustomEffects.Add(WhiteEffect);
        }
        if (!__instance.CustomEffects.Contains(YellowEffect))
        {
            __instance.CustomEffects.Add(YellowEffect);
        }
        if (!__instance.CustomEffects.Contains(GetterRayEffect))
        {
            __instance.CustomEffects.Add(GetterRayEffect);
        }
    }
}
