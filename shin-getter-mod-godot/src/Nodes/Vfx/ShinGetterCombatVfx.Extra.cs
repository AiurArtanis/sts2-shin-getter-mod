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
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Nodes.Vfx;

internal static partial class ShinGetterCombatVfx
{
    private const string VineShamblerVinesScenePath = "res://scenes/vfx/monsters/vine_shambler_vines/vine_shambler_vines_vfx.tscn";
    private const string AwakenedSoulFlashTexturePath = "res://images/powers/s_g_p_awakened_soul.png";

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
        Vector2 apex = origin + Vector2.Up * 300f + direction * 24f;
        Vector2 hitPosition = origin + (targetCenter - ownerCenter) - direction * 92f + Vector2.Up * 22f;

        Tween ascentTween = ownerNode.CreateTween();
        ascentTween.TweenProperty(ownerNode, "global_position", apex, 0.56f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        await NShinGetterStaticVisuals.PlayPhasedCreatureActionAnimation(
            owner,
            "Attack",
            1f,
            2f,
            async () =>
            {
                AddDiveTrail(ownerCenter, targetCenter, GetterRay);
                Tween diveTween = ownerNode.CreateTween();
                diveTween.TweenProperty(ownerNode, "global_position", hitPosition, 0.10f)
                    .SetEase(Tween.EaseType.In)
                    .SetTrans(Tween.TransitionType.Cubic);
                await Cmd.Wait(0.11f);

                AddFlash(targetCenter, GetterRay, 130f, 0.18f);
                target.GetVfxContainer()?.AddChildSafely(NLineBurstVfx.Create(target));
                NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);

                Tween returnTween = ownerNode.CreateTween();
                returnTween.TweenProperty(ownerNode, "global_position", origin, 0.20f)
                    .SetEase(Tween.EaseType.Out)
                    .SetTrans(Tween.TransitionType.Back);
                await Cmd.Wait(0.21f);
            });
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

    public static async Task PlayTacticalRetreat(Creature owner, Func<Task> transform)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (ownerNode == null)
            return;

        Vector2 origin = ownerNode.GlobalPosition;
        Vector2 originCenter = ownerNode.VfxSpawnPosition;
        Vector2 retreatDirection = owner.IsEnemy ? Vector2.Right : Vector2.Left;
        Vector2 offscreen = origin + retreatDirection * 980f;
        AddAfterimageLines(originCenter, originCenter + retreatDirection * 520f, RushLine, 6, 18f);

