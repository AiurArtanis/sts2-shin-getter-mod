#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace ShinGetterMod.Nodes.Vfx;

internal static class ShinGetterCombatVfx
{
    private static readonly Color GetterRay = new(0.294f, 0.996f, 0.768f, 1f);
    private static readonly Color GetterPink = new(1f, 0.18f, 0.58f, 1f);
    private static readonly Color GetterWhite = new(1f, 0.95f, 1f, 1f);
    private static readonly Color KiYellow = new(1f, 0.88f, 0.22f, 1f);
    private static readonly Color HotBloodOrange = new(1f, 0.36f, 0.08f, 1f);
    private static readonly Color SpiritGold = new(1f, 0.72f, 0.02f, 1f);
    private static readonly Color WhiteFlash = new(1f, 1f, 0.92f, 1f);

    public static Task PlayKiAura(Creature creature) => PlayAura(creature, KiYellow, 0.38f, 120f);

    public static Task PlayHotBloodAura(Creature creature) => PlayAura(creature, HotBloodOrange, 0.45f, 135f);

    public static Task PlaySpiritAura(Creature creature) => PlayAura(creature, SpiritGold, 0.5f, 150f);

    public static async Task PlayRush(Creature owner, Creature target, bool whiteFlash = false)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (ownerNode == null || targetNode == null)
            return;

        Vector2 origin = ownerNode.GlobalPosition;
        Vector2 ownerCenter = ownerNode.VfxSpawnPosition;
        Vector2 targetCenter = targetNode.VfxSpawnPosition;
        Vector2 direction = (targetCenter - ownerCenter).Normalized();
        Vector2 lunge = direction * Math.Max(0f, ownerCenter.DistanceTo(targetCenter) - 110f);

        if (whiteFlash)
            AddFlash(ownerCenter, WhiteFlash, 190f, 0.22f);

        AddSpeedLines(ownerCenter, targetCenter, whiteFlash ? WhiteFlash : GetterRay);

        Tween tween = ownerNode.CreateTween();
        tween.TweenProperty(ownerNode, "global_position", origin + lunge, 0.07f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ownerNode, "global_position", origin, 0.12f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
        await Cmd.Wait(0.21f);
    }

    public static async Task PlayExpansion(Creature creature)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (node == null)
            return;

