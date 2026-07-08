#nullable enable
namespace ShinGetterMod.Diagnostics.CardExport;

public readonly struct ShinGetterCardPngExportRequest
{
    public string CharacterFilter { get; init; }
    public string OutputDirectory { get; init; }
    public float Scale { get; init; }
    public bool IncludeUpgradedVariants { get; init; }
    public bool IncludeCardsHiddenFromLibrary { get; init; }
    public string? IdFilterPattern { get; init; }
    public int MaxBaseCards { get; init; }

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
        };
    }
}
