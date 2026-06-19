#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;

namespace ShinGetterMod.Nodes.Vfx;

internal static partial class ShinGetterCombatVfx
{
    private const string VineShamblerVinesScenePath = "res://scenes/vfx/monsters/vine_shambler_vines/vine_shambler_vines_vfx.tscn";

    public static async Task PlayDiveStrike(Creature owner, Creature target)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (ownerNode == null || targetNode == null)
            return;

        Vector2 origin = ownerNode.GlobalPosition;
        Vector2 ownerCenter = ownerNode.VfxSpawnPosition;
        Vector2 targetCenter = targetNode.VfxSpawnPosition;
        Vector2 direction = (targetCenter - ownerCenter).Normalized();
        if (direction == Vector2.Zero)
            direction = owner.IsEnemy ? Vector2.Left : Vector2.Right;
        Vector2 apex = origin + Vector2.Up * 180f;
        Vector2 hitPosition = origin + (targetCenter - ownerCenter) - direction * 92f + Vector2.Up * 22f;

        AddDiveTrail(ownerCenter, targetCenter, GetterRay);
        Tween tween = ownerNode.CreateTween();
        tween.TweenProperty(ownerNode, "global_position", apex, 0.14f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ownerNode, "global_position", hitPosition, 0.09f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        await Cmd.Wait(0.24f);
        AddFlash(targetCenter, GetterRay, 130f, 0.18f);
        target.GetVfxContainer()?.AddChildSafely(NLineBurstVfx.Create(target));
        NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
        Tween returnTween = ownerNode.CreateTween();
        returnTween.TweenProperty(ownerNode, "global_position", origin, 0.13f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await Cmd.Wait(0.14f);
    }

    public static async Task PlayFlashRush(Creature owner, Creature target)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (ownerNode == null || targetNode == null)
            return;

        Vector2 origin = ownerNode.GlobalPosition;
        Vector2 ownerCenter = ownerNode.VfxSpawnPosition;
        Vector2 targetCenter = targetNode.VfxSpawnPosition;
        Vector2 direction = (targetCenter - ownerCenter).Normalized();
        if (direction == Vector2.Zero)
            direction = owner.IsEnemy ? Vector2.Left : Vector2.Right;
        Vector2 lunge = direction * Math.Max(0f, ownerCenter.DistanceTo(targetCenter) - 82f);

        AddAfterimageLines(ownerCenter, targetCenter, WhiteFlash, 8, 22f);
        Tween tween = ownerNode.CreateTween();
        tween.TweenProperty(ownerNode, "global_position", origin + lunge, 0.06f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ownerNode, "global_position", origin, 0.12f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await Cmd.Wait(0.19f);
        AddFlash(targetCenter, WhiteFlash, 210f, 0.18f);
        target.GetVfxContainer()?.AddChildSafely(NBigSlashImpactVfx.Create(targetCenter, 0f, GetterRay));
        NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
        await Cmd.Wait(0.04f);
    }

    public static async Task PlayTacticalRetreat(Creature owner)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (ownerNode == null)
            return;

        Vector2 origin = ownerNode.GlobalPosition;
        Vector2 retreatDirection = owner.IsEnemy ? Vector2.Right : Vector2.Left;
        Vector2 offscreen = origin + retreatDirection * 980f;
        AddAfterimageLines(ownerNode.VfxSpawnPosition, ownerNode.VfxSpawnPosition + retreatDirection * 520f, RushLine, 6, 18f);

        Tween tween = ownerNode.CreateTween();
        tween.TweenProperty(ownerNode, "global_position", offscreen, 0.16f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenInterval(0.10f);
        tween.TweenProperty(ownerNode, "global_position", origin, 0.10f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        await Cmd.Wait(0.38f);
    }

    public static async Task PlayDaggerSpray(Creature owner, IEnumerable<Creature> targets)
    {
        List<Creature> livingTargets = targets.Where(target => target.IsAlive).ToList();
        if (livingTargets.Count == 0)
            return;

        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NDaggerSprayFlurryVfx.Create(owner, GetterRay, goingRight: !owner.IsEnemy));
        await Cmd.Wait(0.08f);
        foreach (Creature target in livingTargets)
            NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NDaggerSprayImpactVfx.Create(target, GetterRay, goingRight: !owner.IsEnemy));
        await Cmd.Wait(0.18f);
    }

