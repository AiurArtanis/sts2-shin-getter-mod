#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using ShinGetterMod.Nodes.Screens.Shops;

namespace ShinGetterMod.Patches;

[HarmonyPatch]
internal static class ShinGetterMerchantReactionPatch
{
    [HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> OpenTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplacePresentation(instructions, opening: true);
    }

    [HarmonyPatch(typeof(NMerchantInventory), "OnPurchaseCompleted")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> PurchaseTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplacePresentation(instructions, opening: false);
    }

    [HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
    [HarmonyPrefix]
    private static void BeforePurchase(MerchantEntry __instance)
    {
        ShinGetterMerchantReactions.ClearPurchase(__instance);
    }

    [HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.InvokePurchaseCompleted))]
    [HarmonyFinalizer]
    private static Exception? AfterPurchaseFeedback(MerchantEntry entry, Exception? __exception)
    {
        // Also clears markers with no local UI subscriber or an interrupted feedback callback.
        ShinGetterMerchantReactions.ClearPurchase(entry);
        return __exception;
    }

    private static IEnumerable<CodeInstruction> ReplacePresentation(IEnumerable<CodeInstruction> instructions, bool opening)
    {
        List<CodeInstruction> original = instructions.ToList();
        MethodInfo? sound = AccessTools.Method(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) });
        MethodInfo? dialogue = AccessTools.Method(typeof(NMerchantDialogue), opening
            ? nameof(NMerchantDialogue.ShowOnInventoryOpen) : nameof(NMerchantDialogue.ShowForPurchaseAttempt));
        MethodInfo? soundReplacement = AccessTools.Method(typeof(ShinGetterMerchantReactions), opening
            ? nameof(ShinGetterMerchantReactions.PlayWelcome) : nameof(ShinGetterMerchantReactions.PlayPurchase));
        MethodInfo? dialogueReplacement = AccessTools.Method(typeof(ShinGetterMerchantReactions), opening
            ? nameof(ShinGetterMerchantReactions.ShowWelcome) : nameof(ShinGetterMerchantReactions.ShowPurchase));
        string expectedSound = opening ? "event:/sfx/npcs/merchant/merchant_welcome"
            : "event:/sfx/npcs/merchant/merchant_thank_yous";
        if (sound == null || dialogue == null || soundReplacement == null || dialogueReplacement == null)
            return Skip(original, opening);

        int[] soundCalls = Enumerable.Range(2, Math.Max(0, original.Count - 2))
            .Where(i => original[i].Calls(sound) && original[i - 2].opcode == OpCodes.Ldstr &&
                Equals(original[i - 2].operand, expectedSound)).ToArray();
        int[] dialogueCalls = Enumerable.Range(0, original.Count).Where(i => original[i].Calls(dialogue)).ToArray();
        if (soundCalls.Length != 1 || dialogueCalls.Length != 1)
            return Skip(original, opening);

        // Replace exactly the sound and dialogue calls. Navigation, focus, animation and all
        // purchase callbacks retain the original instructions and order; no method is skipped.
        var result = new List<CodeInstruction>(original.Count + 4);
        for (int i = 0; i < original.Count; i++)
        {
            bool isSound = i == soundCalls[0];
            if (!isSound && i != dialogueCalls[0])
            {
                result.Add(original[i]);
                continue;
            }
            var self = new CodeInstruction(OpCodes.Ldarg_0);
            self.labels.AddRange(original[i].labels);
            self.blocks.AddRange(original[i].blocks);
            result.Add(self);
            if (!opening)
                result.Add(new CodeInstruction(OpCodes.Ldarg_2));
            result.Add(new CodeInstruction(OpCodes.Call, isSound ? soundReplacement : dialogueReplacement));
        }
        return result;
    }

    private static IEnumerable<CodeInstruction> Skip(List<CodeInstruction> original, bool opening)
    {
        ShinGetterMerchantReactions.WarnOnce($"Native {(opening ? "open" : "purchase")} call layout changed; presentation patch skipped.");
        return original;
    }
}
