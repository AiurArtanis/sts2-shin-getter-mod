#nullable enable
using System;
using System.Collections.Generic;

namespace ShinGetterMod.Services;

/// <summary>Copy-on-repair runtime bindings; never edits the saved mapping.</summary>
internal static class ShinGetterControllerMappingPolicy
{
    internal static Dictionary<T, T> Normalize<T>(Dictionary<T, T> source,
        Dictionary<T, T> defaults, Func<T, bool> hasAction, out int repaired) where T : notnull
    {
        Dictionary<T, T>? result = null;
        repaired = 0;
        foreach (var pair in source)
        {
            bool keyValid = hasAction(pair.Key);
            if (keyValid && hasAction(pair.Value)) continue;
            result ??= new Dictionary<T, T>(source);
            if (keyValid && defaults.TryGetValue(pair.Key, out var fallback) && hasAction(fallback))
                result[pair.Key] = fallback;
            else result.Remove(pair.Key);
            repaired++;
        }
        return result ?? source;
    }
}
