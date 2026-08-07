#nullable enable
using System;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
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
        if (track.Id == ShinGetterBgmCatalog.DefaultTrackId || string.IsNullOrWhiteSpace(track.ResourcePath))
            return;

        if (ActiveCategory == category && ActiveTrackId == track.Id && _player != null)
        {
            bool resume = State == ShinGetterBgmPreviewState.Paused;
            _player.StreamPaused = !resume;
            State = resume ? ShinGetterBgmPreviewState.Playing : ShinGetterBgmPreviewState.Paused;
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
        if (NonInteractiveMode.IsActive
            || ResourceLoader.Load<AudioStream>(track.ResourcePath) is not { } loadedStream
            || Engine.GetMainLoop() is not SceneTree sceneTree)
        {
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
