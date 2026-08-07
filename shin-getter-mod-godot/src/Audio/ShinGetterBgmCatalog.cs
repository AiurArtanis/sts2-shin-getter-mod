#nullable enable
using System;
using System.Collections.Generic;

namespace ShinGetterMod.Audio;

internal enum ShinGetterBgmCategory
{
    Execution,
    NormalCombat,
    EventCombat,
    EliteCombat,
    BossCombat,
}

internal sealed record ShinGetterBgmTrack(
    string Id,
    string ResourcePath,
    string LocalizationKey,
    string FallbackTitle);

internal static class ShinGetterBgmCatalog
{
    internal const string DefaultTrackId = "default";
    internal const string DefaultExecutionMusicPath =
        "res://audio/music/shin_getter/execution_theme.mp3";

    private const string AlbumRoot = "res://audio/music/shin_getter/album";
    private const string EncounterRoot = "res://audio/music/shin_getter/encounters";

    internal static IReadOnlyList<ShinGetterBgmTrack> Tracks { get; } =
        new ShinGetterBgmTrack[]
        {
            Track(DefaultTrackId, string.Empty, "DEFAULT", "(default)"),
            Track("relief", $"{AlbumRoot}/relief.mp3", "RELIEF", "Relief"),
            Track("grief", $"{AlbumRoot}/grief.mp3", "GRIEF", "Grief"),
            Track("morning_on_the_tundra", $"{AlbumRoot}/morning_on_the_tundra.mp3", "MORNING_ON_THE_TUNDRA", "Morning on the Tundra"),
            Track("brutality", $"{AlbumRoot}/brutality.mp3", "BRUTALITY", "Brutality"),
            Track("rebel_army", $"{EncounterRoot}/boss_overgrowth.mp3", "REBEL_ARMY", "Rebel Army"),
            Track("past", $"{AlbumRoot}/past.mp3", "PAST", "Past"),
            Track("memory", $"{AlbumRoot}/memory.mp3", "MEMORY", "Memory"),
            Track("interference", $"{AlbumRoot}/interference.mp3", "INTERFERENCE", "Interference"),
            Track("tension", $"{EncounterRoot}/elite_glory.mp3", "TENSION", "Tension"),
            Track("cold_bloodedness", $"{AlbumRoot}/cold_bloodedness.mp3", "COLD_BLOODEDNESS", "Cold-Bloodedness"),
            Track("mystery", $"{EncounterRoot}/boss_underdocks.mp3", "MYSTERY", "Mystery"),
            Track("momentum", $"{EncounterRoot}/elite_overgrowth.mp3", "MOMENTUM", "Momentum"),
            Track("majesty", $"{EncounterRoot}/boss_hive.mp3", "MAJESTY", "Majesty"),
            Track("unknown", $"{EncounterRoot}/elite_underdocks.mp3", "UNKNOWN", "Unknown"),
            Track("onslaught", $"{EncounterRoot}/elite_hive.mp3", "ONSLAUGHT", "Onslaught"),
            Track("bond_of_blood", $"{AlbumRoot}/bond_of_blood.mp3", "BOND_OF_BLOOD", "Bond of Blood"),
            Track("resolve", $"{AlbumRoot}/resolve.mp3", "RESOLVE", "Resolve"),
            Track("heroic", $"{AlbumRoot}/heroic.mp3", "HEROIC", "Heroic"),
            Track("hymn", $"{AlbumRoot}/hymn.mp3", "HYMN", "Hymn"),
            Track("reminiscence", $"{AlbumRoot}/reminiscence.mp3", "REMINISCENCE", "Reminiscence"),
            Track("final_war", $"{EncounterRoot}/boss_glory.mp3", "FINAL_WAR", "Final War"),
            Track("dragon_sts2", $"{AlbumRoot}/dragon_sts2.ogg", "DRAGON_STS2", "DRAGON (Slay the Spire 2 ver.)"),
            Track("storm_sts2", $"{AlbumRoot}/storm_sts2.ogg", "STORM_STS2", "STORM (Slay the Spire 2 ver.)"),
        };

    internal static ShinGetterBgmTrack ResolveOrDefault(string? trackId)
    {
        foreach (ShinGetterBgmTrack track in Tracks)
        {
            if (string.Equals(track.Id, trackId, StringComparison.OrdinalIgnoreCase))
                return track;
        }

        return Tracks[0];
    }

    internal static float GetRelativeVolume(ShinGetterBgmCategory category) =>
        category == ShinGetterBgmCategory.Execution ? 1f : 0.70f;

    private static ShinGetterBgmTrack Track(
        string id,
        string resourcePath,
        string localizationSuffix,
        string fallbackTitle) =>
        new(
            id,
            resourcePath,
            $"SHIN_GETTER_CHUNIBYO.BGM.TRACK.{localizationSuffix}",
            fallbackTitle);
}
