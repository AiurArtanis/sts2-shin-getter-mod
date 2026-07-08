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

    private static bool Prefix(Player? player, string cmdName, string[] args, ref CmdResult __result)
    {
        if (!cmdName.Equals(AddAllCardsCommandName, StringComparison.OrdinalIgnoreCase))
            return true;

        __result = new ShinGetterAddAllCardsConsoleCmd().Process(player, args);
        return false;
    }
}
