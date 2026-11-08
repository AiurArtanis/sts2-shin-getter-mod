#nullable enable
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Events;

internal static class ShinGetterEventReturnProse
{
    private sealed record Scope(string NativeKey, string ProseKey);
    private sealed record Composition(LocString Original, LocString Prose);
    private static readonly ConditionalWeakTable<EventModel, Scope> Scopes = new();
    private static readonly ConditionalWeakTable<LocString, Composition> Compositions = new();

    internal static void Begin(EventModel model, string nativeKey, string proseKey)
    {
        Scopes.Remove(model);
        Scopes.Add(model, new(nativeKey, proseKey));
    }

    internal static void Clear(EventModel model) => Scopes.Remove(model);

    internal static LocString Compose(EventModel model, LocString description)
    {
        if (!Scopes.TryGetValue(model, out Scope? scope)) return description;
        // Retain the scope for idempotent refresh, not every future occurrence of this page.
        if (description.LocTable != "events" || description.LocEntryKey != scope.NativeKey)
        {
            Clear(model);
            return description;
        }
        if (Compositions.TryGetValue(description, out _)) return description;
        LocString copy = new(description.LocTable, description.LocEntryKey);
        copy.AddVariablesFrom(description); // Do not mutate the native/shared LocString.
        Compositions.Add(copy, new(description, new LocString("events", scope.ProseKey)));
        return copy;
    }

    internal static bool TryRawText(LocString description, out string text)
    {
        text = "";
        if (!Compositions.TryGetValue(description, out Composition? composition)) return false;
        // Resolve both keys in the current locale on every render. SmartFormat still applies
        // the original dynamic variables, so no dish/parameter is frozen or omitted.
        text = composition.Original.GetRawText() + "\n\n" + composition.Prose.GetRawText();
        return true;
    }
}
