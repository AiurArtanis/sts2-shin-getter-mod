#nullable enable
using System;
using System.Collections.Generic;
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
        if (next.Item.Visible && next.Item.Modulate.A > 0.99f)
        {
            ActivateIdleAnimation(next);
            return;
        }

        FormVisual? previous = null;
        foreach (var sprite in sprites.All)
        {
            if (sprite.Item != next.Item && sprite.Item.Visible && sprite.Item.Modulate.A > 0.01f)
                previous = sprite;
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
    }

    private readonly record struct FormVisual(CanvasItem Item, Node2D Node);

    private readonly record struct FormAnimation(
        AnimatedSprite2D Sprite,
        Action<AnimatedSprite2D, string> EnsureLoaded);
}
