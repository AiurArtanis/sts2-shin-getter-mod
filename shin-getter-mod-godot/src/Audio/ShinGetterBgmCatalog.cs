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
    internal const string RandomTrackId = "random";
    internal const string DragonSts2TrackId = "dragon_sts2";
    internal const string StormSts2TrackId = "storm_sts2";
    internal const string HeatsSts2TrackId = "heats_sts2";
    internal const string GetterRoboSts2TrackId = "getter_robo_sts2";
    internal const string DefaultExecutionMusicPath =
        "res://audio/music/shin_getter/album/heroic.mp3";

    private const string AlbumRoot = "res://audio/music/shin_getter/album";
    private const string EncounterRoot = "res://audio/music/shin_getter/encounters";

    internal static IReadOnlyList<ShinGetterBgmTrack> Tracks { get; } =
        new ShinGetterBgmTrack[]
        {
            Track(DefaultTrackId, string.Empty, "DEFAULT", "(default)"),
            Track("relief", $"{AlbumRoot}/relief.mp3", "RELIEF", "Relief"),
            Track("rebel_army", $"{EncounterRoot}/boss_overgrowth.mp3", "REBEL_ARMY", "Rebel Army"),
            Track("past", $"{AlbumRoot}/past.mp3", "PAST", "Past"),
            Track("tension", $"{EncounterRoot}/elite_glory.mp3", "TENSION", "Tension"),
            Track("mystery", $"{EncounterRoot}/boss_underdocks.mp3", "MYSTERY", "Mystery"),
            Track("momentum", $"{EncounterRoot}/elite_overgrowth.mp3", "MOMENTUM", "Momentum"),
            Track("majesty", $"{EncounterRoot}/boss_hive.mp3", "MAJESTY", "Majesty"),
            Track("unknown", $"{EncounterRoot}/elite_underdocks.mp3", "UNKNOWN", "Unknown"),
            Track("onslaught", $"{AlbumRoot}/onslaught.mp3", "ONSLAUGHT", "Onslaught"),
            Track("bond_of_blood", $"{AlbumRoot}/bond_of_blood.mp3", "BOND_OF_BLOOD", "Bond of Blood"),
            Track("heroic", $"{AlbumRoot}/heroic.mp3", "HEROIC", "Bravery"),
            Track("hymn", $"{AlbumRoot}/hymn.mp3", "HYMN", "Hymn"),
            Track("reminiscence", $"{AlbumRoot}/reminiscence.mp3", "REMINISCENCE", "Reminiscence"),
            Track("final_war", $"{EncounterRoot}/boss_glory.mp3", "FINAL_WAR", "Final War"),
            Track("creation", $"{AlbumRoot}/creation.mp3", "CREATION", "Creation"),
            Track("hostility", $"{AlbumRoot}/hostility.mp3", "HOSTILITY", "Hostility"),
            Track("forward", $"{EncounterRoot}/elite_hive.mp3", "FORWARD", "Forward"),
            Track("its_time", $"{AlbumRoot}/its_time.mp3", "ITS_TIME", "It's Time"),
            Track(DragonSts2TrackId, $"{AlbumRoot}/dragon_sts2.mp3", "DRAGON_STS2", "DRAGON(slay the spire 2 ver.)"),
            Track(StormSts2TrackId, $"{AlbumRoot}/storm_sts2.mp3", "STORM_STS2", "STORM(slay the spire 2 ver.)"),
            Track(HeatsSts2TrackId, $"{AlbumRoot}/heats_sts2.mp3", "HEATS_STS2", "HEATS(slay the spire 2 ver.)"),
            Track(GetterRoboSts2TrackId, $"{AlbumRoot}/getter_robo_sts2.mp3", "GETTER_ROBO_STS2", "GETTER ROBO(slay the spire 2 ver.)"),
            Track("bravery_iron_saga", $"{AlbumRoot}/bravery_iron_saga.mp3", "BRAVERY_IRON_SAGA", "Bravery(Iron Saga Ver.)"),
            Track(RandomTrackId, string.Empty, "RANDOM", "Random"),
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

    internal static ShinGetterBgmTrack ResolveForPlayback(ShinGetterBgmTrack selectedTrack)
    {
        if (selectedTrack.Id != RandomTrackId)
            return selectedTrack;

        // Default is first and Random is last; choose only a concrete dropdown track.
        return Tracks[Random.Shared.Next(1, Tracks.Count - 1)];
    }

    internal static bool CanPreview(ShinGetterBgmTrack track) =>
        track.Id != DefaultTrackId
        && (track.Id == RandomTrackId || !string.IsNullOrWhiteSpace(track.ResourcePath));

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
