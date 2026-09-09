#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(ModelDb), nameof(ModelDb.InitIds))]
internal static class ShinGetterSavedPropertiesPatch
{
    private static readonly Action<int> SetNetIdBitSize = AccessTools.MethodDelegate<Action<int>>(
        AccessTools.PropertySetter(typeof(SavedPropertiesTypeCache), nameof(SavedPropertiesTypeCache.NetIdBitSize)));

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix()
    {
        // 109 discovers mod models but only seeds saved properties for built-in types.
        // Run after BaseLib's InitIds prefix; preserve any IDs it already assigned.
        foreach (Type type in typeof(Entry).Assembly.GetTypes()
                     .Where(type => !type.IsAbstract && !type.ContainsGenericParameters
                         && typeof(AbstractModel).IsAssignableFrom(type))
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            SavedPropertiesTypeCache.InjectTypeIntoCache(type);
        }

        // InjectTypeIntoCache appends names without updating the packet field width.
        int maxId = AccessTools.StaticFieldRefAccess<List<string>>(
            typeof(SavedPropertiesTypeCache), "_netIdToPropertyNameMap").Count - 1;
        int bits = 0;
        while (maxId > 0)
        {
            bits++;
            maxId >>= 1;
        }
        SetNetIdBitSize(bits);
    }
}
