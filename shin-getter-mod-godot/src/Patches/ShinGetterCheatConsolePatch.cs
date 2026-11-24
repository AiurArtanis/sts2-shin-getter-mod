#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using ShinGetterMod.Diagnostics;

namespace ShinGetterMod.Patches;

internal static class ShinGetterCheatPhrases
{
    internal static readonly string[] All =
    {
        "现在可不是计较物理法则的时候",
        "物理法則もあったもんじゃねえな",
        "Now is not the time to worry about the laws of physics",
    };

    internal static bool IsComplete(string input) => All.Any(phrase =>
        phrase.Equals(input.Trim().TrimEnd('.', '。', '!', '！'), StringComparison.OrdinalIgnoreCase));
}

[HarmonyPatch(typeof(DevConsole), MethodType.Constructor, new Type[] { typeof(bool) })]
internal static class ShinGetterCheatConsoleRegistrationPatch
{
    private static readonly AccessTools.FieldRef<DevConsole, Dictionary<string, AbstractConsoleCmd>> Commands =
        AccessTools.FieldRefAccess<DevConsole, Dictionary<string, AbstractConsoleCmd>>("_commands");

    private static void Postfix(DevConsole __instance) => EnsureRegistered(__instance);

    internal static void EnsureRegistered(DevConsole console) =>
        Commands(console).TryAdd(ShinGetterCheatConsoleCmd.CommandName, new ShinGetterCheatConsoleCmd());
}

[HarmonyPatch(typeof(DevConsole), nameof(DevConsole.ProcessCommand), new Type[] { typeof(string) })]
internal static class ShinGetterCheatPhraseExecutionPatch
{
    private static void Prefix(DevConsole __instance, ref string inputValue)
    {
        // A console may already exist when mods initialize. Registration must precede
        // the public method's network lookup, not just its eventual local dispatch.
        ShinGetterCheatConsoleRegistrationPatch.EnsureRegistered(__instance);
        // Normalize BEFORE the native networked-command lookup, including spaced English.
        // The canonical command is then enqueued/awaited by the normal ConsoleCmdGameAction.
        if (ShinGetterCheatPhrases.IsComplete(inputValue))
            inputValue = ShinGetterCheatConsoleCmd.CommandName;
    }
}

[HarmonyPatch(typeof(DevConsole), "ProcessCommand", new Type[] { typeof(Player), typeof(string), typeof(string[]) })]
internal static class ShinGetterCheatNetworkDispatchPatch
{
    private static void Prefix(DevConsole __instance, string cmdName)
    {
        // Remote peers/replays may never have submitted a local console command.
        if (cmdName.Equals(ShinGetterCheatConsoleCmd.CommandName, StringComparison.OrdinalIgnoreCase))
            ShinGetterCheatConsoleRegistrationPatch.EnsureRegistered(__instance);
    }
}

[HarmonyPatch(typeof(DevConsole), nameof(DevConsole.GetCompletionResults))]
internal static class ShinGetterCheatPhraseCompletionPatch
{
    private static bool Prefix(string inputBuffer, ref CompletionResult __result)
    {
        string input = inputBuffer.TrimStart();
        if (input.Length < 3)
            return true;
        var candidates = ShinGetterCheatPhrases.All.Where(phrase =>
            phrase.StartsWith(input, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0)
            return true;
        __result = new CompletionResult
        {
            Candidates = candidates,
            CommonPrefix = candidates[0] + " ",
            Type = CompletionType.Command,
            CommandPrefix = "",
            ArgumentIndex = -1,
            ArgumentContext = "",
        };
        return false;
    }
}
