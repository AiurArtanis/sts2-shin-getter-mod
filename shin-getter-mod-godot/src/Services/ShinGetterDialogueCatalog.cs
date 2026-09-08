#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MegaCrit.Sts2.Core.Localization;

namespace ShinGetterMod.Services;

internal sealed class ShinGetterDialogue
{
    public ShinGetterDialogue() { }
    public string Id { get; set; } = "";
    public string Npc { get; set; } = "";
    public string Driver { get; set; } = "";
    public int Stage { get; set; }
    public string Version { get; set; } = "";
    public string Option { get; set; } = "";
    public string Scene { get; set; } = "";
    public string[] Lines { get; set; } = Array.Empty<string>();
}

internal static class ShinGetterDialogueCatalog
{
    internal static readonly string[] Drivers = { "RYOMA", "HAYATO", "BENKEI" };
    internal static readonly string[] BondNpcs = { "OROBAS", "TANX", "VAKUU", "DARV", "PAEL", "TEZCATARA", "NONUPEIPE" };
    private static readonly Lazy<Dictionary<string, ShinGetterDialogue>> Source = new(() => Read("zhs"));
    private static readonly Dictionary<string, Dictionary<string, ShinGetterDialogue>> Locales = new();
    internal static IEnumerable<ShinGetterDialogue> All => Source.Value.Values;
    internal static bool ContainsNpc(string npc) => BondNpcs.Contains(npc) || npc is "NEOW" or "THE_ARCHITECT";
    internal static ShinGetterDialogue Get(string id) => Source.Value[id];

    internal static ShinGetterDialogue Localize(string id) => Localize(id, LocManager.Instance.Language);

    internal static ShinGetterDialogue Localize(string id, string language)
    {
        if (language == "zhs") return Get(id);
        language = language == "jpn" ? "jpn" : "eng";
        if (!Locales.TryGetValue(language, out var locale))
            Locales[language] = locale = Read(language);
        return locale.TryGetValue(id, out var text) ? text : Get(id);
    }

    private static Dictionary<string, ShinGetterDialogue> Read(string language)
    {
        var assembly = typeof(ShinGetterDialogueCatalog).Assembly;
        string resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".data.dialogues.{language}.json", StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        var entries = JsonSerializer.Deserialize<ShinGetterDialogue[]>(stream)
            ?? throw new InvalidDataException("Empty dialogue catalogue");
        if (entries.Any(d => string.IsNullOrEmpty(d.Id) || d.Lines == null || d.Lines.Length == 0
            || d.Lines.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException("Incomplete dialogue catalogue: " + language);
        return entries.ToDictionary(d => d.Id, StringComparer.Ordinal);
    }

    internal static string Ui(string zhs, string eng, string jpn) => LocManager.Instance.Language switch
    {
        "zhs" => zhs,
        "jpn" => jpn,
        _ => eng,
    };
}