        Tween tween = ownerNode.CreateTween();
        tween.TweenProperty(ownerNode, "global_position", offscreen, 0.16f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenInterval(0.10f);
        await Cmd.Wait(0.27f);

        await transform();
        AddAfterimageLines(originCenter + retreatDirection * 520f, originCenter, GetterRay, 6, 18f);
        Tween returnTween = ownerNode.CreateTween();
        returnTween.TweenProperty(ownerNode, "global_position", origin, 0.36f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        await Cmd.Wait(0.37f);
    }

    public static async Task PlayDaggerSpray(Creature owner, IEnumerable<Creature> targets)
    {
        List<Creature> livingTargets = targets.Where(target => target.IsAlive).ToList();
        if (livingTargets.Count == 0)
            return;

        Node2D? flurry = NDaggerSprayFlurryVfx.Create(owner, GetterRay, goingRight: !owner.IsEnemy);
        if (flurry != null)
        {
            flurry.Scale = new Vector2(flurry.Scale.X, flurry.Scale.Y * 1.65f);
            NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(flurry);
        }
        await Cmd.Wait(0.08f);
        foreach (Creature target in livingTargets)
        {
            Node2D? impact = NDaggerSprayImpactVfx.Create(target, GetterRay, goingRight: !owner.IsEnemy);
            if (impact == null)
                continue;

            impact.Scale = new Vector2(impact.Scale.X, impact.Scale.Y * 1.55f);
            NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(impact);
        }
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
        Node2D root = CreateAnnihilationBlackHole();
        root.GlobalPosition = center;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);

        Tween tween = root.CreateTween();
        tween.TweenProperty(root, "scale", Vector2.One * 1.18f, 0.30f).From(Vector2.One * 0.20f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tween.Parallel().TweenProperty(root, "rotation", Mathf.Pi * 0.75f, 0.54f).AsRelative().SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(root, "scale", Vector2.One * 0.08f, 0.24f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        tween.Parallel().TweenProperty(root, "modulate:a", 0f, 0.24f).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(root.QueueFreeSafely));
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Normal);
        await Cmd.Wait(0.56f);
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
        await Cmd.Wait(0.72f);
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Normal);
    }

    public static async Task PlayNewtypeFlash(Creature owner)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (node == null)
            return;

        Texture2D? texture = ResourceLoader.Load<Texture2D>(AwakenedSoulFlashTexturePath);
        if (texture == null)
            return;

        Sprite2D sign = new()
        {
            Texture = texture,
            Centered = true,
        };
        sign.GlobalPosition = node.VfxSpawnPosition + Vector2.Up * 128f;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(sign);
        Tween tween = sign.CreateTween().SetParallel();
        tween.TweenProperty(sign, "scale", Vector2.One * 1.45f, 0.38f).From(Vector2.One * 0.35f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(sign, "modulate:a", 0f, 0.38f).SetDelay(0.20f);
        tween.Chain().TweenCallback(Callable.From(sign.QueueFreeSafely));
        await Cmd.Wait(0.40f);
    }

    public static async Task PlayNewtypeSense(Creature owner)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (node == null)
            return;

        Node2D wave = CreateNewtypeSenseWave();
        wave.GlobalPosition = node.VfxSpawnPosition + Vector2.Up * 118f;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(wave);

        Tween tween = wave.CreateTween().SetParallel();
        tween.TweenProperty(wave, "scale", Vector2.One * 1.18f, 0.34f)
            .From(Vector2.One * 0.68f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(wave, "modulate:a", 0f, 0.34f).SetDelay(0.16f);
        tween.Chain().TweenCallback(Callable.From(wave.QueueFreeSafely));
        await Cmd.Wait(0.36f);
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

    private static Node2D CreateAnnihilationBlackHole()
    {
        Node2D root = new();
        root.AddChild(CreateFilledCircle(206f, new Color(0.02f, 0.00f, 0.05f, 0.48f)));
        root.AddChild(CreateCircle(188f, new Color(0.24f, 0.07f, 0.42f, 0.66f), 22f, 0.72f));
        root.AddChild(CreateCircle(142f, new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.42f), 12f, 0.58f));
        root.AddChild(CreateCircle(98f, new Color(1.00f, 0.32f, 0.74f, 0.48f), 8f, 0.50f));
        for (int i = 0; i < 9; i++)
            root.AddChild(CreateBlackHoleSpiral(i));
        for (int i = 0; i < 18; i++)
            root.AddChild(CreateInwardShard(i, 18));
        root.AddChild(CreateFilledCircle(92f, new Color(0.00f, 0.00f, 0.015f, 0.96f)));
        root.AddChild(CreateCircle(74f, new Color(0.55f, 0.12f, 0.84f, 0.78f), 5f, 0.72f));
        root.AddChild(CreateFilledCircle(38f, new Color(0.0f, 0.0f, 0.0f, 1.0f)));
        return root;
    }

    private static Line2D CreateBlackHoleSpiral(int index)
    {
        Line2D spiral = new()
        {
            Width = 7f - index * 0.28f,
            DefaultColor = index % 2 == 0
                ? new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.50f)
                : new Color(1.0f, 0.34f, 0.76f, 0.42f),
            Antialiased = true
        };

        float start = Mathf.Tau * index / 9f;
        for (int i = 0; i < 22; i++)
        {
            float t = i / 21f;
            float angle = start + t * 2.55f;
            float radius = 34f + t * (158f + index * 3f);
            spiral.AddPoint(Vector2.Right.Rotated(angle) * radius);
        }

        return spiral;
    }

    private static Polygon2D CreateInwardShard(int index, int count)
    {
        float angle = Mathf.Tau * index / count + 0.08f * (index % 3);
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);
        float outer = 196f + (index % 4) * 12f;
        float inner = 92f + (index % 5) * 7f;
        float width = 10f + (index % 4) * 3f;
        Polygon2D shard = new()
        {
            Color = index % 2 == 0 ? new Color(0.05f, 0.0f, 0.08f, 0.72f) : new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.30f),
            Antialiased = true,
        };
        shard.Polygon = new[]
        {
            dir * outer - tangent * width,
            dir * inner,
            dir * outer + tangent * width,
        };
        return shard;
    }

    private static Node2D CreateGetterNovaNode()
    {
        Node2D root = new();
        root.AddChild(CreateFilledCircle(188f, new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.10f)));
        root.AddChild(CreateFilledCircle(132f, new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.18f)));

        for (int i = 0; i < 22; i++)
            root.AddChild(CreateNovaPetal(i, 22));
        for (int i = 0; i < 10; i++)
            root.AddChild(CreateNovaArc(i, 10));

        root.AddChild(CreateFilledCircle(102f, new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.44f)));
        root.AddChild(CreateFilledCircle(70f, new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.74f)));
        root.AddChild(CreateFilledCircle(34f, new Color(1f, 1f, 1f, 0.98f)));
        root.AddChild(CreateCircle(148f, GetterRay, 8f, 0.46f));
        root.AddChild(CreateCircle(98f, GetterWhite, 5f, 0.52f));
        root.AddChild(CreateCircle(54f, GetterRay, 3f, 0.62f));

        for (int i = 0; i < 16; i++)
            root.AddChild(CreateNovaShard(i, 16));
        for (int i = 0; i < 20; i++)
            root.AddChild(CreateNovaSpark(i, 20));

        return root;
    }

    private static Polygon2D CreateNovaPetal(int index, int count)
    {
        float angle = Mathf.Tau * index / count + 0.045f * Mathf.Sin(index * 1.7f);
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);
        float inner = 76f + (index % 3) * 4f;
        float mid = 152f + (index % 4) * 9f;
        float outer = 236f + (index % 5) * 13f;
        float baseWidth = 18f + (index % 4) * 3.5f;
        float midWidth = 10f + (index % 3) * 2f;
        Color color = index % 3 == 0
            ? new Color(1f, 0.24f, 0.66f, 0.30f)
            : new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.38f);

        Polygon2D petal = new()
        {
            Color = color,
            Antialiased = true,
        };
        Vector2 bend = tangent * Mathf.Sin(index * 1.13f) * 18f;
        petal.Polygon = new[]
        {
            dir * inner - tangent * baseWidth,
            dir * mid + bend - tangent * midWidth,
            dir * outer,
            dir * mid + bend + tangent * midWidth,
            dir * inner + tangent * baseWidth,
        };
        return petal;
    }

    private static Line2D CreateNovaArc(int index, int count)
    {
        float baseAngle = Mathf.Tau * index / count;
        float radius = 86f + (index % 5) * 18f;
        float direction = index % 2 == 0 ? 1f : -1f;
        Line2D arc = new()
        {
            Width = 4.5f + index % 3,
            DefaultColor = index % 2 == 0
                ? new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.44f)
                : new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.52f),
            Antialiased = true,
        };

        const int segmentCount = 11;
        for (int i = 0; i < segmentCount; i++)
        {
            float t = i / (float)(segmentCount - 1);
            float angle = baseAngle + direction * Mathf.Lerp(-0.62f, 0.62f, t);
            float ripple = Mathf.Sin((t + index) * 5.4f) * 5f;
            arc.AddPoint(Vector2.Right.Rotated(angle) * (radius + ripple));
        }

        return arc;
    }

    private static Polygon2D CreateNovaShard(int index, int count)
    {
        float angle = Mathf.Tau * index / count + 0.11f * (index % 3);
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);
        float inner = 112f + (index % 4) * 8f;
        float outer = 174f + (index % 5) * 10f;
        float width = 7f + (index % 3) * 2f;
        Polygon2D shard = new()
        {
            Color = index % 2 == 0
                ? new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.58f)
                : new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.54f),
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

    private static Polygon2D CreateNovaSpark(int index, int count)
    {
        float angle = Mathf.Tau * index / count + 0.23f * (index % 4);
        float distance = 38f + (index * 31 % 146);
        float radius = 3.5f + index % 4;
        Polygon2D spark = CreateFilledCircle(radius, index % 2 == 0
            ? new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.66f)
            : new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.58f));
        spark.Position = Vector2.Right.Rotated(angle) * distance;
        return spark;
    }
    private static Node2D CreateNewtypeSenseWave()
    {
        Node2D root = new();
        root.AddChild(CreateNewtypeSenseLine(new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.72f), 9f, Vector2.Zero));
        root.AddChild(CreateNewtypeSenseLine(new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.92f), 4.5f, Vector2.Up * 2f));
        root.AddChild(CreateNewtypeSenseLine(new Color(GetterPink.R, GetterPink.G, GetterPink.B, 0.36f), 3f, Vector2.Down * 8f));
        root.AddChild(CreateCircle(54f, GetterRay, 3.5f, 0.26f));
        return root;
    }

    private static Line2D CreateNewtypeSenseLine(Color color, float width, Vector2 offset)
    {
        Line2D line = new()
        {
            Width = width,
            DefaultColor = color,
            Antialiased = true,
        };

        Vector2[] points =
        {
            new(-130f, 0f),
            new(-82f, 0f),
            new(-64f, -10f),
            new(-46f, 28f),
            new(-24f, -86f),
            new(-4f, 44f),
            new(16f, -12f),
            new(38f, 0f),
            new(76f, 0f),
            new(94f, -48f),
            new(108f, 20f),
            new(126f, 0f),
        };

        foreach (Vector2 point in points)
            line.AddPoint(point + offset);

        return line;
    }
}
