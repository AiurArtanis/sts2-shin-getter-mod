#nullable enable
using System;
using System.Runtime.CompilerServices;
using Godot;

namespace ShinGetterMod.Nodes.Combat;

internal static class NShinGetterSpriteAnimationStateMachine
{
    private static readonly ConditionalWeakTable<AnimatedSprite2D, State> States = new();

    public static bool TryPlay(AnimatedSprite2D sprite, string trigger) =>
        TryPlay(sprite, trigger, NShinGetterSpriteSequence.EnsureLoaded);

    public static bool TryPlay(
        AnimatedSprite2D sprite,
        string trigger,
        Action<AnimatedSprite2D, string> ensureLoaded)
    {
        string? animationName = trigger switch
        {
            "Attack" => NShinGetterSpriteSequence.AttackAnimationName,
            "HeavyAttack" => NShinGetterSpriteSequence.HeavyAttackAnimationName,
            "Cast" => NShinGetterSpriteSequence.CastAnimationName,
            "StonerSunshine" => NShinGetterSpriteSequence.StonerSunshineAnimationName,
            "Cyclone" => NShinGetterSpriteSequence.CycloneAnimationName,
            "DashV2" => NShinGetterSpriteSequence.DashV2AnimationName,
            "DrillAttack" => NShinGetterSpriteSequence.DrillAttackAnimationName,
            "Dash" => NShinGetterSpriteSequence.DashAnimationName,
            "Hit" => NShinGetterSpriteSequence.BlockAnimationName,
            "Block" => NShinGetterSpriteSequence.BlockAnimationName,
            "Dead" => NShinGetterSpriteSequence.DeathAnimationName,
            "Death" => NShinGetterSpriteSequence.DeathAnimationName,
            "Idle" => NShinGetterSpriteSequence.IdleAnimationName,
            _ => null,
        };

        if (animationName == null)
            return false;

        State state = States.GetOrCreateValue(sprite);
        if (ShouldKeepActiveSpecialAnimation(sprite, state, trigger))
        {
            state.NextActionSpeedScale = 1f;
            return true;
        }

        ensureLoaded(sprite, animationName);
        EnsureSignalConnected(sprite, state);

        SpriteFrames? frames = sprite.SpriteFrames;
        if (frames == null || !frames.HasAnimation(animationName))
        {
            PlayIdle(sprite, state);
            return false;
        }

        if (animationName == NShinGetterSpriteSequence.IdleAnimationName)
        {
            PlayIdle(sprite, state);
            return true;
        }

        if (animationName == NShinGetterSpriteSequence.DeathAnimationName)
        {
            state.ActiveOneShotAnimation = string.Empty;
            state.NextActionSpeedScale = 1f;
            sprite.SpeedScale = 1f;
            sprite.Play(animationName);
            return true;
        }

        state.ActiveOneShotAnimation = animationName;
        sprite.SpeedScale = state.NextActionSpeedScale;
        state.NextActionSpeedScale = 1f;
        sprite.Play(animationName);
        return true;
    }

    public static void QueueNextActionSpeed(AnimatedSprite2D sprite, float speedScale)
    {
        State state = States.GetOrCreateValue(sprite);
        state.NextActionSpeedScale = Math.Max(0.05f, speedScale);
    }

    public static void PlayIdle(AnimatedSprite2D sprite) =>
        PlayIdle(sprite, NShinGetterSpriteSequence.EnsureIdleLoaded);

    public static void PlayIdle(AnimatedSprite2D sprite, Action<AnimatedSprite2D> ensureLoaded)
    {
        State state = States.GetOrCreateValue(sprite);
        EnsureSignalConnected(sprite, state);
        ensureLoaded(sprite);
        PlayIdle(sprite, state);
    }

    private static void EnsureSignalConnected(AnimatedSprite2D sprite, State state)
    {
        if (state.SignalConnected)
            return;

        sprite.Connect(AnimatedSprite2D.SignalName.AnimationFinished, Callable.From(() => OnAnimationFinished(sprite)));
        state.SignalConnected = true;
    }

    private static void OnAnimationFinished(AnimatedSprite2D sprite)
    {
        if (!GodotObject.IsInstanceValid(sprite))
            return;

        State state = States.GetOrCreateValue(sprite);
        if (string.IsNullOrEmpty(state.ActiveOneShotAnimation))
            return;

        if (sprite.Animation.ToString() != state.ActiveOneShotAnimation)
            return;

        PlayIdle(sprite, state);
    }

    private static bool ShouldKeepActiveSpecialAnimation(
        AnimatedSprite2D sprite,
        State state,
        string trigger) =>
        sprite.IsPlaying()
        && trigger is "Attack" or "HeavyAttack" or "Cast" or "Dash" or "Hit"
        && IsSpecialAnimation(state.ActiveOneShotAnimation);

    private static bool IsSpecialAnimation(string animationName) =>
        animationName is NShinGetterSpriteSequence.CycloneAnimationName
            or NShinGetterSpriteSequence.DashV2AnimationName
            or NShinGetterSpriteSequence.DrillAttackAnimationName;

    private static void PlayIdle(AnimatedSprite2D sprite, State state)
    {
        state.ActiveOneShotAnimation = string.Empty;
        if (sprite.SpriteFrames?.HasAnimation(NShinGetterSpriteSequence.IdleAnimationName) == true)
        {
            sprite.SpeedScale = 1f;
            sprite.Frame = 0;
            sprite.Play(NShinGetterSpriteSequence.IdleAnimationName);
        }
    }

    private sealed class State
    {
        public bool SignalConnected;
        public string ActiveOneShotAnimation = string.Empty;
        public float NextActionSpeedScale = 1f;
    }
}
