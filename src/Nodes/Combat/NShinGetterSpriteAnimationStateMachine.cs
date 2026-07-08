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

    public static bool TryPlay(AnimatedSprite2D sprite, string trigger, Action<AnimatedSprite2D> ensureLoaded)
    {
        string? animationName = trigger switch
        {
            "Attack" => NShinGetterSpriteSequence.AttackAnimationName,
            "HeavyAttack" => NShinGetterSpriteSequence.HeavyAttackAnimationName,
            "Cast" => NShinGetterSpriteSequence.CastAnimationName,
            "Idle" => NShinGetterSpriteSequence.IdleAnimationName,
            _ => null,
        };

        if (animationName == null)
            return false;

        ensureLoaded(sprite);
        SpriteFrames? frames = sprite.SpriteFrames;
        if (frames == null || !frames.HasAnimation(animationName))
            return false;

        State state = States.GetOrCreateValue(sprite);
        EnsureSignalConnected(sprite, state);

        if (animationName == NShinGetterSpriteSequence.IdleAnimationName)
        {
            PlayIdle(sprite, state);
            return true;
        }

        state.ActiveOneShotAnimation = animationName;
        sprite.Play(animationName);
        return true;
    }

    public static void PlayIdle(AnimatedSprite2D sprite) =>
        PlayIdle(sprite, NShinGetterSpriteSequence.EnsureLoaded);

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
    }
}
