#nullable enable
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Audio;

internal static class ShinGetterEncounterMusicService
{
    private const float RelativeVolume = 0.82f;
    private const float FadeInDurationSeconds = 0.5f;
    private const float SilentVolumeDb = -80f;

    private const string OvergrowthElite = "res://audio/music/shin_getter/encounters/elite_overgrowth.mp3";
    private const string UnderdocksElite = "res://audio/music/shin_getter/encounters/elite_underdocks.mp3";
    private const string HiveElite = "res://audio/music/shin_getter/encounters/elite_hive.mp3";
    private const string GloryElite = "res://audio/music/shin_getter/encounters/elite_glory.mp3";
    private const string OvergrowthBoss = "res://audio/music/shin_getter/encounters/boss_overgrowth.mp3";
    private const string UnderdocksBoss = "res://audio/music/shin_getter/encounters/boss_underdocks.mp3";
    private const string HiveBoss = "res://audio/music/shin_getter/encounters/boss_hive.mp3";
    private const string GloryBoss = "res://audio/music/shin_getter/encounters/boss_glory.mp3";

    private static EncounterMusicState? _activeState;

    static ShinGetterEncounterMusicService()
    {
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    internal static void TryStart(CombatRoom room, IRunState? runState)
    {
        StopActiveAndRestore();
        ShinGetterExecutionMusicService.StopImmediatelyAndRestore();

        if (runState is null || NonInteractiveMode.IsActive)
            return;
        if (!runState.Players.Any(player => player.Character is ShinGetter))
            return;
        if (!TryResolveTrack(runState.Act.CanonicalInstance, room.RoomType, out string trackPath))
            return;
        if (ResourceLoader.Load<AudioStream>(trackPath) is not { } loadedStream)
            return;
        if (Engine.GetMainLoop() is not SceneTree sceneTree)
            return;

        AudioStream stream = loadedStream.Duplicate() as AudioStream ?? loadedStream;
        if (stream is AudioStreamMP3 mp3)
            mp3.Loop = true;

        float configuredBgmVolume = SaveManager.Instance.SettingsSave.VolumeBgm;
        float encounterMusicVolume = Mathf.Max(
            Mathf.Pow(configuredBgmVolume, 2f) * RelativeVolume,
            0.0001f);
        var player = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = "Master",
            VolumeDb = SilentVolumeDb,
        };
        var state = new EncounterMusicState(room.CombatState, player);
        _activeState = state;

        NAudioManager.Instance?.SetBgmVol(0f);
        sceneTree.Root.AddChild(player);
        player.Play();

        Tween tween = player.CreateTween();
        state.FadeTween = tween;
        tween.TweenProperty(
            player,
            "volume_db",
            Mathf.LinearToDb(encounterMusicVolume),
            FadeInDurationSeconds);
    }

    internal static bool SuspendForExecution()
    {
        if (_activeState is not { } state)
            return false;

        _activeState = null;
        Discard(state);
        return true;
    }

    internal static void StopActiveAndRestore()
    {
        if (_activeState is not { } state)
            return;

        _activeState = null;
        Discard(state);
        NAudioManager.Instance?.SetBgmVol(SaveManager.Instance.SettingsSave.VolumeBgm);
    }

    private static bool TryResolveTrack(ActModel act, RoomType roomType, out string trackPath)
    {
        trackPath = (act, roomType) switch
        {
            (Overgrowth, RoomType.Elite) => OvergrowthElite,
            (Underdocks, RoomType.Elite) => UnderdocksElite,
            (Hive, RoomType.Elite) => HiveElite,
            (Glory, RoomType.Elite) => GloryElite,
            (Overgrowth, RoomType.Boss) => OvergrowthBoss,
            (Underdocks, RoomType.Boss) => UnderdocksBoss,
            (Hive, RoomType.Boss) => HiveBoss,
            (Glory, RoomType.Boss) => GloryBoss,
            _ => string.Empty,
        };
        return trackPath.Length > 0;
    }

    private static void OnCombatEnded(CombatRoom room)
    {
        if (_activeState is { } state && ReferenceEquals(state.CombatState, room.CombatState))
            StopActiveAndRestore();
    }

    private static void Discard(EncounterMusicState state)
    {
        state.FadeTween?.Kill();
        state.FadeTween = null;
        if (GodotObject.IsInstanceValid(state.Player))
            state.Player.QueueFree();
    }

    private sealed class EncounterMusicState(CombatState combatState, AudioStreamPlayer player)
    {
        public CombatState CombatState { get; } = combatState;
        public AudioStreamPlayer Player { get; } = player;
        public Tween? FadeTween { get; set; }
    }
}
