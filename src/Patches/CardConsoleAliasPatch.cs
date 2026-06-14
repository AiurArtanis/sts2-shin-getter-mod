using System;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardConsoleCmd), nameof(CardConsoleCmd.Process))]
internal static class CardConsoleAliasPatch
{
    private const string ClassPrefix = "SGC_";

    private static void Prefix(string[] args)
    {
        if (args.Length == 0 || !args[0].StartsWith(ClassPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        args[0] = ToModelEntry(args[0][ClassPrefix.Length..]);
    }

    private static string ToModelEntry(string classNameSuffix)
    {
        StringBuilder entry = new("S_G_C_");
        for (int i = 0; i < classNameSuffix.Length; i++)
        {
            char current = classNameSuffix[i];
            if (current == '_')
            {
                if (entry[^1] != '_')
                {
                    entry.Append('_');
                }
                continue;
            }

            if (i > 0 && char.IsUpper(current) && char.IsLower(classNameSuffix[i - 1]))
            {
                entry.Append('_');
            }
            entry.Append(char.ToUpperInvariant(current));
        }
        return entry.ToString();
    }
}
