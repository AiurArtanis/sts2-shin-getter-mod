#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.SetAlignmentForCardHolder))]
internal static class ShinGetterCardSelectionHoverPatch
{
    private static void Postfix(NHoverTipSet __instance, NCardHolder holder)
    {
        if (holder.CardModel is not ShinGetterCardBase || !IsCombatPileSelection(holder))
            return;

        // Text tip backgrounds default to Stop even though their root ignores the mouse.
        if (__instance.GetNodeOrNull<Control>("textHoverTipContainer") is { } textTips)
            MakeMouseTransparent(textTips);
    }

    private static bool IsCombatPileSelection(Node holder)
    {
        for (Node? node = holder; node != null; node = node.GetParent())
        {
            if (node is NCombatPileCardSelectScreen)
                return true;
        }
        return false;
    }

    private static void MakeMouseTransparent(Node node)
    {
        if (node is Control control)
            control.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (Node child in node.GetChildren())
            MakeMouseTransparent(child);
    }
}
