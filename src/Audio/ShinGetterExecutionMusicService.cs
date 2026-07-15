#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Saves;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Audio;

internal static class ShinGetterExecutionMusicService
{
    private const string ExecutionMusicPath = "res://audio/music/shin_getter/execution_theme.mp3";
    private const float FadeDurationSeconds = 1f;
    private const float SilentVolumeDb = -80f;

    private static readonly ConditionalWeakTable<CombatState, ExecutionMusicState> States = new();

    internal static void TryStart(Player owner, CardModel card)
    {
        if (!ReferenceEquals(card.Owner, owner)
            || card.Pile?.Type != PileType.Hand
            || card.CombatState is not CombatState combatState
            || card is not (SGC_StonerSunshine or SGC_StarSlash or SGC_ShiningSpark))
        {
            return;
        }

        ExecutionMusicState state = States.GetOrCreateValue(combatState);
        if (state.HasTriggered)
            return;

        state.HasTriggered = true;
        StartPlayback(state);
    }

    internal static async Task StopAndRestore(CombatState combatState)
    {
        if (!States.TryGetValue(combatState, out ExecutionMusicState? state)
            || !state.IsActive)
        {
            return;
        }

        state.IsActive = false;
        state.FadeTween?.Kill();

        float restoredBgmVolume = SaveManager.Instance.SettingsSave.VolumeBgm;
        AudioStreamPlayer? player = state.Player;
        if (player == null || !GodotObject.IsInstanceValid(player))
        {
            NAudioManager.Instance?.SetBgmVol(restoredBgmVolume);
            return;
        }

        Tween tween = player.CreateTween();
        state.FadeTween = tween;
        tween.SetParallel();
        tween.TweenMethod(
            Callable.From<float>(volume =>
            {
                state.CurrentBgmVolume = volume;
                NAudioManager.Instance?.SetBgmVol(volume);
            }),
            state.CurrentBgmVolume,
            restoredBgmVolume,
            FadeDurationSeconds);
        tween.TweenProperty(player, "volume_db", SilentVolumeDb, FadeDurationSeconds);

        await player.ToSignal(tween, Tween.SignalName.Finished);
        if (GodotObject.IsInstanceValid(player))
            player.QueueFree();
    }

    private static void StartPlayback(ExecutionMusicState state)
    {
        if (NonInteractiveMode.IsActive
            || ResourceLoader.Load<AudioStream>(ExecutionMusicPath) is not { } stream
            || Engine.GetMainLoop() is not SceneTree sceneTree)
        {
            return;
        }

        float configuredBgmVolume = SaveManager.Instance.SettingsSave.VolumeBgm;
        float executionMusicVolume = Mathf.Max(Mathf.Pow(configuredBgmVolume, 2f), 0.0001f);
        var player = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = "Master",
            VolumeDb = SilentVolumeDb,
        };

        state.IsActive = true;
        state.CurrentBgmVolume = configuredBgmVolume;
        state.Player = player;
        player.Finished += () =>
        {
            if (state.IsActive && GodotObject.IsInstanceValid(player))
                player.Play();
        };

        sceneTree.Root.AddChild(player);
        player.Play();

        Tween tween = player.CreateTween();
        state.FadeTween = tween;
        tween.SetParallel();
        tween.TweenMethod(
            Callable.From<float>(volume =>
            {
                state.CurrentBgmVolume = volume;
                NAudioManager.Instance?.SetBgmVol(volume);
            }),
            configuredBgmVolume,
            0f,
            FadeDurationSeconds);
        tween.TweenProperty(
            player,
            "volume_db",
            Mathf.LinearToDb(executionMusicVolume),
            FadeDurationSeconds);
    }

    private sealed class ExecutionMusicState
    {
        public bool HasTriggered;
        public bool IsActive;
        public float CurrentBgmVolume;
        public AudioStreamPlayer? Player;
        public Tween? FadeTween;
    }
}
