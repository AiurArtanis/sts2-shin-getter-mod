#nullable enable
using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Players;
using ShinGetterMod.Diagnostics;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(DevConsole), "ProcessCommand", new Type[] { typeof(Player), typeof(string), typeof(string[]) })]
internal static class ShinGetterConsoleCommandPatch
{
    private const string AddAllCardsCommandName = "shin_getter_add_cards";
    private const string ChunibyoCommandName = "chunibyo";
    private const string ShinGetterSoundCommandName = "sgs";

    private static bool Prefix(Player? player, string cmdName, string[] args, ref CmdResult __result)
    {
        if (cmdName.Equals(AddAllCardsCommandName, StringComparison.OrdinalIgnoreCase))
        {
            __result = new ShinGetterAddAllCardsConsoleCmd().Process(player, args);
            return false;
        }

        if (cmdName.Equals(ChunibyoCommandName, StringComparison.OrdinalIgnoreCase))
        {
            __result = new ShinGetterChunibyoConsoleCmd().Process(player, args);
            return false;
        }

        if (cmdName.Equals(ShinGetterSoundCommandName, StringComparison.OrdinalIgnoreCase))
        {
            __result = new ShinGetterSoundConsoleCmd().Process(player, args);
            return false;
        }

        return true;
    }
}
