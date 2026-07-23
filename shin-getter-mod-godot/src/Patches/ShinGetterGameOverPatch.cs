#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NGameOverScreen), nameof(NGameOverScreen._Ready))]
internal static class ShinGetterGameOverPatch
{
    private static readonly AccessTools.FieldRef<NGameOverScreen, Player> LocalPlayerRef =
        AccessTools.FieldRefAccess<NGameOverScreen, Player>("_localPlayer");

    private static readonly AccessTools.FieldRef<NGameOverScreen, RunHistory> HistoryRef =
        AccessTools.FieldRefAccess<NGameOverScreen, RunHistory>("_history");

    private static readonly AccessTools.FieldRef<NGameOverScreen, string> EncounterQuoteRef =
        AccessTools.FieldRefAccess<NGameOverScreen, string>("_encounterQuote");

    private static readonly AccessTools.FieldRef<NGameOverScreen, MegaRichTextLabel> DeathQuoteRef =
        AccessTools.FieldRefAccess<NGameOverScreen, MegaRichTextLabel>("_deathQuote");

    [HarmonyPostfix]
    private static void Postfix(NGameOverScreen __instance)
    {
        Player player = LocalPlayerRef(__instance);
        RunHistory history = HistoryRef(__instance);
        if (player.Character is not ShinGetter || history.Win)
            return;

        string deathQuote = player.Character.EventDeathPreventionLine.GetFormattedText();
        EncounterQuoteRef(__instance) = deathQuote;
        DeathQuoteRef(__instance).Text = deathQuote;
    }
}
