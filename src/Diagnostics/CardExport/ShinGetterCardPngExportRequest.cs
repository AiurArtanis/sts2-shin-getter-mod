#nullable enable
namespace ShinGetterMod.Diagnostics.CardExport;

public enum ShinGetterCardExportNameFormat
{
    Default,
    Zhs,
    Jpn,
    Eng,
}

public readonly struct ShinGetterCardPngExportRequest
{
    public string CharacterFilter { get; init; }
    public string OutputDirectory { get; init; }
    public float Scale { get; init; }
    public bool IncludeUpgradedVariants { get; init; }
    public bool IncludeCardsHiddenFromLibrary { get; init; }
    public string? IdFilterPattern { get; init; }
    public int MaxBaseCards { get; init; }
    public ShinGetterCardExportNameFormat NameFormat { get; init; }

    public static ShinGetterCardPngExportRequest CreateDefault(string characterFilter, string outputDirectory)
    {
        return new()
        {
            CharacterFilter = characterFilter,
            OutputDirectory = outputDirectory,
            Scale = 1f,
            IncludeUpgradedVariants = true,
            IncludeCardsHiddenFromLibrary = false,
            MaxBaseCards = 0,
            NameFormat = ShinGetterCardExportNameFormat.Default,
        };
    }
}
