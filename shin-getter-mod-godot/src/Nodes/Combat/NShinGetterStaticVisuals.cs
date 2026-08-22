#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Nodes.Combat;

public static class NShinGetterStaticVisuals
{
    private const float OpeningFusionFirstFrameHoldSeconds = 0.2f;
    private const float FusionTransitionHoldSeconds = 0.2f;
    private const float ShadeAfterimageSpacing = 182f;
    private const float ShinDragonOpenGetAlpha = 0.3f;

    public static Task ShowForm(
        Creature creature,
        ShinGetterForm form,
        bool animate = true,
        float speedScale = 1f)
    {
        if (!TryGetFormSprites(creature, out var visuals, out var sprites))
            return Task.CompletedTask;

        FormVisual next = form switch
        {
            ShinGetterForm.Getter2 => sprites.GetterTwo,
            ShinGetterForm.Getter3 => sprites.GetterThree,
            _ => sprites.GetterOne,
        };

        return SwitchTo(visuals, sprites, next, animate, speedScale);
    }

    /// <summary>
    /// Plays the combat-opening Getter One fusion after the starting form has been prepared.
    /// Unlike normal fighter-to-fighter transitions, it has no previous form to reverse.
    /// </summary>
    public static Task PlayOpeningGetterOneFusion(Creature creature)
    {
        if (!TryGetFormSprites(creature, out _, out var sprites))
            return Task.CompletedTask;

        return PlayOpeningGetterOneFusion(sprites);
    }

    /// <summary>
    /// Readies the opening form without exposing its idle frame. The opening fusion is started
    /// only after the combat-start state and voice context are ready.
    /// </summary>
    public static void PrepareOpeningGetterOneFusion(Creature creature)
    {
        if (!TryGetFormSprites(creature, out _, out var sprites))
            return;

        foreach (FormVisual sprite in sprites.All)
        {
            sprite.Item.Visible = false;
            sprite.Item.Modulate = new Color(sprite.Item.Modulate, 0f);
            sprite.Node.RotationDegrees = 0f;
            if (sprite.Node is AnimatedSprite2D animation)
                NShinGetterSpriteSequence.ReleaseActionAnimations(animation);
        }
    }

    public static Task ShowShinDragon(Creature creature, bool animate = true)
    {
        if (!TryGetFormSprites(creature, out var visuals, out var sprites))
            return Task.CompletedTask;

        return SwitchTo(visuals, sprites, sprites.ShinDragon, animate, 1f);
    }

    public static bool TryPlayGetterOneActionAnimation(NCreature creatureNode, string trigger)
    {
        if (creatureNode.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/GetterOne") is not { } getterOneAnimation)
            return false;

        return TryPlayVisibleActionAnimation(getterOneAnimation, trigger, NShinGetterSpriteSequence.EnsureLoaded);
    }

    public static bool TryPlayCreatureActionAnimation(Creature creature, string trigger)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        return creatureNode != null && TryPlayGetterActionAnimation(creatureNode, trigger);
    }

    public static async Task PlayCreatureActionAnimationAndWait(
        Creature creature,
        string trigger,
        float fallbackDuration)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (creatureNode == null
            || !TryGetVisibleFormAnimation(creatureNode, out FormAnimation formAnimation)
            || !TryPlayVisibleActionAnimation(
                formAnimation.Sprite,
                trigger,
                formAnimation.EnsureLoaded))
        {
            await Cmd.CustomScaledWait(fallbackDuration, fallbackDuration);
            return;
        }

