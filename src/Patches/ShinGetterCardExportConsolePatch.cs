#nullable enable
using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Players;
using ShinGetterMod.Diagnostics.CardExport;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(DevConsole), "ProcessCommand", new Type[] { typeof(Player), typeof(string), typeof(string[]) })]
internal static class ShinGetterCardExportConsolePatch
{
    private const string ExportCommandName = "export_cards";

    private static bool Prefix(Player? player, string cmdName, string[] args, ref CmdResult __result)
    {
        if (!cmdName.Equals(ExportCommandName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        __result = new ShinGetterCardExportConsoleCmd().Process(player, args);
        return false;
    }
}
