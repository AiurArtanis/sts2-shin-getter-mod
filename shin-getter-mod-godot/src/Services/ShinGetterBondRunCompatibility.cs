#nullable enable
using System.Collections.Generic;
using System;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ShinGetterMod.Services;

internal static class ShinGetterBondRunCompatibility
{
    private const string StatsType = "St2bs.St2bsCode.Modifiers.GirlsNightmareEndStatsModifier";
    private const string StatsEntry = "ST2BS-GIRLS_NIGHTMARE_END_STATS_MODIFIER";

    // BS2 2.3 adds this saved counter to every new run, including Standard.
    // Never treat other modifiers (or a mixture) as a standard solo run.
    internal static bool IsStandardModifiers(IReadOnlyList<ModifierModel> modifiers)
    {
        if (modifiers.Count == 0) return true;
        if (modifiers.Count != 1 || modifiers[0] is not { } modifier) return false;
        try
        {
            return IsStatsType(modifier) && ResolveStats(modifier.Id)?.GetType() == modifier.GetType();
        }
        catch (Exception) { return false; }
    }

    internal static bool IsStandardHistoryModifiers(IReadOnlyList<SerializableModifier> modifiers)
    {
        if (modifiers.Count == 0) return true;
        if (modifiers.Count != 1 || modifiers[0]?.Id is not { } id) return false;
        try { return ResolveStats(id) != null; }
        catch (Exception) { return false; }
    }

    private static bool IsStatsType(ModifierModel modifier)
    {
        Type type = modifier.GetType();
        return type.IsSealed && type.FullName == StatsType && type.Assembly.GetName().Name == "St2bs";
    }

    private static ModifierModel? ResolveStats(ModelId id)
    {
        if (id.Category != "MODIFIER" || id.Entry != StatsEntry) return null;
        // Look up only already-registered models: no optional assembly loading,
        // and no cached miss before ModelDb finishes new-run/cold-load setup.
        var registered = ModelDb.GetByIdOrNull<ModifierModel>(id);
        return registered != null && registered.Id == id && IsStatsType(registered) ? registered : null;
    }
}
