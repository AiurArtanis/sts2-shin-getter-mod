#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Nodes.Combat;

public static class NShinGetterStaticVisuals
{
    public static void ShowForm(Creature creature, ShinGetterForm form, bool animate = true)
    {
        if (!TryGetFormSprites(creature, out var visuals, out var sprites))
            return;

        FormVisual next = form switch
        {
            ShinGetterForm.Getter2 => sprites.GetterTwo,
            ShinGetterForm.Getter3 => sprites.GetterThree,
            _ => sprites.GetterOne,
        };

        SwitchTo(visuals, sprites, next, animate);
    }

    public static void ShowShinDragon(Creature creature, bool animate = true)
    {
        if (!TryGetFormSprites(creature, out var visuals, out var sprites))
            return;

        SwitchTo(visuals, sprites, sprites.ShinDragon, animate);
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

    public static void PlayShinFormTransformVfx(Creature creature)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (creatureNode == null)
            return;

        var vfxContainer = NCombatRoom.Instance?.CombatVfxContainer;
        if (vfxContainer == null)
            return;

        Node2D rayBurst = new()
        {
            GlobalPosition = creatureNode.GlobalPosition + new Vector2(24f, -205f),
            ZIndex = 80,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };

        Color getterRay = new(0.23f, 1f, 0.72f, 0.92f);
        for (int i = 0; i < 12; i++)
        {
            float y = -118f + i * 20f;
            Line2D line = new()
            {
                Width = 7f + i % 3,
                DefaultColor = getterRay,
                Antialiased = true,
            };
            line.AddPoint(new Vector2(-130f, y));
            line.AddPoint(new Vector2(-68f, y - 22f + i % 4 * 10f));
            line.AddPoint(new Vector2(0f, y + 18f - i % 5 * 7f));
            line.AddPoint(new Vector2(72f, y - 16f + i % 3 * 12f));
            line.AddPoint(new Vector2(138f, y + 4f));
            rayBurst.AddChild(line);
        }

        vfxContainer.AddChild(rayBurst);
        Tween tween = rayBurst.CreateTween().SetParallel();
        tween.TweenProperty(rayBurst, "modulate:a", 1f, 0.08f);
        tween.TweenProperty(rayBurst, "scale", new Vector2(1.24f, 1.08f), 0.34f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(rayBurst, "rotation_degrees", 6f, 0.34f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(rayBurst, "modulate:a", 0f, 0.18f)
            .SetDelay(0.22f)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(rayBurst.QueueFree)).SetDelay(0.42f);
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
            NShinGetterSpriteSequence.EnsureLoaded(getterOneAnimation);
            if (getterOneAnimation.Visible && !getterOneAnimation.IsPlaying())
                NShinGetterSpriteAnimationStateMachine.PlayIdle(getterOneAnimation);
        }

        Node2D? getterTwoNode = creatureNode.Visuals.GetNodeOrNull<Node2D>("Visuals/GetterTwo");
        if (getterTwoNode is AnimatedSprite2D getterTwoAnimation)
        {
            NShinGetterSpriteSequence.EnsureGetterTwoLoaded(getterTwoAnimation);
            if (getterTwoAnimation.Visible && !getterTwoAnimation.IsPlaying())
                NShinGetterSpriteAnimationStateMachine.PlayIdle(getterTwoAnimation, NShinGetterSpriteSequence.EnsureGetterTwoLoaded);
        }

        Node2D? getterThreeNode = creatureNode.Visuals.GetNodeOrNull<Node2D>("Visuals/GetterThree");
        if (getterThreeNode is AnimatedSprite2D getterThreeAnimation)
        {
            NShinGetterSpriteSequence.EnsureGetterThreeLoaded(getterThreeAnimation);
            if (getterThreeAnimation.Visible && !getterThreeAnimation.IsPlaying())
                NShinGetterSpriteAnimationStateMachine.PlayIdle(getterThreeAnimation, NShinGetterSpriteSequence.EnsureGetterThreeLoaded);
        }

        Node2D? shinDragonNode = creatureNode.Visuals.GetNodeOrNull<Node2D>("Visuals/ShinDragon");
        if (shinDragonNode is AnimatedSprite2D shinDragonAnimation)
        {
            NShinGetterSpriteSequence.EnsureShinDragonLoaded(shinDragonAnimation);
            if (shinDragonAnimation.Visible && !shinDragonAnimation.IsPlaying())
                NShinGetterSpriteAnimationStateMachine.PlayIdle(shinDragonAnimation, NShinGetterSpriteSequence.EnsureShinDragonLoaded);
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
        System.Action<AnimatedSprite2D> ensureLoaded)
    {
        if (!animation.Visible || animation.Modulate.A <= 0.01f)
            return false;

        if (trigger == "HeavyAttack")
        {
            if (NShinGetterSpriteAnimationStateMachine.TryPlay(animation, trigger, ensureLoaded))
                return true;

            return NShinGetterSpriteAnimationStateMachine.TryPlay(animation, "Attack", ensureLoaded);
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

    private static void SwitchTo(NCreatureVisuals visuals, FormSprites sprites, FormVisual next, bool animate)
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
            }
            ActivateIdleAnimation(next);
            return;
        }

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
            transformTween.TweenProperty(previousVisual.Item, "modulate:a", 0f, 0.16f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
            transformTween.TweenProperty(previousVisual.Node, "scale", previousBaseScale * 1.18f, 0.16f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Sine);
        }

        transformTween.TweenProperty(next.Item, "modulate:a", 1f, 0.28f)
            .SetDelay(0.06f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        transformTween.TweenProperty(next.Node, "scale", nextBaseScale, 0.28f)
            .SetDelay(0.06f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        transformTween.TweenProperty(next.Node, "rotation_degrees", 0f, 0.28f)
            .SetDelay(0.06f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
        transformTween.TweenCallback(Callable.From(() => HideInactive(sprites, next, previous, previousBaseScale))).SetDelay(0.36f);
    }

    private static void HideInactive(FormSprites sprites, FormVisual active, FormVisual? previous, Vector2 previousBaseScale)
    {
        foreach (var sprite in sprites.All)
        {
            if (sprite.Item == active.Item)
                continue;

            sprite.Item.Visible = false;
            sprite.Node.RotationDegrees = 0f;
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
            NShinGetterSpriteAnimationStateMachine.PlayIdle(animation, NShinGetterSpriteSequence.EnsureGetterTwoLoaded);
            return;
        }

        if (animation.Name == "GetterThree")
        {
            NShinGetterSpriteAnimationStateMachine.PlayIdle(animation, NShinGetterSpriteSequence.EnsureGetterThreeLoaded);
            return;
        }

        if (animation.Name == "ShinDragon")
        {
            NShinGetterSpriteAnimationStateMachine.PlayIdle(animation, NShinGetterSpriteSequence.EnsureShinDragonLoaded);
            return;
        }

        NShinGetterSpriteAnimationStateMachine.PlayIdle(animation, NShinGetterSpriteSequence.EnsureLoaded);
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

    private readonly record struct FormAnimation(AnimatedSprite2D Sprite, Action<AnimatedSprite2D> EnsureLoaded);
}