        AddFlash(node.VfxSpawnPosition, GetterRay, 150f, 0.28f);
        node.ScaleTo(1.5f, 0.12);
        await Cmd.Wait(0.16f);
        node.ScaleTo(1f, 0.14);
        await Cmd.Wait(0.06f);
    }

    public static async Task PlayAvalanche(Creature target)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(target);
        if (node == null)
            return;

        Vector2 origin = node.GlobalPosition;
        AddSpiral(node.VfxSpawnPosition, GetterRay);

        Tween tween = node.CreateTween();
        tween.TweenProperty(node, "global_position", origin + Vector2.Up * 155f, 0.18f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(node, "global_position", origin, 0.13f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        await Cmd.Wait(0.32f);
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
    }

    public static async Task PlayMissile(Creature owner, Creature target, int index)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (ownerNode == null || targetNode == null)
            return;

        Vector2 source = ownerNode.VfxSpawnPosition + new Vector2(0f, -20f + index * 12f);
        Vector2 destination = targetNode.VfxSpawnPosition;
        Node2D missile = CreateMissile(source, destination);
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(missile);

        Tween tween = missile.CreateTween();
        Vector2 arc = source.Lerp(destination, 0.45f) + Vector2.Up * (90f + index * 15f);
        tween.TweenProperty(missile, "global_position", arc, 0.09f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(missile, "global_position", destination, 0.11f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
        tween.TweenCallback(Callable.From(missile.QueueFreeSafely));
        await Cmd.Wait(0.2f);
        AddFlash(destination, GetterPink, 70f, 0.16f);
    }

    public static async Task PlayThunderField(Creature owner, IEnumerable<Creature> targets)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (ownerNode == null)
            return;

        Node2D root = new();
        Vector2 source = ownerNode.VfxSpawnPosition + Vector2.Up * 15f;
        foreach (Creature target in targets.Where(target => target.IsAlive))
        {
            NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
            if (targetNode == null)
                continue;

            root.AddChild(CreateCrackLine(source, targetNode.VfxSpawnPosition, GetterRay));
            root.AddChild(CreateCrackLine(source + Vector2.Up * 26f, targetNode.VfxSpawnPosition + Vector2.Right * 34f, GetterWhite));
        }

        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        Tween tween = root.CreateTween();
        tween.TweenProperty(root, "modulate:a", 0f, 0.32f).SetDelay(0.12f);
        tween.TweenCallback(Callable.From(root.QueueFreeSafely));
        NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
        await Cmd.Wait(0.18f);
    }

    public static async Task PlayEnergyBall(Creature owner, IEnumerable<Creature> targets)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        List<Vector2> targetPositions = targets
            .Where(target => target.IsAlive)
            .Select(target => NCombatRoom.Instance?.GetCreatureNode(target)?.VfxSpawnPosition)
            .OfType<Vector2>()
            .ToList();
        if (ownerNode == null || targetPositions.Count == 0)
            return;

        Vector2 source = ownerNode.VfxSpawnPosition + Vector2.Up * 185f;
        Vector2 destination = targetPositions.Aggregate(Vector2.Zero, (sum, pos) => sum + pos) / targetPositions.Count;
        Node2D ball = new() { GlobalPosition = source };
        ball.AddChild(CreateCircle(88f, GetterRay, 18f, 0.85f));
        ball.AddChild(CreateCircle(48f, GetterWhite, 10f, 0.7f));
        ball.AddChild(CreateCircle(118f, GetterPink, 7f, 0.42f));
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(ball);

        Tween tween = ball.CreateTween();
        tween.TweenProperty(ball, "scale", Vector2.One * 1.45f, 0.26f).From(Vector2.One * 0.2f).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(ball, "global_position", destination, 0.22f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenCallback(Callable.From(ball.QueueFreeSafely));
        await Cmd.Wait(0.5f);

        foreach (Vector2 pos in targetPositions)
            AddFlash(pos, GetterRay, 130f, 0.22f);
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
    }

    public static Task PlayWhiteFlash(Creature creature)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (node != null)
            AddFlash(node.VfxSpawnPosition, WhiteFlash, 240f, 0.24f);
        return Cmd.Wait(0.08f);
    }

    public static async Task PlayTomahawk(Creature owner, Creature target)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (ownerNode == null || targetNode == null)
            return;

        Vector2 source = ownerNode.VfxSpawnPosition + Vector2.Up * 20f;
        Vector2 destination = targetNode.VfxSpawnPosition;
        Node2D axe = CreateTomahawkNode();
        axe.GlobalPosition = source;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(axe);

        Tween tween = axe.CreateTween().SetParallel();
        tween.TweenProperty(axe, "global_position", destination, 0.18f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(axe, "rotation", Mathf.Tau * 2.4f, 0.18f);
        tween.Chain().TweenCallback(Callable.From(axe.QueueFreeSafely));
        await Cmd.Wait(0.18f);
        AddFlash(destination, GetterRay, 80f, 0.16f);
    }

    public static Node2D CreateGetterSlashImpact()
    {
        Node2D root = new();
        Line2D first = CreateSlashLine(new Vector2(-80f, 56f), new Vector2(92f, -58f), GetterRay, 13f);
        Line2D second = CreateSlashLine(new Vector2(-58f, -50f), new Vector2(78f, 48f), GetterPink, 7f);
        root.AddChild(first);
        root.AddChild(second);
        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.25f, 0.2f);
        tween.TweenProperty(root, "modulate:a", 0f, 0.2f).SetDelay(0.1f);
        tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
        return root;
    }

    private static async Task PlayAura(Creature creature, Color color, float duration, float radius)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (node == null)
            return;

        Node2D root = new() { GlobalPosition = node.VfxSpawnPosition };
        root.AddChild(CreateCircle(radius, color, 8f, 0.8f));
        root.AddChild(CreateCircle(radius * 0.68f, new Color(1f, 1f, 1f, 0.75f), 4f, 0.45f));

        for (int i = 0; i < 10; i++)
            root.AddChild(CreateRay(i, radius, color));

        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.35f, duration);
        tween.TweenProperty(root, "modulate:a", 0f, duration).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
        await Cmd.Wait(duration);
    }

    private static Line2D CreateCircle(float radius, Color color, float width, float alpha)
    {
        Line2D line = new()
        {
            Width = width,
            DefaultColor = new Color(color.R, color.G, color.B, alpha),
            Closed = true,
            Antialiased = true,
        };
        foreach (Vector2 point in CirclePoints(radius, 48))
            line.AddPoint(point);
        return line;
    }

    private static Line2D CreateRay(int index, float radius, Color color)
    {
        float angle = Mathf.Tau * index / 10f;
        Vector2 dir = Vector2.Right.Rotated(angle);
        Line2D ray = new()
        {
            Width = 5f,
            DefaultColor = new Color(color.R, color.G, color.B, 0.62f),
            Antialiased = true,
        };
        ray.AddPoint(dir * radius * 0.2f);
        ray.AddPoint(dir * radius * 1.15f);
        return ray;
    }

    private static void AddSpeedLines(Vector2 source, Vector2 target, Color color)
    {
        Node2D root = new();
        Vector2 dir = (target - source).Normalized();
        Vector2 normal = new(-dir.Y, dir.X);
        for (int i = -2; i <= 2; i++)
        {
            Line2D line = new()
            {
                Width = 9f - Math.Abs(i) * 1.2f,
                DefaultColor = new Color(color.R, color.G, color.B, 0.62f),
                Antialiased = true,
            };
            Vector2 offset = normal * i * 28f;
            line.AddPoint(source + offset);
            line.AddPoint(target + offset - dir * 55f);
            root.AddChild(line);
        }
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        Tween tween = root.CreateTween();
        tween.TweenProperty(root, "modulate:a", 0f, 0.18f);
        tween.TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    private static void AddFlash(Vector2 position, Color color, float radius, float duration)
    {
        Node2D root = new() { GlobalPosition = position };
        root.AddChild(CreateCircle(radius, color, 12f, 0.8f));
        root.AddChild(CreateCircle(radius * 0.55f, WhiteFlash, 6f, 0.55f));
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.5f, duration);
        tween.TweenProperty(root, "modulate:a", 0f, duration);
        tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    private static void AddSpiral(Vector2 position, Color color)
    {
        Line2D spiral = new()
        {
            GlobalPosition = position,
            Width = 7f,
            DefaultColor = new Color(color.R, color.G, color.B, 0.75f),
            Antialiased = true,
        };
        for (int i = 0; i < 54; i++)
        {
            float t = i / 53f;
            float angle = t * Mathf.Tau * 2.8f;
            spiral.AddPoint(Vector2.Right.Rotated(angle) * Mathf.Lerp(20f, 130f, t) + Vector2.Up * Mathf.Lerp(60f, -170f, t));
        }
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(spiral);
        Tween tween = spiral.CreateTween();
        tween.TweenProperty(spiral, "modulate:a", 0f, 0.38f);
        tween.TweenCallback(Callable.From(spiral.QueueFreeSafely));
    }

    private static Node2D CreateMissile(Vector2 source, Vector2 destination)
    {
        Vector2 direction = (destination - source).Normalized();
        Node2D root = new()
        {
            GlobalPosition = source,
            Rotation = direction.Angle(),
        };
        Polygon2D body = new()
        {
            Polygon = new[]
            {
                new Vector2(24f, 0f),
                new Vector2(-16f, -9f),
                new Vector2(-8f, 0f),
                new Vector2(-16f, 9f),
            },
            Color = GetterPink,
        };
        Line2D trail = new()
        {
            Width = 10f,
            DefaultColor = new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.65f),
            Antialiased = true,
        };
        trail.AddPoint(new Vector2(-18f, 0f));
        trail.AddPoint(new Vector2(-70f, 0f));
        root.AddChild(trail);
        root.AddChild(body);
        return root;
    }

    private static Line2D CreateCrackLine(Vector2 from, Vector2 to, Color color)
    {
        Line2D line = new()
        {
            Width = 8f,
            DefaultColor = new Color(color.R, color.G, color.B, 0.75f),
            Antialiased = true,
        };
        Vector2 delta = to - from;
        Vector2 normal = new Vector2(-delta.Y, delta.X).Normalized();
        line.AddPoint(from);
        for (int i = 1; i < 6; i++)
        {
            float t = i / 6f;
            float zigzag = (i % 2 == 0 ? -1f : 1f) * 24f;
            line.AddPoint(from.Lerp(to, t) + normal * zigzag);
        }
        line.AddPoint(to);
        return line;
    }

    private static Node2D CreateTomahawkNode()
    {
        Node2D root = new();
        root.AddChild(CreateSlashLine(new Vector2(-42f, 0f), new Vector2(42f, 0f), GetterRay, 10f));
        root.AddChild(CreateSlashLine(new Vector2(0f, -34f), new Vector2(0f, 34f), GetterPink, 8f));
        root.AddChild(CreateCircle(22f, GetterRay, 4f, 0.68f));
        return root;
    }

    private static Line2D CreateSlashLine(Vector2 from, Vector2 to, Color color, float width)
    {
        Line2D line = new()
        {
            Width = width,
            DefaultColor = color,
            Antialiased = true,
        };
        line.AddPoint(from);
        line.AddPoint(to);
        return line;
    }

    private static IEnumerable<Vector2> CirclePoints(float radius, int count)
    {
        for (int i = 0; i < count; i++)
            yield return Vector2.Right.Rotated(Mathf.Tau * i / count) * radius;
    }
}