    public static async Task PlayAnnihilation(Creature owner, IEnumerable<Creature> targets)
    {
        List<Vector2> targetPositions = targets
            .Where(target => target.IsAlive)
            .Select(target => NCombatRoom.Instance?.GetCreatureNode(target)?.VfxSpawnPosition)
            .OfType<Vector2>()
            .ToList();
        if (targetPositions.Count == 0)
            return;

        Vector2 center = targetPositions.Aggregate(Vector2.Zero, (sum, pos) => sum + pos) / targetPositions.Count;
        Node2D root = CreateAnnihilationExplosion();
        root.GlobalPosition = center;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NGaseousImpactVfx.Create(center, new Color("#402f45")));

        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * 1.38f, 0.34f).From(Vector2.One * 0.18f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tween.TweenProperty(root, "modulate:a", 0f, 0.52f).SetDelay(0.16f).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(root.QueueFreeSafely));
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Normal);
        await Cmd.Wait(0.38f);
    }

    public static async Task PlayGrappleVines(Creature target)
    {
        if (TestMode.IsOn)
            return;

        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (targetNode == null)
            return;

        var vines = PreloadManager.Cache.GetScene(VineShamblerVinesScenePath).Instantiate<NVineShamblerVinesVfx>(PackedScene.GenEditState.Disabled);
        vines.GlobalPosition = targetNode.GetBottomOfHitbox();
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(vines);
        await Cmd.Wait(0.45f);
    }

    public static async Task PlayGetterNova(Creature owner, IEnumerable<Creature> targets)
    {
        List<Vector2> targetPositions = targets
            .Where(target => target.IsAlive)
            .Select(target => NCombatRoom.Instance?.GetCreatureNode(target)?.VfxSpawnPosition)
            .OfType<Vector2>()
            .ToList();
        if (targetPositions.Count == 0)
            return;

        Vector2 center = targetPositions.Aggregate(Vector2.Zero, (sum, pos) => sum + pos) / targetPositions.Count;
        Node2D nova = CreateGetterNovaNode();
        nova.GlobalPosition = center;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(nova);
        Tween tween = nova.CreateTween().SetParallel();
        tween.TweenProperty(nova, "scale", Vector2.One * 1.65f, 0.72f).From(Vector2.One * 0.12f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(nova, "modulate:a", 0f, 0.46f).SetDelay(0.28f).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(nova.QueueFreeSafely));
        NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Normal);
        await Cmd.Wait(0.66f);
    }

    public static async Task PlayNewtypeFlash(Creature owner)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (node == null)
            return;

        Node2D sign = CreateNewtypeSign();
        sign.GlobalPosition = node.VfxSpawnPosition + Vector2.Up * 128f;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(sign);
        Tween tween = sign.CreateTween().SetParallel();
        tween.TweenProperty(sign, "scale", Vector2.One * 1.45f, 0.38f).From(Vector2.One * 0.35f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(sign, "modulate:a", 0f, 0.38f).SetDelay(0.20f);
        tween.Chain().TweenCallback(Callable.From(sign.QueueFreeSafely));
        await Cmd.Wait(0.40f);
    }

    public static async Task PlayInsectVirusNightmare(Creature owner)
    {
        if (TestMode.IsOn)
            return;

        Control? globalUi = NGame.Instance?.CurrentRunNode?.GlobalUi;
        if (globalUi == null)
            return;

        globalUi.AddChildSafely(NSmokyVignetteVfx.Create(new Color(0f, 0f, 0f, 0.78f), new Color(0.02f, 0f, 0.04f, 0.45f)));
        NNightmareHandsVfx? hands = NNightmareHandsVfx.Create();
        if (hands != null)
        {
            hands.Modulate = new Color(0f, 0f, 0f, 0.92f);
            globalUi.AddChildSafely(hands);
        }
        await Cmd.CustomScaledWait(0.18f, 0.36f);
    }

    private static void AddDiveTrail(Vector2 source, Vector2 target, Color color)
    {
        Node2D root = new();
        Vector2 dir = (target - source).Normalized();
        if (dir == Vector2.Zero)
            dir = Vector2.Right;
        Vector2 normal = new(-dir.Y, dir.X);
        for (int i = -3; i <= 3; i++)
        {
            Line2D line = new()
            {
                Width = 6f + (3 - Math.Abs(i)) * 1.4f,
                DefaultColor = new Color(color.R, color.G, color.B, 0.62f),
                Antialiased = true,
            };
            Vector2 offset = normal * i * 22f + Vector2.Up * 90f;
            line.AddPoint(source + offset);
            line.AddPoint(target + offset * 0.18f);
            root.AddChild(line);
        }
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        Tween tween = root.CreateTween();
        tween.TweenProperty(root, "modulate:a", 0f, 0.24f);
        tween.TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    private static void AddAfterimageLines(Vector2 source, Vector2 target, Color color, int count, float spacing)
    {
        Node2D root = new();
        Vector2 dir = (target - source).Normalized();
        if (dir == Vector2.Zero)
            dir = Vector2.Right;
        Vector2 normal = new(-dir.Y, dir.X);
        for (int i = 0; i < count; i++)
        {
            float centered = i - (count - 1) * 0.5f;
            Line2D line = new()
            {
                Width = 10f - Math.Abs(centered) * 0.85f,
                DefaultColor = new Color(color.R, color.G, color.B, 0.52f),
                Antialiased = true,
            };
            Vector2 offset = normal * centered * spacing;
            line.AddPoint(source + offset - dir * 32f);
            line.AddPoint(target + offset - dir * 70f);
            root.AddChild(line);
        }
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        Tween tween = root.CreateTween();
        tween.TweenProperty(root, "modulate:a", 0f, 0.20f);
        tween.TweenCallback(Callable.From(root.QueueFreeSafely));
    }

    private static Node2D CreateAnnihilationExplosion()
    {
        Node2D root = new();
        root.AddChild(CreateFilledCircle(190f, new Color(0.18f, 0.12f, 0.22f, 0.30f)));
        root.AddChild(CreateFilledCircle(120f, new Color(SolarOrange.R, SolarOrange.G, SolarOrange.B, 0.34f)));
        root.AddChild(CreateFilledCircle(64f, new Color(SolarCore.R, SolarCore.G, SolarCore.B, 0.88f)));
        for (int i = 0; i < 24; i++)
            root.AddChild(CreateExplosionShard(i, 24));
        for (int i = 0; i < 20; i++)
            root.AddChild(CreateSmokePuff(i, 20));
        root.AddChild(CreateCircle(170f, SolarOrange, 16f, 0.72f));
        root.AddChild(CreateCircle(94f, SolarCore, 9f, 0.64f));
        return root;
    }

    private static Polygon2D CreateExplosionShard(int index, int count)
    {
        float angle = Mathf.Tau * index / count;
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);
        float inner = 34f + (index % 4) * 8f;
        float outer = 172f + (index % 5) * 18f;
        float width = 14f + (index % 3) * 4f;
        Polygon2D shard = new()
        {
            Color = index % 2 == 0 ? new Color(SolarGold.R, SolarGold.G, SolarGold.B, 0.72f) : new Color(SolarOrange.R, SolarOrange.G, SolarOrange.B, 0.66f),
            Antialiased = true,
        };
        shard.Polygon = new[]
        {
            dir * inner - tangent * width,
            dir * outer,
            dir * inner + tangent * width,
        };
        return shard;
    }

    private static Polygon2D CreateSmokePuff(int index, int count)
    {
        float angle = Mathf.Tau * index / count + 0.13f * (index % 4);
        float radius = 118f + (index % 6) * 14f;
        Polygon2D puff = CreateFilledCircle(22f + (index % 5) * 5f, new Color(0.13f, 0.10f, 0.15f, 0.34f));
        puff.Position = Vector2.Right.Rotated(angle) * radius;
        return puff;
    }

    private static Node2D CreateGetterNovaNode()
    {
        Node2D root = new();
        root.AddChild(CreateFilledCircle(132f, new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.22f)));
        root.AddChild(CreateFilledCircle(72f, new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.78f)));
        root.AddChild(CreateCircle(148f, GetterRay, 12f, 0.70f));
        root.AddChild(CreateCircle(88f, GetterWhite, 7f, 0.58f));
        for (int i = 0; i < 32; i++)
        {
            float angle = Mathf.Tau * i / 32f;
            Vector2 dir = Vector2.Right.Rotated(angle);
            Line2D ray = new()
            {
                Width = 5f + (i % 4),
                DefaultColor = i % 2 == 0 ? new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.72f) : new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.55f),
                Antialiased = true,
            };
            ray.AddPoint(dir * 74f);
            ray.AddPoint(dir * (230f + (i % 5) * 18f));
            root.AddChild(ray);
        }
        return root;
    }

    private static Node2D CreateNewtypeSign()
    {
        Node2D root = new();
        Color gold = new(1f, 0.84f, 0.12f, 1f);
        Color white = new(1f, 1f, 0.82f, 1f);
        root.AddChild(CreateSlashLine(new Vector2(-54f, -22f), new Vector2(0f, -68f), gold, 8f));
        root.AddChild(CreateSlashLine(new Vector2(0f, -68f), new Vector2(54f, -22f), gold, 8f));
        root.AddChild(CreateSlashLine(new Vector2(-38f, -4f), new Vector2(0f, -38f), white, 4f));
        root.AddChild(CreateSlashLine(new Vector2(0f, -38f), new Vector2(38f, -4f), white, 4f));
        root.AddChild(CreateCircle(46f, gold, 4f, 0.58f));
        for (int i = 0; i < 8; i++)
        {
            Vector2 dir = Vector2.Right.Rotated(Mathf.Tau * i / 8f);
            root.AddChild(CreateSlashLine(dir * 54f, dir * 84f, i % 2 == 0 ? gold : white, 4f));
        }
        return root;
    }
}

