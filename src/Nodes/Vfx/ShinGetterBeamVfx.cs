#nullable enable
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

internal enum ShinGetterBeamStyle
{
    GetterBeam,
    FinalGetterBeam,
}

internal static class ShinGetterBeamVfx
{
    private static readonly Color GetterPink = new(1f, 0.18f, 0.58f, 1f);
    private static readonly Color GetterWhite = new(1f, 0.95f, 1f, 1f);
    private static readonly Color GetterRay = new(0.294f, 0.996f, 0.768f, 1f);

    public static async Task Play(Creature owner, IEnumerable<Creature> targets, ShinGetterBeamStyle style)
    {
        List<Creature> livingTargets = targets.Where(target => target.IsAlive).ToList();
        if (livingTargets.Count == 0)
            return;

        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        NCreature? mainTargetNode = NCombatRoom.Instance?.GetCreatureNode(livingTargets.Last());
        if (ownerNode == null || mainTargetNode == null)
            return;

        Vector2 source = ownerNode.VfxSpawnPosition + Vector2.Up * 32f;
        Vector2 target = mainTargetNode.VfxSpawnPosition;
        float chargeTime = style == ShinGetterBeamStyle.GetterBeam ? 0.14f : 0.38f;
        float beamTime = style == ShinGetterBeamStyle.GetterBeam ? 0.22f : 0.42f;

        Node2D root = new() { GlobalPosition = source };
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        AddCharge(root, style);
        await Cmd.Wait(chargeTime);

        Node2D beam = CreateBeam(source, target, style);
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(beam);

        foreach (Creature creature in livingTargets)
        {
            NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
            if (node != null)
                AddImpact(node.VfxSpawnPosition, style);
        }

        NGame.Instance?.ScreenShake(
            style == ShinGetterBeamStyle.GetterBeam ? ShakeStrength.Medium : ShakeStrength.Strong,
            ShakeDuration.Short);

        await Cmd.Wait(beamTime);
        root.QueueFreeSafely();
        beam.QueueFreeSafely();
    }

    private static void AddCharge(Node2D root, ShinGetterBeamStyle style)
    {
        Color color = style == ShinGetterBeamStyle.GetterBeam ? GetterPink : GetterRay;
        float radius = style == ShinGetterBeamStyle.GetterBeam ? 48f : 86f;
        Line2D ring = CreateCircle(radius, color, 7f, 0.8f);
        Line2D inner = CreateCircle(radius * 0.55f, GetterWhite, 4f, 0.5f);
        root.AddChild(ring);
        root.AddChild(inner);

        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 0.35f, style == ShinGetterBeamStyle.GetterBeam ? 0.12f : 0.34f)
            .From(Vector2.One * 1.35f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(root, "modulate:a", 0f, style == ShinGetterBeamStyle.GetterBeam ? 0.14f : 0.38f);
    }

    private static Node2D CreateBeam(Vector2 source, Vector2 target, ShinGetterBeamStyle style)
    {
        Node2D root = new();
        Vector2 direction = target - source;
        float length = direction.Length() + 260f;
        float angle = direction.Angle();
        root.GlobalPosition = source;
        root.Rotation = angle;

        if (style == ShinGetterBeamStyle.GetterBeam)
        {
            root.AddChild(CreateStraightLine(length, 78f, new Color(GetterPink.R, GetterPink.G, GetterPink.B, 0.78f), 0f));
            root.AddChild(CreateStraightLine(length, 30f, GetterWhite, 0f));
        }
        else
        {
            root.AddChild(CreateStraightLine(length, 210f, new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.78f), 0f));
            root.AddChild(CreateStraightLine(length, 86f, new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.72f), 0f));
            root.AddChild(CreateWrappedLine(length, -52f, 34f, GetterPink, 0f));
            root.AddChild(CreateWrappedLine(length, 52f, 34f, GetterPink, 1.7f));
            root.AddChild(CreateWrappedLine(length, 0f, 20f, new Color(GetterPink.R, GetterPink.G, GetterPink.B, 0.78f), 3.2f));
        }

        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "modulate:a", 0f, style == ShinGetterBeamStyle.GetterBeam ? 0.22f : 0.42f)
            .SetDelay(style == ShinGetterBeamStyle.GetterBeam ? 0.16f : 0.28f);
        return root;
    }

    private static Line2D CreateStraightLine(float length, float width, Color color, float y)
    {
        Line2D line = new()
        {
            Width = width,
            DefaultColor = color,
            Antialiased = true,
        };
        line.AddPoint(new Vector2(-30f, y));
        line.AddPoint(new Vector2(length, y));
        return line;
    }

    private static Line2D CreateWrappedLine(float length, float yOffset, float width, Color color, float phase)
    {
        Line2D line = new()
        {
            Width = width,
            DefaultColor = color,
            Antialiased = true,
        };
        for (int i = 0; i < 32; i++)
        {
            float t = i / 31f;
            float x = Mathf.Lerp(-20f, length, t);
            float y = yOffset * Mathf.Sin(t * Mathf.Tau * 2.5f + phase);
            line.AddPoint(new Vector2(x, y));
        }
        return line;
    }

    private static void AddImpact(Vector2 position, ShinGetterBeamStyle style)
    {
        Node2D root = new() { GlobalPosition = position };
        Color color = style == ShinGetterBeamStyle.GetterBeam ? GetterPink : GetterRay;
        float radius = style == ShinGetterBeamStyle.GetterBeam ? 68f : 120f;
        root.AddChild(CreateCircle(radius, color, 10f, 0.8f));
        root.AddChild(CreateCircle(radius * 0.5f, GetterWhite, 5f, 0.65f));
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.55f, 0.24f);
        tween.TweenProperty(root, "modulate:a", 0f, 0.24f);
        tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
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
        for (int i = 0; i < 48; i++)
            line.AddPoint(Vector2.Right.Rotated(Mathf.Tau * i / 48f) * radius);
        return line;
    }
}
