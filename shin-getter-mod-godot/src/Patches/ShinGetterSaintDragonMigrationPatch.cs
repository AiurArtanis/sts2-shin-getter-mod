using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Patches;

internal static class ShinGetterSaintDragonMigration
{
    // Exact legacy save inputs only. Do not register a second card model.
    internal static string CardEntry(string category, string entry) =>
        category == "CARD" && entry == "S_G_C_HOLY_DRAGON_ROAR"
            ? "S_G_C_SAINT_DRAGON_ROAR" : entry;

    internal static string LocKey(string table, string key) => (table, key) switch
    {
        ("cards", "S_G_C_HOLY_DRAGON_ROAR.title") => "S_G_C_SAINT_DRAGON_ROAR.title",
        ("cards", "S_G_C_HOLY_DRAGON_ROAR.description") => "S_G_C_SAINT_DRAGON_ROAR.description",
        ("events", "S_G_E_GETTER_MANDALA.pages.INITIAL.options.HOLY_DRAGON.title") =>
            "S_G_E_GETTER_MANDALA.pages.INITIAL.options.SAINT_DRAGON.title",
        ("events", "S_G_E_GETTER_MANDALA.pages.INITIAL.options.HOLY_DRAGON.description") =>
            "S_G_E_GETTER_MANDALA.pages.INITIAL.options.SAINT_DRAGON.description",
        ("events", "S_G_E_GETTER_MANDALA.pages.HOLY_DRAGON.description") =>
            "S_G_E_GETTER_MANDALA.pages.SAINT_DRAGON.description",
        _ => key,
    };
}

// Both the run-save string converter and generated object reader construct ModelId.
[HarmonyPatch(typeof(ModelId), MethodType.Constructor, new[] { typeof(string), typeof(string) })]
internal static class ShinGetterSaintDragonModelIdPatch
{
    private static void Prefix(string category, ref string entry) =>
        entry = ShinGetterSaintDragonMigration.CardEntry(category, entry);
}

[HarmonyPatch(typeof(LocString), MethodType.Constructor, new[] { typeof(string), typeof(string) })]
internal static class ShinGetterSaintDragonLocStringPatch
{
    private static void Prefix(string locTable, ref string locEntryKey) =>
        locEntryKey = ShinGetterSaintDragonMigration.LocKey(locTable, locEntryKey);
}

// GetIfExists checks the table before constructing the LocString.
[HarmonyPatch(typeof(LocString), nameof(LocString.Exists), new[] { typeof(string), typeof(string) })]
internal static class ShinGetterSaintDragonLocExistsPatch
{
    private static void Prefix(string table, ref string key) =>
        key = ShinGetterSaintDragonMigration.LocKey(table, key);
}
