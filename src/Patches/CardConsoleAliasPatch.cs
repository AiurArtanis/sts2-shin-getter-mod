using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardConsoleCmd), nameof(CardConsoleCmd.Process))]
internal static class CardConsoleAliasPatch
{
    private const string ClassPrefix = "SGC_";
    private const string ModelPrefix = "s_g_c_";

    internal static readonly IReadOnlyDictionary<string, string> SpecialAliases =
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

[HarmonyPatch(typeof(CardConsoleCmd), nameof(CardConsoleCmd.GetArgumentCompletions))]
internal static class CardConsoleAliasCompletionPatch
{
    private static void Postfix(string[] args, CompletionResult __result)
    {
        if (args.Length > 1)
            return;

        string partial = args.FirstOrDefault() ?? string.Empty;
        foreach (string alias in CardConsoleAliasPatch.SpecialAliases.Keys
                     .Where(alias => alias.StartsWith(partial, StringComparison.OrdinalIgnoreCase)))
        {
            if (!__result.Candidates.Contains(alias, StringComparer.OrdinalIgnoreCase))
                __result.Candidates.Add(alias);
        }

        __result.CommonPrefix = CalculateCommonCompletion(__result.Candidates, __result.CommandPrefix);
    }

    private static string CalculateCommonCompletion(IReadOnlyList<string> candidates, string prefix)
    {
        if (candidates.Count == 0)
            return string.Empty;
        if (candidates.Count == 1)
            return prefix + candidates[0] + " ";

        int commonLength = candidates.Min(candidate => candidate.Length);
        string first = candidates[0];
        for (int index = 0; index < commonLength; index++)
        {
            if (candidates.Any(candidate =>
                    char.ToLowerInvariant(candidate[index]) != char.ToLowerInvariant(first[index])))
            {
                commonLength = index;
                break;
            }
        }

        return commonLength > 0 ? prefix + first[..commonLength] : string.Empty;
    }
}
