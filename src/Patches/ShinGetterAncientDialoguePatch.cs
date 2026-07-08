#nullable enable
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Localization;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(AncientDialogueSet), nameof(AncientDialogueSet.PopulateLocKeys))]
internal static class ShinGetterAncientDialoguePatch
{
    private const string ShinGetterKey = "SHIN_GETTER";
    private const int MaxDialoguesToProbe = 8;
    private const int MaxLinesToProbe = 8;

    private static void Prefix(AncientDialogueSet __instance, string ancientEntry)
    {
        if (__instance.CharacterDialogues.ContainsKey(ShinGetterKey))
            return;

        List<AncientDialogue> dialogues = new();
        for (int dialogueIndex = 0; dialogueIndex < MaxDialoguesToProbe; dialogueIndex++)
        {
            int lineCount = CountLines(ancientEntry, dialogueIndex);
            if (lineCount <= 0)
                continue;

            AncientDialogue dialogue = new(Enumerable.Repeat("", lineCount).ToArray())
            {
                VisitIndex = dialogueIndex,
                EndAttackers = ArchitectAttackers.Player
            };
            dialogues.Add(dialogue);
        }

        if (dialogues.Count > 0)
            __instance.CharacterDialogues[ShinGetterKey] = dialogues;
    }

    private static int CountLines(string ancientEntry, int dialogueIndex)
    {
        int count = 0;
        for (int lineIndex = 0; lineIndex < MaxLinesToProbe; lineIndex++)
        {
            string baseKey = $"{ancientEntry}.talk.{ShinGetterKey}.{dialogueIndex}-{lineIndex}";
            if (!ExistsLine(baseKey))
                break;

            count++;
        }

        return count;
    }

    private static bool ExistsLine(string baseKey) =>
        LocString.Exists("ancients", baseKey + ".ancient")
        || LocString.Exists("ancients", baseKey + ".char")
        || LocString.Exists("ancients", baseKey + "r.ancient")
        || LocString.Exists("ancients", baseKey + "r.char");
}
