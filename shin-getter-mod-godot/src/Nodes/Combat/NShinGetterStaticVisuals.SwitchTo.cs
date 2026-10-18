#nullable enable
using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ShinGetterMod.Nodes.Combat;

public static partial class NShinGetterStaticVisuals
{
    private static async Task SwitchTo(NCreatureVisuals visuals, FormSprites sprites, FormVisual next,
        bool animate, float speedScale, TacticalRetreatSync? retreatSync = null)
    {
        try
        {
            await SwitchToCore(visuals, sprites, next, animate, speedScale, retreatSync);
        }
        catch (OperationCanceledException) when (retreatSync != null && !retreatSync.ShouldAbortAction)
        {
            // A disappearing/rebuilt visual is not a canceled model action. Do not skip the card's remaining effects.
        }
    }

    private static async Task SwitchToCore(NCreatureVisuals visuals, FormSprites sprites, FormVisual next,
        bool animate, float speedScale, TacticalRetreatSync? retreatSync)
    {
        retreatSync?.CancellationToken.ThrowIfCancellationRequested();
        FormVisual? previous = null;
        foreach (var sprite in sprites.All)
            if (sprite.Item != next.Item && sprite.Item.Visible && sprite.Item.Modulate.A > 0.01f)
                previous = sprite;

        if (next.Item.Visible && next.Item.Modulate.A > 0.99f)
        {
            ActivateIdleAnimation(next);
            return;
        }
        if (!animate)
        {
            foreach (var sprite in sprites.All)
            {
                bool isNext = sprite.Item == next.Item;
                sprite.Item.Visible = isNext;
                sprite.Item.Modulate = new Color(sprite.Item.Modulate, isNext ? 1f : 0f);
                sprite.Node.RotationDegrees = 0f;
                if (!isNext && sprite.Node is AnimatedSprite2D animation)
                    NShinGetterSpriteSequence.ReleaseActionAnimations(animation);
            }
            ActivateIdleAnimation(next);
            return;
        }
        float animationSpeed = Math.Max(0.05f, speedScale);
        if (retreatSync != null
            ? await TryPlayTacticalRetreatFusion(visuals, sprites, previous, next, animationSpeed, retreatSync)
            : await TryPlayFusionTransition(sprites, previous, next, animationSpeed))
            return;

        foreach (var sprite in sprites.All)
        {
            if (sprite.Item != next.Item && (previous == null || sprite.Item != previous.Value.Item))
            {
                sprite.Item.Visible = false;
                sprite.Item.Modulate = new Color(sprite.Item.Modulate, 0f);
                sprite.Node.RotationDegrees = 0f;
            }
        }
        Vector2 nextBaseScale = next.Node.Scale;
        next.Item.Visible = true;
        ActivateIdleAnimation(next);
        next.Item.Modulate = new Color(next.Item.Modulate, 0f);
        next.Node.Scale = nextBaseScale * 0.76f;
        next.Node.RotationDegrees = -5f;

        // The existing crossfade parameters also define the tactical-return clock's total length.
        float fadeOutSeconds = 0.16f / animationSpeed;
        float fadeInSeconds = 0.28f / animationSpeed;
        float fadeInDelaySeconds = 0.06f / animationSpeed;
        float cleanupSeconds = 0.36f / animationSpeed;
        Tween transformTween = visuals.CreateTween().SetParallel();
        if (retreatSync != null) transformTween.Pause();
        Vector2 previousBaseScale = Vector2.One;
        if (previous != null)
        {
            FormVisual previousVisual = previous.Value;
            previousBaseScale = previousVisual.Node.Scale;
            transformTween.TweenProperty(previousVisual.Item, "modulate:a", 0f, fadeOutSeconds)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
            transformTween.TweenProperty(previousVisual.Node, "scale", previousBaseScale * 1.18f, fadeOutSeconds)
                .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
        }
        transformTween.TweenProperty(next.Item, "modulate:a", 1f, fadeInSeconds)
            .SetDelay(fadeInDelaySeconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
        transformTween.TweenProperty(next.Node, "scale", nextBaseScale, fadeInSeconds)
            .SetDelay(fadeInDelaySeconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        transformTween.TweenProperty(next.Node, "rotation_degrees", 0f, fadeInSeconds)
            .SetDelay(fadeInDelaySeconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
        if (retreatSync == null)
        {
            transformTween.TweenCallback(Callable.From(() => HideInactive(sprites, next, previous, previousBaseScale)))
                .SetDelay(cleanupSeconds);
            await visuals.ToSignal(transformTween, Tween.SignalName.Finished);
        }
        else
        {
            try
            {
                float totalSeconds = Math.Max(cleanupSeconds, Math.Max(fadeOutSeconds, fadeInDelaySeconds + fadeInSeconds));
                await TacticalRetreatTween.PlayClock(visuals, transformTween, totalSeconds,
                    progress => retreatSync.Advance(progress), retreatSync.CancellationToken);
            }
            finally
            {
                FinishTacticalRetreatForm(sprites, next, nextBaseScale, previous, previousBaseScale, !retreatSync.ShouldAbortAction);
            }
        }
    }
}