        AnimatedSprite2D sprite = formAnimation.Sprite;
        StringName animation = sprite.Animation;
        int frameCount = sprite.SpriteFrames?.GetFrameCount(animation) ?? 0;
        double framesPerSecond = sprite.SpriteFrames?.GetAnimationSpeed(animation) ?? 0d;
        float speedScale = Math.Max(0.05f, Math.Abs(sprite.SpeedScale));
        float duration = frameCount > 0 && framesPerSecond > 0d
            ? (float)(frameCount / framesPerSecond / speedScale)
            : fallbackDuration;
        await Cmd.CustomScaledWait(duration, duration);
    }

    public static bool TryPlayGetterActionAnimation(NCreature creatureNode, string trigger)
    {
        return TryPlayVisibleFormActionAnimation(creatureNode, trigger);
    }

    public static float PlayGetterDeathAnimation(NCreature creatureNode)
    {
        if (!TryGetVisibleFormAnimation(creatureNode, out FormAnimation formAnimation))
            return 0f;

        AnimatedSprite2D sprite = formAnimation.Sprite;
        if (!TryPlayVisibleActionAnimation(sprite, "Dead", formAnimation.EnsureLoaded)
            || sprite.SpriteFrames is not { } frames)
        {
            return 0f;
        }

        StringName animation = sprite.Animation;
        int frameCount = frames.GetFrameCount(animation);
        double framesPerSecond = frames.GetAnimationSpeed(animation);
        float speedScale = Math.Max(0.05f, Math.Abs(sprite.SpeedScale));
        return framesPerSecond > 0.0
            ? (float)(frameCount / framesPerSecond / speedScale)
            : 0f;
    }

    public static bool QueueNextActionSpeed(Creature creature, float speedScale)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (creatureNode == null)
            return false;

        foreach (var formAnimation in GetFormAnimations(creatureNode))
        {
            AnimatedSprite2D sprite = formAnimation.Sprite;
            if (!sprite.Visible || sprite.Modulate.A <= 0.01f)
                continue;

            NShinGetterSpriteAnimationStateMachine.QueueNextActionSpeed(sprite, speedScale);
            return true;
        }

        return false;
    }

    public static async Task PlayPhasedCreatureActionAnimation(
        Creature creature,
        string trigger,
        float firstHalfSpeedScale,
        float secondHalfSpeedScale,
        Func<Task> onSecondHalf,
        float fallbackFirstHalfDuration = 0.56f,
        float? firstHalfDurationOverride = null)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (creatureNode == null || !TryGetVisibleFormAnimation(creatureNode, out FormAnimation formAnimation))
        {
            await Cmd.CustomScaledWait(fallbackFirstHalfDuration, fallbackFirstHalfDuration);
            await onSecondHalf();
            return;
        }

        AnimatedSprite2D sprite = formAnimation.Sprite;
        NShinGetterSpriteAnimationStateMachine.QueueNextActionSpeed(sprite, firstHalfSpeedScale);
        if (!TryPlayVisibleActionAnimation(sprite, trigger, formAnimation.EnsureLoaded))
        {
            await Cmd.CustomScaledWait(fallbackFirstHalfDuration, fallbackFirstHalfDuration);
            await onSecondHalf();
            return;
        }

        StringName animation = sprite.Animation;
        SpriteFrames? frames = sprite.SpriteFrames;
        int frameCount = frames?.GetFrameCount(animation) ?? 0;
        double framesPerSecond = frames?.GetAnimationSpeed(animation) ?? 0d;
        int secondHalfFrame = Math.Max(1, frameCount / 2);
        float firstHalfDuration = firstHalfDurationOverride
            ?? (frameCount > 1 && framesPerSecond > 0d
                ? (float)(secondHalfFrame / framesPerSecond / Math.Max(0.05f, firstHalfSpeedScale))
                : fallbackFirstHalfDuration);

        await Cmd.CustomScaledWait(firstHalfDuration, firstHalfDuration);
        if (GodotObject.IsInstanceValid(sprite)
            && sprite.Animation == animation
            && frameCount > 1)
        {
            sprite.Frame = Math.Max(sprite.Frame, Math.Min(secondHalfFrame, frameCount - 1));
            sprite.SpeedScale = Math.Max(0.05f, secondHalfSpeedScale);
        }

        await onSecondHalf();
    }

    public static async Task PlayAcceleratedFollowupAnimation(Creature creature, float speedScale)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (creatureNode == null)
            return;

        foreach (var formAnimation in GetFormAnimations(creatureNode))
        {
            AnimatedSprite2D sprite = formAnimation.Sprite;
            if (!sprite.Visible || sprite.Modulate.A <= 0.01f)
                continue;

            NShinGetterSpriteAnimationStateMachine.QueueNextActionSpeed(sprite, speedScale);
            TryPlayVisibleActionAnimation(sprite, "Attack", formAnimation.EnsureLoaded);
            await Cmd.CustomScaledWait(0.12f, 0.18f);
            return;
        }
    }

    public static async Task PlayShinFormTransformVfx(Creature creature)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (creatureNode == null)
            return;

        var vfxContainer = NCombatRoom.Instance?.CombatVfxContainer;
        if (vfxContainer == null)
            return;

        Node2D rayWrap = new()
        {
            GlobalPosition = creatureNode.GlobalPosition + new Vector2(-16f, -205f),
            ZIndex = 80,
        };

        vfxContainer.AddChild(rayWrap);
        Color getterRay = new(0.29f, 1f, 0.77f, 0.96f);
        Color getterRayGlow = new(0.76f, 1f, 0.94f, 0.98f);
        for (int i = 0; i < 12; i++)
            CreateRisingRayRing(rayWrap, i, i % 3 == 0 ? getterRayGlow : getterRay);

        Sprite2D? silhouette = CreateGetterRaySilhouette(creatureNode, vfxContainer, getterRayGlow);
        if (silhouette != null)
        {
            Tween silhouetteTween = silhouette.CreateTween();
            silhouetteTween.TweenProperty(silhouette, "self_modulate:a", 0.96f, 0.22f).SetDelay(0.58f);
            silhouetteTween.TweenProperty(silhouette, "self_modulate:a", 0f, 0.24f).SetDelay(0.16f);
            silhouetteTween.TweenCallback(Callable.From(silhouette.QueueFree));
        }

        Tween cleanup = rayWrap.CreateTween();
        cleanup.TweenInterval(1.12f);
        cleanup.TweenProperty(rayWrap, "modulate:a", 0f, 0.16f);
        cleanup.TweenCallback(Callable.From(rayWrap.QueueFree));
        await Cmd.CustomScaledWait(0.74f, 0.82f);
    }

    /// <summary>
    /// Plays the current atomic form through fighter separation and recombination.
    /// Shin Getter Dragon uses an opacity tween because it has no fighter separation frames.
    /// </summary>
    public static async Task PlayOpenGetVfx(Creature creature)
    {
        if (!TryGetFormSprites(creature, out _, out FormSprites sprites))
            return;

        foreach (FormVisual formVisual in sprites.Atomic)
        {
            if (!formVisual.Item.Visible || formVisual.Item.Modulate.A <= 0.01f)
                continue;

            if (formVisual.Node is AnimatedSprite2D sprite
                && TryGetAtomicForm(formVisual, out ShinGetterForm form))
            {
                await PlayFusionAnimation(sprite, form, backwards: true, speedScale: 1f);
                await PlayFusionAnimation(sprite, form, backwards: false, speedScale: 1f);
                ActivateIdleAnimation(formVisual);
            }
            return;
        }

        FormVisual shinDragon = sprites.ShinDragon;
        if (!shinDragon.Item.Visible || shinDragon.Item.Modulate.A <= 0.01f)
            return;

        Tween tween = shinDragon.Item.CreateTween();
        tween.TweenProperty(shinDragon.Item, "modulate:a", ShinDragonOpenGetAlpha, 0.12f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(shinDragon.Item, "modulate:a", 1f, 0.18f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Sine);
        await shinDragon.Item.ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>
    /// Creates three or five synchronized afterimages above the active form for Shade.
    /// The source sprite itself continues playing normally underneath the VFX layer.
    /// </summary>
    public static Task PlayShadeVfx(Creature creature)
    {
        if (!TryGetFormSprites(creature, out _, out FormSprites sprites)
            || NCombatRoom.Instance?.CombatVfxContainer is not { } vfxContainer)
        {
            return Task.CompletedTask;
        }

        FormVisual? active = sprites.All
            .FirstOrDefault(sprite => sprite.Item.Visible && sprite.Item.Modulate.A > 0.01f);
        if (active == null || active.Value.Node is not AnimatedSprite2D source)
            return Task.CompletedTask;

        int ghostCount = Random.Shared.Next(0, 2) == 0 ? 3 : 5;
        List<AnimatedSprite2D> ghosts = new(ghostCount);
        for (int index = 0; index < ghostCount; index++)
        {
            float centered = index - (ghostCount - 1) / 2f;
            AnimatedSprite2D ghost = new()
            {
                SpriteFrames = source.SpriteFrames,
                Animation = source.Animation,
                Frame = source.Frame,
                SpeedScale = source.SpeedScale,
                Centered = source.Centered,
                Offset = source.Offset,
                ZIndex = 96 + index,
                SelfModulate = new Color(0.62f, 0.86f, 1f, 0f),
            };
            vfxContainer.AddChild(ghost);
            ghost.GlobalTransform = source.GlobalTransform;
            ghost.Play();
            ghosts.Add(ghost);

            Vector2 initialPosition = ghost.Position;
            Vector2 spreadPosition = initialPosition + Vector2.Right * centered * ShadeAfterimageSpacing;
            Tween tween = ghost.CreateTween();
            tween.TweenProperty(ghost, "self_modulate:a", 0.44f, 0.08f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Parallel().TweenProperty(ghost, "position", spreadPosition, 0.12f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Chain().TweenProperty(ghost, "self_modulate:a", 0.12f, 0.10f);
            tween.Parallel().TweenProperty(ghost, "position", initialPosition, 0.14f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
            tween.Chain().TweenProperty(ghost, "self_modulate:a", 0f, 0.10f);
            tween.TweenCallback(Callable.From(ghost.QueueFree));
        }

        return Task.CompletedTask;
    }

    private static void CreateRisingRayRing(Node2D parent, int index, Color color)
    {
        Line2D ring = new()
        {
            Width = 6f + index % 3 * 1.5f,
            DefaultColor = color,
            Antialiased = true,
            Position = new Vector2(0f, 188f),
            Scale = new Vector2(0.62f, 0.52f),
            Modulate = new Color(1f, 1f, 1f, 0f),
        };

        const int segments = 44;
        for (int point = 0; point <= segments; point++)
        {
            float angle = point / (float)segments * Mathf.Tau;
            float radiusWave = 1f + Mathf.Sin(angle * 3f + index * 0.65f) * 0.07f;
            ring.AddPoint(new Vector2(
                Mathf.Cos(angle) * 168f * radiusWave,
                Mathf.Sin(angle) * 42f));
        }
        parent.AddChild(ring);

        float delay = index * 0.045f;
        Tween movement = ring.CreateTween().SetParallel();
        movement.TweenProperty(ring, "position:y", -188f, 0.72f)
            .SetDelay(delay)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        movement.TweenProperty(ring, "scale", new Vector2(1.08f, 0.9f), 0.72f)
            .SetDelay(delay)
            .SetEase(Tween.EaseType.Out);
        movement.TweenProperty(ring, "modulate:a", 1f, 0.1f).SetDelay(delay);
        movement.TweenProperty(ring, "modulate:a", 0f, 0.18f).SetDelay(delay + 0.58f);
    }

    private static Sprite2D? CreateGetterRaySilhouette(
        NCreature creatureNode,
        Node vfxContainer,
        Color color)
    {
        foreach (var formAnimation in GetFormAnimations(creatureNode))
        {
            AnimatedSprite2D source = formAnimation.Sprite;
            if (!source.Visible || source.Modulate.A <= 0.01f)
                continue;

            Texture2D? texture = source.SpriteFrames?.GetFrameTexture(source.Animation, source.Frame);
            if (texture == null)
                return null;

            Sprite2D silhouette = new()
            {
                Texture = texture,
                Centered = source.Centered,
                Offset = source.Offset,
                FlipH = source.FlipH,
                FlipV = source.FlipV,
                ZIndex = 81,
                SelfModulate = new Color(color, 0f),
            };
            vfxContainer.AddChild(silhouette);
            silhouette.GlobalTransform = source.GlobalTransform;
            return silhouette;
        }

        return null;
    }

    private static bool TryGetFormSprites(
        Creature creature,
        out NCreatureVisuals visuals,
        out FormSprites sprites)
    {
        visuals = null!;
        sprites = default;

        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (creatureNode?.Visuals == null)
            return false;

        Node2D? getterOneNode = creatureNode.Visuals.GetNodeOrNull<Node2D>("Visuals/GetterOne");
        if (getterOneNode is AnimatedSprite2D getterOneAnimation)
        {
            NShinGetterSpriteSequence.EnsureIdleLoaded(getterOneAnimation);
            if (getterOneAnimation.Visible && !getterOneAnimation.IsPlaying())
                NShinGetterSpriteAnimationStateMachine.PlayIdle(getterOneAnimation);
        }

        Node2D? getterTwoNode = creatureNode.Visuals.GetNodeOrNull<Node2D>("Visuals/GetterTwo");
        if (getterTwoNode is AnimatedSprite2D getterTwoAnimation)
        {
            NShinGetterSpriteSequence.EnsureGetterTwoIdleLoaded(getterTwoAnimation);
            if (getterTwoAnimation.Visible && !getterTwoAnimation.IsPlaying())
                NShinGetterSpriteAnimationStateMachine.PlayIdle(getterTwoAnimation, NShinGetterSpriteSequence.EnsureGetterTwoIdleLoaded);
        }

        Node2D? getterThreeNode = creatureNode.Visuals.GetNodeOrNull<Node2D>("Visuals/GetterThree");
        if (getterThreeNode is AnimatedSprite2D getterThreeAnimation)
        {
            NShinGetterSpriteSequence.EnsureGetterThreeIdleLoaded(getterThreeAnimation);
            if (getterThreeAnimation.Visible && !getterThreeAnimation.IsPlaying())
                NShinGetterSpriteAnimationStateMachine.PlayIdle(getterThreeAnimation, NShinGetterSpriteSequence.EnsureGetterThreeIdleLoaded);
        }

        Node2D? shinDragonNode = creatureNode.Visuals.GetNodeOrNull<Node2D>("Visuals/ShinDragon");
        if (shinDragonNode is AnimatedSprite2D shinDragonAnimation)
        {
            NShinGetterSpriteSequence.EnsureShinDragonIdleLoaded(shinDragonAnimation);
            if (shinDragonAnimation.Visible && !shinDragonAnimation.IsPlaying())
                NShinGetterSpriteAnimationStateMachine.PlayIdle(shinDragonAnimation, NShinGetterSpriteSequence.EnsureShinDragonIdleLoaded);
        }

        var getterOne = ToFormVisual(getterOneNode);
        var getterTwo = ToFormVisual(getterTwoNode);
        var getterThree = ToFormVisual(getterThreeNode);
        var shinDragon = ToFormVisual(shinDragonNode);
        if (getterOne == null || getterTwo == null || getterThree == null || shinDragon == null)
            return false;

        visuals = creatureNode.Visuals;
        sprites = new FormSprites(getterOne.Value, getterTwo.Value, getterThree.Value, shinDragon.Value);
        return true;
    }

    private static FormVisual? ToFormVisual(Node2D? node) =>
        node is CanvasItem item ? new FormVisual(item, node) : null;

    private static bool TryPlayVisibleActionAnimation(
        AnimatedSprite2D animation,
        string trigger,
        System.Action<AnimatedSprite2D, string> ensureLoaded)
    {
        if (!animation.Visible || animation.Modulate.A <= 0.01f)
            return false;

        if (trigger == "HeavyAttack")
        {
            if (NShinGetterSpriteAnimationStateMachine.TryPlay(animation, trigger, ensureLoaded))
                return true;

            return NShinGetterSpriteAnimationStateMachine.TryPlay(animation, "Attack", ensureLoaded);
        }

        if (trigger == "Attack")
        {
            if (NShinGetterSpriteAnimationStateMachine.TryPlay(animation, trigger, ensureLoaded))
                return true;

            return NShinGetterSpriteAnimationStateMachine.TryPlay(animation, "Cast", ensureLoaded);
        }

        return NShinGetterSpriteAnimationStateMachine.TryPlay(animation, trigger, ensureLoaded);
    }

    private static bool TryPlayVisibleFormActionAnimation(NCreature creatureNode, string trigger)
    {
        foreach (var formAnimation in GetFormAnimations(creatureNode))
        {
            if (TryPlayVisibleActionAnimation(formAnimation.Sprite, trigger, formAnimation.EnsureLoaded))
                return true;
        }

        return false;
    }

    private static bool TryGetVisibleFormAnimation(NCreature creatureNode, out FormAnimation visibleAnimation)
    {
        foreach (FormAnimation formAnimation in GetFormAnimations(creatureNode))
        {
            if (!formAnimation.Sprite.Visible || formAnimation.Sprite.Modulate.A <= 0.01f)
                continue;

            visibleAnimation = formAnimation;
            return true;
        }

        visibleAnimation = default;
        return false;
    }

    private static IEnumerable<FormAnimation> GetFormAnimations(NCreature creatureNode)
    {
        if (creatureNode.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/GetterOne") is { } getterOne)
            yield return new FormAnimation(getterOne, NShinGetterSpriteSequence.EnsureLoaded);

        if (creatureNode.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/GetterTwo") is { } getterTwo)
            yield return new FormAnimation(getterTwo, NShinGetterSpriteSequence.EnsureGetterTwoLoaded);

        if (creatureNode.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/GetterThree") is { } getterThree)
            yield return new FormAnimation(getterThree, NShinGetterSpriteSequence.EnsureGetterThreeLoaded);

        if (creatureNode.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/ShinDragon") is { } shinDragon)
            yield return new FormAnimation(shinDragon, NShinGetterSpriteSequence.EnsureShinDragonLoaded);
    }

    private static async Task SwitchTo(
        NCreatureVisuals visuals,
        FormSprites sprites,
        FormVisual next,
        bool animate,
        float speedScale)
    {
        FormVisual? previous = null;
        foreach (var sprite in sprites.All)
        {
            if (sprite.Item != next.Item && sprite.Item.Visible && sprite.Item.Modulate.A > 0.01f)
                previous = sprite;
        }

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

        if (await TryPlayFusionTransition(sprites, previous, next, animationSpeed))
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

        Tween transformTween = visuals.CreateTween().SetParallel();
        Vector2 previousBaseScale = Vector2.One;
        if (previous != null)
        {
            FormVisual previousVisual = previous.Value;
            previousBaseScale = previousVisual.Node.Scale;
            transformTween.TweenProperty(previousVisual.Item, "modulate:a", 0f, 0.16f / animationSpeed)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
            transformTween.TweenProperty(previousVisual.Node, "scale", previousBaseScale * 1.18f, 0.16f / animationSpeed)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
        }

        transformTween.TweenProperty(next.Item, "modulate:a", 1f, 0.28f / animationSpeed)
            .SetDelay(0.06f / animationSpeed)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        transformTween.TweenProperty(next.Node, "scale", nextBaseScale, 0.28f / animationSpeed)
            .SetDelay(0.06f / animationSpeed)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        transformTween.TweenProperty(next.Node, "rotation_degrees", 0f, 0.28f / animationSpeed)
            .SetDelay(0.06f / animationSpeed)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        transformTween.TweenCallback(Callable.From(() => HideInactive(sprites, next, previous, previousBaseScale)))
            .SetDelay(0.36f / animationSpeed);
        await visuals.ToSignal(transformTween, Tween.SignalName.Finished);
    }

    private static async Task PlayOpeningGetterOneFusion(FormSprites sprites)
    {
        FormVisual next = sprites.GetterOne;
        AnimatedSprite2D? preparedSprite = next.Node as AnimatedSprite2D;
        int frameCount = 0;
        bool hasFusion = preparedSprite != null
            && TryPrepareFusionAnimation(preparedSprite, ShinGetterForm.Getter1, backwards: false, out frameCount);
        foreach (FormVisual sprite in sprites.All)
        {
            if (sprite.Item == next.Item)
                continue;

            sprite.Item.Visible = false;
            sprite.Item.Modulate = new Color(sprite.Item.Modulate, 0f);
            sprite.Node.RotationDegrees = 0f;
            if (sprite.Node is AnimatedSprite2D animation)
                NShinGetterSpriteSequence.ReleaseActionAnimations(animation);
        }

        next.Item.Visible = true;
        next.Item.Modulate = new Color(next.Item.Modulate, 1f);
        next.Node.RotationDegrees = 0f;
        if (hasFusion && preparedSprite != null)
        {
            await Cmd.CustomScaledWait(
                OpeningFusionFirstFrameHoldSeconds,
                OpeningFusionFirstFrameHoldSeconds);
            await PlayPreparedFusionAnimation(preparedSprite, frameCount, backwards: false, speedScale: 1f);
        }

        ActivateIdleAnimation(next);
    }

    private static async Task<bool> TryPlayFusionTransition(
        FormSprites sprites,
        FormVisual? previous,
        FormVisual next,
        float speedScale)
    {
        if (!TryGetAtomicForm(next, out ShinGetterForm nextForm)
            || next.Node is not AnimatedSprite2D nextSprite)
        {
            return false;
        }

        // Transitions to or from Shin Getter Dragon retain the existing non-fighter effect.
        if (previous is { } visiblePrevious && !TryGetAtomicForm(visiblePrevious, out _))
            return false;

        if (previous is { } previousVisual
            && TryGetAtomicForm(previousVisual, out ShinGetterForm previousForm)
            && previousVisual.Node is AnimatedSprite2D previousSprite)
        {
            await PlayFusionAnimation(previousSprite, previousForm, backwards: true, speedScale: speedScale);
            await Cmd.CustomScaledWait(FusionTransitionHoldSeconds, FusionTransitionHoldSeconds);
            previousVisual.Item.Visible = false;
            previousVisual.Item.Modulate = new Color(previousVisual.Item.Modulate, 0f);
            previousVisual.Node.RotationDegrees = 0f;
        }

        foreach (FormVisual sprite in sprites.All)
        {
            if (sprite.Item == next.Item)
                continue;

            sprite.Item.Visible = false;
            sprite.Item.Modulate = new Color(sprite.Item.Modulate, 0f);
            sprite.Node.RotationDegrees = 0f;
            if (sprite.Node is AnimatedSprite2D animation)
                NShinGetterSpriteSequence.ReleaseActionAnimations(animation);
        }

        next.Item.Visible = true;
        next.Item.Modulate = new Color(next.Item.Modulate, 1f);
        next.Node.RotationDegrees = 0f;
        await PlayFusionAnimation(nextSprite, nextForm, backwards: false, speedScale: speedScale);
        ActivateIdleAnimation(next);
        return true;
    }

    private static async Task PlayFusionAnimation(
        AnimatedSprite2D sprite,
        ShinGetterForm form,
        bool backwards,
        float speedScale)
    {
        if (!TryPrepareFusionAnimation(sprite, form, backwards, out int frameCount))
            return;

        await PlayPreparedFusionAnimation(sprite, frameCount, backwards, speedScale);
    }

    private static bool TryPrepareFusionAnimation(
        AnimatedSprite2D sprite,
        ShinGetterForm form,
        bool backwards,
        out int frameCount)
    {
        frameCount = 0;
        if (!NShinGetterSpriteSequence.EnsureFusionLoaded(sprite, form)
            || sprite.SpriteFrames is not { } frames)
        {
            return false;
        }

        StringName animation = NShinGetterSpriteSequence.FusionAnimationName;
        frameCount = frames.GetFrameCount(animation);
        if (frameCount <= 0)
            return false;

        sprite.Animation = animation;
        sprite.Frame = backwards ? frameCount - 1 : 0;
        sprite.SpeedScale = 1f;
        sprite.Stop();
        return true;
    }

    private static async Task PlayPreparedFusionAnimation(
        AnimatedSprite2D sprite,
        int frameCount,
        bool backwards,
        float speedScale)
    {
        StringName animation = NShinGetterSpriteSequence.FusionAnimationName;
        sprite.Play(animation);
        sprite.SpeedScale = backwards ? -Math.Max(0.05f, speedScale) : Math.Max(0.05f, speedScale);
        sprite.Frame = backwards ? frameCount - 1 : 0;
        double framesPerSecond = sprite.SpriteFrames?.GetAnimationSpeed(animation) ?? 0d;
        float duration = framesPerSecond > 0d
            ? (float)(frameCount / framesPerSecond / Math.Max(0.05f, speedScale))
            : 0.5f / Math.Max(0.05f, speedScale);
        await Cmd.CustomScaledWait(duration, duration);
        if (!GodotObject.IsInstanceValid(sprite))
            return;

        sprite.Stop();
        sprite.Frame = backwards ? 0 : frameCount - 1;
        sprite.SpeedScale = 1f;
    }

    private static void HideInactive(FormSprites sprites, FormVisual active, FormVisual? previous, Vector2 previousBaseScale)
    {
        foreach (var sprite in sprites.All)
        {
            if (sprite.Item == active.Item)
                continue;

            sprite.Item.Visible = false;
            sprite.Node.RotationDegrees = 0f;
            if (sprite.Node is AnimatedSprite2D animation)
                NShinGetterSpriteSequence.ReleaseActionAnimations(animation);
            if (previous != null && sprite.Item == previous.Value.Item)
                sprite.Node.Scale = previousBaseScale;
        }
    }

    private static void ActivateIdleAnimation(FormVisual visual)
    {
        if (visual.Node is not AnimatedSprite2D animation)
            return;

        if (animation.Name == "GetterTwo")
        {
            NShinGetterSpriteAnimationStateMachine.PlayIdle(animation, NShinGetterSpriteSequence.EnsureGetterTwoIdleLoaded);
            return;
        }

        if (animation.Name == "GetterThree")
        {
            NShinGetterSpriteAnimationStateMachine.PlayIdle(animation, NShinGetterSpriteSequence.EnsureGetterThreeIdleLoaded);
            return;
        }

        if (animation.Name == "ShinDragon")
        {
            NShinGetterSpriteAnimationStateMachine.PlayIdle(animation, NShinGetterSpriteSequence.EnsureShinDragonIdleLoaded);
            return;
        }

        NShinGetterSpriteAnimationStateMachine.PlayIdle(animation, NShinGetterSpriteSequence.EnsureIdleLoaded);
    }

    private readonly record struct FormSprites(
        FormVisual GetterOne,
        FormVisual GetterTwo,
        FormVisual GetterThree,
        FormVisual ShinDragon)
    {
        public FormVisual[] All => new[] { GetterOne, GetterTwo, GetterThree, ShinDragon };
        public FormVisual[] Atomic => new[] { GetterOne, GetterTwo, GetterThree };
    }

    private readonly record struct FormVisual(CanvasItem Item, Node2D Node);

    private static bool TryGetAtomicForm(FormVisual visual, out ShinGetterForm form)
    {
        string nodeName = visual.Node.Name.ToString();
        form = nodeName switch
        {
            "GetterOne" => ShinGetterForm.Getter1,
            "GetterTwo" => ShinGetterForm.Getter2,
            "GetterThree" => ShinGetterForm.Getter3,
            _ => ShinGetterForm.None,
        };
        return form != ShinGetterForm.None;
    }

    private readonly record struct FormAnimation(
        AnimatedSprite2D Sprite,
        Action<AnimatedSprite2D, string> EnsureLoaded);
}
