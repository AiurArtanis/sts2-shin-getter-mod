#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Nodes.Combat;

public static partial class NShinGetterStaticVisuals
{
    private static async Task<bool> TryPlayTacticalRetreatFusion(NCreatureVisuals visuals,
        FormSprites sprites, FormVisual? previous, FormVisual next, float speed, TacticalRetreatSync sync)
    {
        if (!TryGetAtomicForm(next, out var nextForm) || next.Node is not AnimatedSprite2D nextSprite
            || !TryReadTacticalFrames(nextSprite, nextForm, speed, out var nextFrames))
            return false;

        AnimatedSprite2D? previousSprite = null;
        TacticalRetreatFrames? previousFrames = null;
        if (previous is { } old)
        {
            if (!TryGetAtomicForm(old, out var oldForm) || old.Node is not AnimatedSprite2D oldSprite
                || !TryReadTacticalFrames(oldSprite, oldForm, speed, out previousFrames))
                return false;
            previousSprite = oldSprite;
        }

        var timeline = new TacticalRetreatTimeline(previousFrames, FusionTransitionHoldSeconds, nextFrames!);
        Vector2 nextBaseScale = next.Node.Scale;
        Vector2 previousBaseScale = previous is { } visible && IsTacticalVisualAlive(visible.Node)
            ? visible.Node.Scale : Vector2.One;
        bool forwardStarted = false;
        Tween clock = visuals.CreateTween();
        clock.Pause();
        try
        {
            // Both sheets are ready before this single clock starts. No loading or extra waits between phases.
            await TacticalRetreatTween.PlayClock(visuals, clock, (float)timeline.Duration, progress =>
            {
                if (!IsTacticalVisualAlive(next.Node)
                    || (previousSprite != null && !IsTacticalVisualAlive(previousSprite)))
                    throw new OperationCanceledException("Fusion sprite left the scene.");
                TacticalRetreatSample sample = timeline.Sample(timeline.Duration * progress);
                bool forward = sample.Phase == TacticalRetreatPhase.Forward;
                AnimatedSprite2D active = forward ? nextSprite : previousSprite!;
                if (forward && !forwardStarted)
                {
                    forwardStarted = true;
                    foreach (FormVisual visual in sprites.All)
                    {
                        if (!IsTacticalVisualAlive(visual.Node)) continue;
                        bool isNext = visual.Item == next.Item;
                        visual.Item.Visible = isNext;
                        visual.Item.Modulate = new Color(visual.Item.Modulate, isNext ? 1f : 0f);
                        if (!isNext && visual.Node is AnimatedSprite2D animation)
                            NShinGetterSpriteSequence.ReleaseActionAnimations(animation);
                    }
                }
                active.Stop();
                active.Animation = NShinGetterSpriteSequence.FusionAnimationName;
                active.SpeedScale = 1f;
                active.Frame = sample.Frame;
                active.FrameProgress = 0f;
                sync.Advance(sample.Progress);
            }, sync.CancellationToken);
        }
        finally
        {
            FinishTacticalRetreatForm(sprites, next, nextBaseScale, previous, previousBaseScale, !sync.ShouldAbortAction);
        }
        return true;
    }

    private static bool TryReadTacticalFrames(AnimatedSprite2D sprite, ShinGetterForm form, float speed,
        out TacticalRetreatFrames? playback)
    {
        playback = null;
        if (!IsTacticalVisualAlive(sprite) || !NShinGetterSpriteSequence.EnsureFusionLoaded(sprite, form)
            || sprite.SpriteFrames is not { } frames)
            return false;
        StringName name = NShinGetterSpriteSequence.FusionAnimationName;
        try
        {
            playback = new TacticalRetreatFrames(Enumerable.Range(0, frames.GetFrameCount(name))
                .Select(frame => (double)frames.GetFrameDuration(name, frame)), frames.GetAnimationSpeed(name), speed);
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool IsTacticalVisualAlive(Node node) =>
        GodotObject.IsInstanceValid(node) && node.IsInsideTree() && !node.IsQueuedForDeletion();

    private static void FinishTacticalRetreatForm(FormSprites sprites, FormVisual next, Vector2 nextScale,
        FormVisual? previous, Vector2 previousScale, bool activateIdle = true)
    {
        foreach (FormVisual visual in sprites.All)
        {
            if (!IsTacticalVisualAlive(visual.Node)) continue;
            bool isNext = visual.Item == next.Item;
            visual.Item.Visible = isNext;
            visual.Item.Modulate = new Color(visual.Item.Modulate, isNext ? 1f : 0f);
            visual.Node.RotationDegrees = 0f;
            if (isNext) visual.Node.Scale = nextScale;
            else if (previous is { } old && visual.Item == old.Item) visual.Node.Scale = previousScale;
            if (visual.Node is AnimatedSprite2D animation)
            {
                animation.SpeedScale = 1f;
                if (!isNext) NShinGetterSpriteSequence.ReleaseActionAnimations(animation);
            }
        }
        if (activateIdle && IsTacticalVisualAlive(next.Node)) ActivateIdleAnimation(next);
    }
}
