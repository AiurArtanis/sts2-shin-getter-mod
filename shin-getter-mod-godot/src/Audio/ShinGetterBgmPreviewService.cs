#nullable enable
using System;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Saves;

namespace ShinGetterMod.Audio;

internal enum ShinGetterBgmPreviewState
{
    Stopped,
    Playing,
    Paused,
}

internal static class ShinGetterBgmPreviewService
{
    private static AudioStreamPlayer? _player;

    internal static event Action? StateChanged;

    internal static ShinGetterBgmPreviewState State { get; private set; }
    internal static ShinGetterBgmCategory? ActiveCategory { get; private set; }
    internal static string? ActiveTrackId { get; private set; }

    internal static void Toggle(ShinGetterBgmTrack track, ShinGetterBgmCategory category)
    {
        if (!ShinGetterBgmCatalog.CanPreview(track))
        {
            Log.Info($"[ShinGetterBgmPreview] Ignored preview request for default track ({category}).");
            return;
        }

        if (ActiveCategory == category && ActiveTrackId == track.Id && _player != null)
        {
            bool resume = State == ShinGetterBgmPreviewState.Paused;
            _player.StreamPaused = !resume;
            State = resume ? ShinGetterBgmPreviewState.Playing : ShinGetterBgmPreviewState.Paused;
            Log.Info($"[ShinGetterBgmPreview] {(resume ? "Resumed" : "Paused")} {track.Id} ({category}).");
            StateChanged?.Invoke();
            return;
        }

        Start(track, category);
    }

    internal static void Stop()
    {
        if (_player == null && State == ShinGetterBgmPreviewState.Stopped)
            return;

        StopInternal(restoreGameMusic: true);
        StateChanged?.Invoke();
    }

    private static void Start(ShinGetterBgmTrack track, ShinGetterBgmCategory category)
    {
        if (NonInteractiveMode.IsActive)
        {
            Log.Warn($"[ShinGetterBgmPreview] Cannot preview {track.Id}: non-interactive mode is active.");
            return;
        }

        ShinGetterBgmTrack playbackTrack = ShinGetterBgmCatalog.ResolveForPlayback(track);
        if (!ResourceLoader.Exists(playbackTrack.ResourcePath)
            || ResourceLoader.Load<AudioStream>(playbackTrack.ResourcePath) is not { } loadedStream)
        {
            Log.Error($"[ShinGetterBgmPreview] Cannot load preview resource: {playbackTrack.ResourcePath}");
            return;
        }

        if (Engine.GetMainLoop() is not SceneTree sceneTree)
        {
            Log.Error($"[ShinGetterBgmPreview] Cannot preview {track.Id}: no active SceneTree.");
            return;
        }

        StopInternal(restoreGameMusic: false);
        AudioStream stream = loadedStream.Duplicate() as AudioStream ?? loadedStream;
        EnableLoop(stream);

        float configuredBgmVolume = SaveManager.Instance.SettingsSave.VolumeBgm;
        float previewVolume = Mathf.Max(
            Mathf.Pow(configuredBgmVolume, 2f) * ShinGetterBgmCatalog.GetRelativeVolume(category),
            0.0001f);
        var player = new AudioStreamPlayer
        {
            Name = "ShinGetterBgmPreview",
            Stream = stream,
            Bus = "Master",
            VolumeDb = Mathf.LinearToDb(previewVolume),
            ProcessMode = Node.ProcessModeEnum.Always,
        };

        _player = player;
        ActiveCategory = category;
        ActiveTrackId = track.Id;
        State = ShinGetterBgmPreviewState.Playing;

        NAudioManager.Instance?.SetBgmVol(0f);
        sceneTree.Root.AddChild(player);
        player.Finished += () =>
        {
            if (ReferenceEquals(_player, player)
                && State == ShinGetterBgmPreviewState.Playing
                && GodotObject.IsInstanceValid(player))
            {
                player.Play();
            }
        };
        player.Play();
        Log.Info(
            $"[ShinGetterBgmPreview] Started {playbackTrack.Id} ({category}, selected={track.Id}) "
            + $"from {playbackTrack.ResourcePath}; "
            + $"playing={player.Playing}, bgm={configuredBgmVolume:0.###}, "
            + $"master={SaveManager.Instance.SettingsSave.VolumeMaster:0.###}, db={player.VolumeDb:0.##}.");
        StateChanged?.Invoke();
    }

    private static void StopInternal(bool restoreGameMusic)
    {
        AudioStreamPlayer? player = _player;
        _player = null;
        ActiveCategory = null;
        ActiveTrackId = null;
        State = ShinGetterBgmPreviewState.Stopped;

        if (GodotObject.IsInstanceValid(player))
            player!.QueueFree();
        if (restoreGameMusic)
            NAudioManager.Instance?.SetBgmVol(SaveManager.Instance.SettingsSave.VolumeBgm);

        Log.Info($"[ShinGetterBgmPreview] Stopped; restoreGameMusic={restoreGameMusic}.");
    }

    internal static void EnableLoop(AudioStream stream)
    {
        switch (stream)
        {
            case AudioStreamMP3 mp3:
                mp3.Loop = true;
                break;
            case AudioStreamOggVorbis ogg:
                ogg.Loop = true;
                break;
        }
    }
}
