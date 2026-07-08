using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(EventConsoleCmd), nameof(EventConsoleCmd.Process))]
internal static class EventConsoleAliasPatch
{
    private const string EventClassPrefix = "SGE_";
    private const string EventModelPrefix = "s_g_e_";

    private static readonly IReadOnlyDictionary<string, string> SpecialAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["s_g_c_getter_mandala"] = "S_G_E_GETTER_MANDALA",
            ["s_g_e_getter_mandala"] = "S_G_E_GETTER_MANDALA",
        };

    private static void Prefix(string[] args)
    {
        if (args.Length == 0)
            return;

        if (SpecialAliases.TryGetValue(args[0], out string modelEntry))
        {
            args[0] = modelEntry;
            return;
        }

        if (args[0].StartsWith(EventModelPrefix, StringComparison.OrdinalIgnoreCase))
        {
            args[0] = args[0].ToUpperInvariant();
            return;
        }

        if (args[0].StartsWith(EventClassPrefix, StringComparison.OrdinalIgnoreCase))
        {
            args[0] = ToModelEntry(args[0][EventClassPrefix.Length..]);
        }
    }

    private static string ToModelEntry(string classNameSuffix)
    {
        StringBuilder entry = new("S_G_E_");
        for (int i = 0; i < classNameSuffix.Length; i++)
        {
            char current = classNameSuffix[i];
            if (current == '_')
            {
                if (entry[^1] != '_')
                    entry.Append('_');
                continue;
            }

            if (i > 0 && char.IsUpper(current) && char.IsLower(classNameSuffix[i - 1]))
                entry.Append('_');

            entry.Append(char.ToUpperInvariant(current));
        }

        return entry.ToString();
    }
}
