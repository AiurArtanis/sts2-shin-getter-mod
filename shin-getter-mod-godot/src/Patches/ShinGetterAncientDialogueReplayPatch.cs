using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(AncientDialogueSet), nameof(AncientDialogueSet.GetValidDialogues))]
internal static class ShinGetterAncientDialogueReplayPatch
{
    private static void Prefix(ModelId characterId, ref int charVisits)
    {
        if (characterId.Entry == ShinGetterAncientDialoguePatch.ShinGetterKey)
            charVisits = 0;
    }
}
