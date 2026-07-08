using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardConsoleCmd), nameof(CardConsoleCmd.Process))]
internal static class CardConsoleAliasPatch
{
    private const string ClassPrefix = "SGC_";
    private const string ModelPrefix = "s_g_c_";

    private static readonly IReadOnlyDictionary<string, string> SpecialAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["s_g_c_saint_dragon_roar"] = "S_G_C_HOLY_DRAGON_ROAR",
        };

    private static void Prefix(string[] args)
    {
        if (args.Length == 0)
        {
            return;
        }

        if (SpecialAliases.TryGetValue(args[0], out string modelEntry))
        {
            args[0] = modelEntry;
            return;
        }

        if (args[0].StartsWith(ModelPrefix, StringComparison.OrdinalIgnoreCase))
        {
            args[0] = NormalizeSnakeAlias(args[0]);
            return;
        }

        if (args[0].StartsWith(ClassPrefix, StringComparison.OrdinalIgnoreCase))
        {
            args[0] = ToModelEntry(args[0][ClassPrefix.Length..]);
        }
    }

    private static string NormalizeSnakeAlias(string alias) => alias.ToUpperInvariant();

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
