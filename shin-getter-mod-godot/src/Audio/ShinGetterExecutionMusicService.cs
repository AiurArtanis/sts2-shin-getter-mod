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
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Audio;

internal static class ShinGetterExecutionMusicService
{
    private const string ExecutionMusicPath = "res://audio/music/shin_getter/execution_theme.mp3";
    private const float FadeInDurationSeconds = 1f;
    private const float CombatEndFadeOutDurationSeconds = 3f;
    private const float SilentVolumeDb = -80f;

    private static readonly ConditionalWeakTable<CombatState, ExecutionMusicState> States = new();
    private static ExecutionMusicState? _activeState;

    static ShinGetterExecutionMusicService()
    {
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    internal static void TryStart(Player owner, CardModel card)
    {
        if (!ReferenceEquals(card.Owner, owner)
            || card.Pile?.Type != PileType.Hand
            || card.CombatState is not CombatState combatState
            || card is not (SGC_StonerSunshine or SGC_StarSlash or SGC_ShiningSpark)
            || owner.PlayerCombatState is not { TurnNumber: >= 2 }
            || !CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsOverOrEnding
            || !ReferenceEquals(CombatManager.Instance.DebugOnlyGetState(), combatState))
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
        if (States.TryGetValue(combatState, out ExecutionMusicState? state))
            await StopStateAndRestore(state);
    }

    internal static Task StopActiveAndRestore() =>
        _activeState is { } state ? StopStateAndRestore(state) : Task.CompletedTask;

    internal static void StopImmediatelyAndRestore()
    {
        if (_activeState is not { } state)
            return;

        _activeState = null;
        DiscardPlayback(state);
        NAudioManager.Instance?.SetBgmVol(SaveManager.Instance.SettingsSave.VolumeBgm);
    }

    private static void OnCombatEnded(CombatRoom _)
    {
        TaskHelper.RunSafely(StopActiveAndRestore());
    }

    private static Task StopStateAndRestore(ExecutionMusicState state)
    {
        if (state.StopCompletion is { } existingStop)
            return existingStop.Task;

        if (!ReferenceEquals(_activeState, state) || !state.IsActive)
            return Task.CompletedTask;

        state.IsActive = false;
        state.FadeTween?.Kill();

        var stopCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        state.StopCompletion = stopCompletion;
        float restoredBgmVolume = SaveManager.Instance.SettingsSave.VolumeBgm;
        AudioStreamPlayer? player = state.Player;
        if (player == null || !GodotObject.IsInstanceValid(player))
        {
            CompleteStop(state, player, stopCompletion, restoredBgmVolume);
            return stopCompletion.Task;
        }

        state.CurrentBgmVolume = 0f;
        NAudioManager.Instance?.SetBgmVol(0f);

        Tween tween = player.CreateTween();
        state.FadeTween = tween;
        tween.TweenProperty(player, "volume_db", SilentVolumeDb, CombatEndFadeOutDurationSeconds);
        tween.Finished += () => CompleteStop(state, player, stopCompletion, restoredBgmVolume);
        return stopCompletion.Task;
    }

    private static void CompleteStop(
        ExecutionMusicState state,
        AudioStreamPlayer? player,
        TaskCompletionSource<bool> completion,
        float restoredBgmVolume)
    {
        if (GodotObject.IsInstanceValid(player))
            player!.QueueFree();

        if (ReferenceEquals(state.Player, player))
            state.Player = null;
        state.FadeTween = null;
        state.StopCompletion = null;

        if (ReferenceEquals(_activeState, state))
        {
            _activeState = null;
            NAudioManager.Instance?.SetBgmVol(restoredBgmVolume);
        }

        completion.TrySetResult(true);
    }

    private static void StartPlayback(ExecutionMusicState state)
    {
        if (NonInteractiveMode.IsActive
            || ResourceLoader.Load<AudioStream>(ExecutionMusicPath) is not { } stream
            || Engine.GetMainLoop() is not SceneTree sceneTree)
        {
            return;
        }

        bool replacedEncounterMusic = ShinGetterEncounterMusicService.SuspendForExecution();
        ExecutionMusicState? previousState = _activeState;
        _activeState = null;
        if (previousState != null && !ReferenceEquals(previousState, state))
            DiscardPlayback(previousState);

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
        _activeState = state;
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
        if (replacedEncounterMusic)
        {
            state.CurrentBgmVolume = 0f;
            NAudioManager.Instance?.SetBgmVol(0f);
        }
        else
        {
            tween.TweenMethod(
                Callable.From<float>(volume =>
                {
                    state.CurrentBgmVolume = volume;
                    NAudioManager.Instance?.SetBgmVol(volume);
                }),
                configuredBgmVolume,
                0f,
                FadeInDurationSeconds);
        }
        tween.TweenProperty(
            player,
            "volume_db",
            Mathf.LinearToDb(executionMusicVolume),
            FadeInDurationSeconds);
    }

    private static void DiscardPlayback(ExecutionMusicState state)
    {
        state.IsActive = false;
        state.FadeTween?.Kill();
        state.FadeTween = null;

        if (state.Player is { } player && GodotObject.IsInstanceValid(player))
            player.QueueFree();
        state.Player = null;

        state.StopCompletion?.TrySetResult(true);
        state.StopCompletion = null;
    }

    private sealed class ExecutionMusicState
    {
        public bool HasTriggered;
        public bool IsActive;
        public float CurrentBgmVolume;
        public AudioStreamPlayer? Player;
        public Tween? FadeTween;
        public TaskCompletionSource<bool>? StopCompletion;
    }
}
