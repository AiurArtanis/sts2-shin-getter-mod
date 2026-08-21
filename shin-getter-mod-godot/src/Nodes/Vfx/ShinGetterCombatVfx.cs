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
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace ShinGetterMod.Nodes.Vfx;

internal static partial class ShinGetterCombatVfx
{
    private static readonly Color GetterRay = new(0.266667f, 0.988235f, 0.772549f, 1f);
    private static readonly Color GetterPink = new(1f, 0.18f, 0.58f, 1f);
    private static readonly Color GetterWhite = new(1f, 0.95f, 1f, 1f);
    private static readonly Color RushLine = new(0.92f, 0.92f, 0.88f, 1f);
    private static readonly Color KiYellow = new(1f, 0.88f, 0.22f, 1f);
    private static readonly Color HotBloodOrange = new(1f, 0.36f, 0.08f, 1f);
    private static readonly Color SpiritGold = new(1f, 0.72f, 0.02f, 1f);
    private static readonly Color WhiteFlash = new(1f, 1f, 0.92f, 1f);
    private static readonly Color SolarCore = new(1f, 0.96f, 0.72f, 1f);
    private static readonly Color SolarGold = new(1f, 0.68f, 0.06f, 1f);
    private static readonly Color SolarOrange = new(1f, 0.28f, 0.02f, 1f);
    private static readonly Color SolarRed = new(0.95f, 0.08f, 0.01f, 1f);
    private static readonly Color ThunderBlue = new(0.08f, 0.52f, 1f, 1f);
    private static readonly Color ThunderCyan = new(0.18f, 0.94f, 1f, 1f);

    public static Task PlayKiAura(Creature creature) => PlayForbiddenIncantationAura(creature, KiYellow, 0.42f, 118f, 1, 8, ShakeStrength.Weak);

    public static Task PlayHotBloodAura(Creature creature) => PlayForbiddenIncantationAura(creature, HotBloodOrange, 0.52f, 145f, 2, 12, ShakeStrength.Medium);

    public static Task PlaySpiritAura(Creature creature) => PlayForbiddenIncantationAura(creature, SpiritGold, 0.62f, 172f, 3, 16, ShakeStrength.Strong);

    public static Task PlaySuperKiAura(Creature creature) => PlayForbiddenIncantationAura(creature, SpiritGold, 0.78f, 196f, 4, 22, ShakeStrength.Strong, withLightning: true);

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

        owner.GetVfxContainer()?.AddChildSafely(NHorizontalLinesVfx.Create(new Color("F0F0E8AA"), 1.0, movingRightwards: !owner.IsEnemy));
        AddSpeedLines(ownerCenter, targetCenter, whiteFlash ? WhiteFlash : RushLine);

        Tween tween = ownerNode.CreateTween();
        tween.TweenProperty(ownerNode, "global_position", origin + lunge, 0.07f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ownerNode, "global_position", origin, 0.12f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
        await Cmd.Wait(0.21f);
    }

    public static async Task PlayExpansionRush(Creature owner, Creature target)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (ownerNode == null || targetNode == null)
            return;

        Vector2 origin = ownerNode.GlobalPosition;
        Vector2 ownerCenter = ownerNode.VfxSpawnPosition;
        Vector2 targetCenter = targetNode.VfxSpawnPosition;
        Vector2 direction = (targetCenter - ownerCenter).Normalized();
        Vector2 lunge = direction * Math.Max(0f, ownerCenter.DistanceTo(targetCenter) - 105f);

        Vector2 originalScale = ownerNode.Scale;
        Vector2 enlargedScale = originalScale * 1.55f;

        AddFlash(ownerCenter, GetterRay, 150f, 0.22f);
        Tween growTween = ownerNode.CreateTween();
        growTween.TweenProperty(ownerNode, "scale", enlargedScale, 0.22f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        await Cmd.Wait(0.28f);

        owner.GetVfxContainer()?.AddChildSafely(NHorizontalLinesVfx.Create(new Color("F0F0E8AA"), 1.15, movingRightwards: !owner.IsEnemy));
        AddSpeedLines(ownerCenter, targetCenter, RushLine);

        Tween tween = ownerNode.CreateTween();
        tween.TweenProperty(ownerNode, "global_position", origin + lunge, 0.13f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(ownerNode, "global_position", origin, 0.16f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await Cmd.Wait(0.14f);
        target.GetVfxContainer()?.AddChildSafely(NLineBurstVfx.Create(target));
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
        await Cmd.Wait(0.18f);

        Tween shrinkTween = ownerNode.CreateTween();
        shrinkTween.TweenProperty(ownerNode, "scale", originalScale, 0.14f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        await Cmd.Wait(0.14f);
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

    public static async Task PlayBurningGrowl(Creature owner)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (ownerNode == null)
            return;

        owner.GetBackVfxContainer()?.AddChildSafely(NTestSubjectBurnVfx.Create());
        AddFlash(ownerNode.VfxSpawnPosition, HotBloodOrange, 160f, 0.35f);
        await Cmd.Wait(0.75f);
    }

    public static async Task PlayThunderField(Creature owner, IEnumerable<Creature> targets)
    {
        NCombatRoom? combatRoom = NCombatRoom.Instance;
        Control? container = combatRoom?.CombatVfxContainer;
        if (combatRoom == null || container == null)
            return;

        List<Creature> livingTargets = targets.Where(target => target.IsAlive).ToList();
        Vector2 size = container.Size;
        if (size.X < 100f || size.Y < 100f)
            size = combatRoom.GetViewportRect().Size;

        Node2D root = CreateFullscreenThunderNet(size);
        root.Name = "shin_getter_poseidon_thunder_net";
        root.Modulate = new Color(1f, 1f, 1f, 0f);
        container.AddChildSafely(root);

        Tween thunderTween = root.CreateTween();
        thunderTween.TweenProperty(root, "modulate:a", 1f, 0.05f);
        thunderTween.TweenProperty(root, "modulate:a", 0.32f, 0.07f);
        thunderTween.TweenProperty(root, "modulate:a", 1f, 0.05f);
        thunderTween.TweenInterval(0.08f);
        thunderTween.TweenProperty(root, "modulate:a", 0f, 0.26f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);
        thunderTween.TweenCallback(Callable.From(root.QueueFreeSafely));

        await Cmd.Wait(0.16f);
        foreach (Creature target in livingTargets)
        {
            if (combatRoom.GetCreatureNode(target) is { } targetNode)
                AddFlash(targetNode.VfxSpawnPosition, ThunderCyan, 155f, 0.22f);
        }

        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Normal);
        await Cmd.Wait(0.22f);
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

        Vector2 source = ownerNode.VfxSpawnPosition;
        Vector2 destination = targetPositions.Aggregate(Vector2.Zero, (sum, pos) => sum + pos) / targetPositions.Count;
        Node2D ball = CreateSolarEnergyBall();
        ball.GlobalPosition = source + Vector2.Up * 18f;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(ball);

        Tween tween = ball.CreateTween();
        tween.TweenProperty(ball, "scale", Vector2.One * 1.75f, 0.78f).From(Vector2.One * 0.08f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);
        tween.TweenProperty(ball, "global_position", destination, 0.4f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenCallback(Callable.From(ball.QueueFreeSafely));
        await Cmd.Wait(1.2f);

        foreach (Vector2 pos in targetPositions)
            AddFlash(pos, SolarOrange, 155f, 0.26f);
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
    }

    public static async Task PlayStonerSunshine(
        Creature owner,
        IEnumerable<Creature> targets,
        float sequenceDurationSeconds)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        List<Vector2> targetPositions = targets
            .Where(target => target.IsAlive)
            .Select(target => NCombatRoom.Instance?.GetCreatureNode(target)?.VfxSpawnPosition)
            .OfType<Vector2>()
            .ToList();
        if (ownerNode == null || targetPositions.Count == 0)
            return;

        const float flightDurationSeconds = 0.4f;
        const float ascentDurationSeconds = 0.75f;
        const float landingDurationSeconds = 0.4f;
        float totalDuration = Math.Max(2.6f, sequenceDurationSeconds);
        float firstGrowthDuration = Math.Min(2f, totalDuration - flightDurationSeconds - 0.2f);
        float secondGrowthDuration = Math.Max(0.2f,
            totalDuration - firstGrowthDuration - flightDurationSeconds);
        Vector2 ownerOrigin = ownerNode.GlobalPosition;
        Vector2 airborneOffset = new(-90f, -150f);
        Vector2 destination = targetPositions.Aggregate(Vector2.Zero, (sum, pos) => sum + pos)
            / targetPositions.Count;

        Tween movementTween = ownerNode.CreateTween();
        movementTween.TweenProperty(
                ownerNode,
                "global_position",
                ownerOrigin + airborneOffset,
                ascentDurationSeconds)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        movementTween.TweenInterval(Math.Max(
            0f,
            totalDuration - ascentDurationSeconds - landingDurationSeconds));
        movementTween.TweenProperty(ownerNode, "global_position", ownerOrigin, landingDurationSeconds)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);

        Node2D ball = CreateSolarEnergyBall();
        ball.GlobalPosition = ownerNode.VfxSpawnPosition + Vector2.Up * 168f;
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(ball);

        Tween firstGrowthTween = ball.CreateTween().SetParallel();
        firstGrowthTween.TweenProperty(ball, "scale", Vector2.One * 1.31f, firstGrowthDuration)
            .From(Vector2.One * 0.08f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        firstGrowthTween.TweenProperty(ball, "rotation", Mathf.Tau * 0.35f, firstGrowthDuration)
            .AsRelative();
        await Cmd.Wait(firstGrowthDuration);

        for (int index = 0; index < 16; index++)
            ball.AddChild(CreateSolarLightning(index, 16, 180f));
        AddFlash(ball.GlobalPosition, SolarCore, 220f, 0.28f);

        Tween secondGrowthTween = ball.CreateTween().SetParallel();
        secondGrowthTween.TweenProperty(ball, "scale", Vector2.One * 1.75f, secondGrowthDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        secondGrowthTween.TweenProperty(ball, "rotation", Mathf.Tau * 0.55f, secondGrowthDuration)
            .AsRelative();
        await Cmd.Wait(secondGrowthDuration);

        Tween flightTween = ball.CreateTween().SetParallel();
        flightTween.TweenProperty(ball, "global_position", destination, flightDurationSeconds)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Cubic);
        flightTween.TweenProperty(ball, "scale", Vector2.One * 1.1f, flightDurationSeconds);
        flightTween.Chain().TweenCallback(Callable.From(ball.QueueFreeSafely));
        await Cmd.Wait(flightDurationSeconds);

        foreach (Vector2 position in targetPositions)
            AddFlash(position, SolarOrange, 210f, 0.32f);
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Normal);
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

    public static async Task PlayHeavyCleave(Creature owner, IEnumerable<Creature> targets)
    {
        List<Vector2> targetPositions = targets
            .Where(target => target.IsAlive)
            .Select(target => NCombatRoom.Instance?.GetCreatureNode(target)?.VfxSpawnPosition)
            .OfType<Vector2>()
            .ToList();
        if (targetPositions.Count == 0)
            return;

        Vector2 center = targetPositions.Aggregate(Vector2.Zero, (sum, pos) => sum + pos) / targetPositions.Count;
        NCombatRoom.Instance?.RadialBlur(VfxPosition.Right);
        NGame.Instance?.DoHitStop(ShakeStrength.Strong, ShakeDuration.Normal);
        NGame.Instance?.ScreenShake(ShakeStrength.TooMuch, ShakeDuration.Normal);
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NBigSlashVfx.Create(center, facingRight: true, GetterRay));
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NBigSlashImpactVfx.Create(center, 0f, GetterRay));
        await Cmd.Wait(0.18f);
    }

    public static async Task PlayRisingDrill(Creature target)
    {
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (targetNode == null)
            return;

        Vector2 startPosition = targetNode.VfxSpawnPosition + Vector2.Down * 300f;
        Vector2 endPosition = targetNode.VfxSpawnPosition + Vector2.Up * 110f;
        Node2D tornado = new()
        {
            GlobalPosition = startPosition,
            Modulate = new Color(1f, 1f, 1f, 0f),
            Scale = new Vector2(0.72f, 0.86f),
        };

        for (int strand = 0; strand < 6; strand++)
        {
            Line2D spiral = new()
            {
                Width = 6.5f - strand * 0.55f,
                DefaultColor = strand % 2 == 0
                    ? new Color(GetterRay.R, GetterRay.G, GetterRay.B, 0.82f)
                    : new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.68f),
                Antialiased = true,
            };
            Curve widthCurve = new();
            widthCurve.AddPoint(new Vector2(0f, 0.22f));
            widthCurve.AddPoint(new Vector2(0.7f, 0.78f));
            widthCurve.AddPoint(new Vector2(1f, 1f));
            spiral.WidthCurve = widthCurve;

            for (int index = 0; index < 64; index++)
            {
                float t = index / 63f;
                float angle = t * Mathf.Tau * 4.35f + strand * (Mathf.Tau / 6f);
                float radius = Mathf.Lerp(10f, 92f, Mathf.Pow(t, 0.78f));
                Vector2 orbit = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.24f);
                spiral.AddPoint(orbit + Vector2.Up * (t * 320f));
            }
            tornado.AddChild(spiral);
        }

        for (int band = 0; band < 4; band++)
        {
            float t = (band + 1f) / 5f;
            float radius = Mathf.Lerp(22f, 82f, t);
            Line2D windBand = new()
            {
                Position = Vector2.Up * (t * 320f),
                Width = 4.5f - band * 0.45f,
                DefaultColor = new Color(GetterWhite.R, GetterWhite.G, GetterWhite.B, 0.58f),
                Antialiased = true,
            };
            for (int index = 0; index < 28; index++)
            {
                float angle = index / 27f * Mathf.Tau * 0.82f;
                windBand.AddPoint(new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.28f));
            }
            tornado.AddChild(windBand);
            tornado.CreateTween().TweenProperty(windBand, "rotation", Mathf.Tau * (band % 2 == 0 ? 1f : -1f), 0.52f)
                .AsRelative();
        }

        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(tornado);
        Tween movementTween = tornado.CreateTween().SetParallel();
        movementTween.TweenProperty(tornado, "global_position", endPosition, 0.52f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        movementTween.TweenProperty(tornado, "scale", new Vector2(1.08f, 1.04f), 0.52f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        Tween fadeTween = tornado.CreateTween();
        fadeTween.TweenProperty(tornado, "modulate:a", 0.94f, 0.07f);
        fadeTween.TweenInterval(0.34f);
        fadeTween.TweenProperty(tornado, "modulate:a", 0f, 0.11f);
        fadeTween.TweenCallback(Callable.From(tornado.QueueFreeSafely));

        await Cmd.Wait(0.36f);
        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(
            NBigSlashImpactVfx.Create(targetNode.VfxSpawnPosition, -Mathf.Pi / 2f, GetterRay));
        NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
        await Cmd.Wait(0.16f);
    }

    public static Task PlayHolyDragonRoarAtScreenCenter(Creature creature)
    {
        const float scaleMultiplier = 1.3f;
        NGame? game = NGame.Instance;
        if (game == null)
            return Task.CompletedTask;

        VfxCmd.PlayOnCreatureCenter(creature, "vfx/vfx_scream");
        return PlayForbiddenIncantationAuraAtPosition(
            game.GetViewportRect().Size * 0.5f,
            GetterRay,
            duration: 0.72f,
            radius: 520f,
            ringCount: 4,
            rayCount: 32,
            shakeStrength: ShakeStrength.Strong,
            withLightning: true,
            scaleMultiplier: scaleMultiplier);
    }

    private static Task PlayForbiddenIncantationAura(Creature creature, Color color, float duration, float radius, int ringCount, int rayCount, ShakeStrength shakeStrength, bool withLightning = false)
    {
        NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
        if (node == null)
            return Task.CompletedTask;

        VfxCmd.PlayOnCreatureCenter(creature, "vfx/vfx_scream");
        return PlayForbiddenIncantationAuraAtPosition(
            node.VfxSpawnPosition,
            color,
            duration,
            radius,
            ringCount,
            rayCount,
            shakeStrength,
            withLightning);
    }

    private static async Task PlayForbiddenIncantationAuraAtPosition(
        Vector2 position,
        Color color,
        float duration,
        float radius,
        int ringCount,
        int rayCount,
        ShakeStrength shakeStrength,
        bool withLightning = false,
        float scaleMultiplier = 1f)
    {
        NGame.Instance?.ScreenShake(shakeStrength, ShakeDuration.Short);
        Node2D root = new()
        {
            GlobalPosition = position,
            Scale = Vector2.One * 0.62f * scaleMultiplier,
        };

        for (int i = 0; i < ringCount; i++)
        {
            float ringRadius = radius * (0.38f + i * 0.15f);
            root.AddChild(CreateCircle(ringRadius, color, 7.5f - i * 0.9f, 0.46f - i * 0.05f));
        }
        root.AddChild(CreateCircle(radius * 0.26f, new Color(1f, 1f, 1f, 0.68f), 4f, 0.28f));

        for (int i = 0; i < rayCount; i++)
            root.AddChild(CreateAuraBurstLine(i, rayCount, radius, color));

        if (withLightning)
        {
            int lightningCount = Math.Max(6, rayCount / 2);
            for (int i = 0; i < lightningCount; i++)
                root.AddChild(CreateAuraLightning(i, lightningCount, radius * 1.08f, i % 2 == 0 ? GetterRay : WhiteFlash));
        }

        NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(root);
        Tween tween = root.CreateTween().SetParallel();
        tween.TweenProperty(root, "scale", Vector2.One * (1.12f + ringCount * 0.12f) * scaleMultiplier, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
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

    private static Node2D CreateAuraBurstLine(int index, int count, float radius, Color color)
    {
        float angle = Mathf.Tau * index / count + 0.035f * Mathf.Sin(index * 1.31f);
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);
        float inner = radius * (0.38f + (index % 3) * 0.025f);
        float outer = radius * (1.24f + (index % 4) * 0.05f);
        float baseWidth = 18f + (index % 4) * 4f;
        float midWidth = 8f + (index % 3) * 2f;

        Node2D root = new();
        Polygon2D shard = new()
        {
            Color = new Color(color.R, color.G, color.B, 0.70f),
            Antialiased = true,
        };
        Vector2 bend = tangent * ((index % 5) - 2) * 7f;
        shard.Polygon = new[]
        {
            dir * inner - tangent * baseWidth,
            dir * Mathf.Lerp(inner, outer, 0.62f) + bend - tangent * midWidth,
            dir * outer,
            dir * Mathf.Lerp(inner, outer, 0.62f) + bend + tangent * midWidth,
            dir * inner + tangent * baseWidth,
        };
        root.AddChild(shard);

        Line2D highlight = new()
        {
            Width = 3.5f + index % 2,
            DefaultColor = new Color(1f, 1f, 0.84f, 0.62f),
            Antialiased = true,
        };
        highlight.AddPoint(dir * (inner + 10f));
        highlight.AddPoint(dir * (outer - 10f) + bend * 0.35f);
        root.AddChild(highlight);
        return root;
    }

    private static Line2D CreateAuraLightning(int index, int count, float radius, Color color)
    {
        float angle = Mathf.Tau * index / count + 0.09f * Mathf.Sin(index * 1.7f);
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);

        Line2D line = new()
        {
            Width = 5f + index % 2,
            DefaultColor = new Color(color.R, color.G, color.B, 0.84f),
            Antialiased = true,
        };

        const int segmentCount = 6;
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float zigzag = (i % 2 == 0 ? -1f : 1f) * (14f + (index % 3) * 4f) * (1f - t * 0.25f);
            line.AddPoint(dir * Mathf.Lerp(radius * 0.28f, radius * 1.28f, t) + tangent * zigzag);
        }

        return line;
    }

    private static Node2D CreateSolarEnergyBall()
    {
        Node2D root = new();
        root.AddChild(CreateFilledCircle(218f, new Color(SolarRed.R, SolarRed.G, SolarRed.B, 0.035f)));
        root.AddChild(CreateFilledCircle(194f, new Color(SolarOrange.R, SolarOrange.G, SolarOrange.B, 0.055f)));
        root.AddChild(CreateFilledCircle(176f, new Color(SolarGold.R, SolarGold.G, SolarGold.B, 0.075f)));

        for (int index = 0; index < 24; index++)
            root.AddChild(CreateSolarCoronaRay(index, 24));
        for (int index = 0; index < 18; index++)
            root.AddChild(CreateSolarFlameTongue(index, 18));

        AddSolarGradientLayers(root);
        root.AddChild(CreateCircle(154f, SolarRed, 5f, 0.22f));
        root.AddChild(CreateCircle(132f, SolarOrange, 4f, 0.32f));
        root.AddChild(CreateCircle(96f, SolarGold, 3f, 0.38f));

        for (int index = 0; index < 10; index++)
            root.AddChild(CreateSolarSwirlLine(index, 10));
        root.AddChild(CreateSolarGloss(50f, new Vector2(-38f, -44f), new Vector2(1.22f, 0.46f), 0.34f));
        root.AddChild(CreateSolarGloss(24f, new Vector2(-58f, -68f), new Vector2(1.0f, 0.52f), 0.56f));
        root.AddChild(CreateSolarGloss(12f, new Vector2(-72f, -78f), new Vector2(0.9f, 0.62f), 0.84f));

        for (int index = 0; index < 12; index++)
            root.AddChild(CreateSolarRay(index, 12));
        for (int index = 0; index < 22; index++)
            root.AddChild(CreateSolarSpark(index, 22));

        return root;
    }

    private static void AddSolarGradientLayers(Node2D root)
    {
        const int layerCount = 22;
        for (int index = 0; index < layerCount; index++)
        {
            float t = index / (float)(layerCount - 1);
            float radius = Mathf.Lerp(162f, 18f, t);
            Color color;
            if (t < 0.34f)
                color = SolarRed.Lerp(SolarOrange, t / 0.34f);
            else if (t < 0.72f)
                color = SolarOrange.Lerp(SolarGold, (t - 0.34f) / 0.38f);
            else
                color = SolarGold.Lerp(SolarCore, (t - 0.72f) / 0.28f);

            float alpha = Mathf.Lerp(0.14f, 0.98f, Mathf.Pow(t, 0.72f));
            root.AddChild(CreateFilledCircle(radius, new Color(color.R, color.G, color.B, alpha)));
        }
    }

    private static Polygon2D CreateSolarCoronaRay(int index, int count)
    {
        float angle = Mathf.Tau * index / count + Mathf.Sin(index * 2.17f) * 0.065f;
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);
        float innerRadius = 128f + index % 4 * 6f;
        float outerRadius = 206f + index % 5 * 19f;
        float halfWidth = 5f + index % 4 * 2.4f;
        Color color = (index % 3) switch
        {
            0 => new Color(SolarRed.R, SolarRed.G, SolarRed.B, 0.24f),
            1 => new Color(SolarOrange.R, SolarOrange.G, SolarOrange.B, 0.30f),
            _ => new Color(SolarGold.R, SolarGold.G, SolarGold.B, 0.25f),
        };

        Polygon2D ray = new()
        {
            Color = color,
            Antialiased = true,
        };
        ray.Polygon = new[]
        {
            dir * innerRadius - tangent * halfWidth,
            dir * Mathf.Lerp(innerRadius, outerRadius, 0.55f) - tangent * halfWidth * 0.42f,
            dir * outerRadius,
            dir * Mathf.Lerp(innerRadius, outerRadius, 0.55f) + tangent * halfWidth * 0.42f,
            dir * innerRadius + tangent * halfWidth,
        };
        return ray;
    }

    private static Polygon2D CreateSolarFlameTongue(int index, int count)
    {
        float angle = Mathf.Tau * index / count;
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);
        float innerRadius = 112f + (index % 4) * 4f;
        float midRadius = 144f + (index % 3) * 7f;
        float outerRadius = 178f + (index % 5) * 10f;
        float halfWidth = 8f + (index % 4) * 2f;
        Color color = index % 3 == 0
            ? new Color(SolarRed.R, SolarRed.G, SolarRed.B, 0.27f)
            : new Color(SolarOrange.R, SolarOrange.G, SolarOrange.B, 0.31f);

        Polygon2D flame = new()
        {
            Color = color,
            Antialiased = true,
        };
        flame.Polygon = new[]
        {
            dir * innerRadius - tangent * halfWidth,
            dir * midRadius - tangent * halfWidth * 0.55f,
            dir * outerRadius,
            dir * midRadius + tangent * halfWidth * 0.55f,
            dir * innerRadius + tangent * halfWidth,
        };
        return flame;
    }

    private static Line2D CreateSolarSwirlLine(int index, int count)
    {
        float baseAngle = Mathf.Tau * index / count;
        float direction = index % 2 == 0 ? 1f : -1f;
        Line2D line = new()
        {
            Width = 2.4f + index % 3 * 0.55f,
            DefaultColor = index % 2 == 0
                ? new Color(SolarGold.R, SolarGold.G, SolarGold.B, 0.44f)
                : new Color(SolarCore.R, SolarCore.G, SolarCore.B, 0.38f),
            Antialiased = true,
        };

        Curve widthCurve = new();
        widthCurve.AddPoint(new Vector2(0f, 0.18f));
        widthCurve.AddPoint(new Vector2(0.45f, 1f));
        widthCurve.AddPoint(new Vector2(1f, 0.12f));
        line.WidthCurve = widthCurve;

        for (int i = 0; i < 12; i++)
        {
            float t = i / 11f;
            float angle = baseAngle + direction * t * 0.92f;
            float radius = Mathf.Lerp(42f, 142f, t) + Mathf.Sin((index + t) * 4.1f) * 4f;
            line.AddPoint(Vector2.Right.Rotated(angle) * radius);
        }

        return line;
    }

    private static Line2D CreateSolarRay(int index, int count)
    {
        float angle = Mathf.Tau * index / count;
        Vector2 dir = Vector2.Right.Rotated(angle);
        Line2D ray = new()
        {
            Width = 2.2f + index % 3 * 0.65f,
            DefaultColor = index % 2 == 0
                ? new Color(SolarCore.R, SolarCore.G, SolarCore.B, 0.52f)
                : new Color(SolarGold.R, SolarGold.G, SolarGold.B, 0.40f),
            Antialiased = true,
        };
        ray.AddPoint(dir * (142f + index % 3 * 5f));
        ray.AddPoint(dir * (222f + index % 4 * 18f));
        return ray;
    }

    private static Polygon2D CreateSolarGloss(
        float radius,
        Vector2 position,
        Vector2 scale,
        float alpha)
    {
        Polygon2D gloss = CreateFilledCircle(radius, new Color(1f, 1f, 0.92f, alpha));
        gloss.Position = position;
        gloss.Scale = scale;
        gloss.Rotation = -0.42f;
        return gloss;
    }

    private static Line2D CreateSolarLightning(int index, int count, float radius)
    {
        float angle = Mathf.Tau * index / count + Mathf.Sin(index * 1.83f) * 0.11f;
        Vector2 dir = Vector2.Right.Rotated(angle);
        Vector2 tangent = new(-dir.Y, dir.X);
        Line2D line = new()
        {
            Width = 2.2f + index % 3 * 0.55f,
            DefaultColor = index % 2 == 0
                ? new Color(SolarCore.R, SolarCore.G, SolarCore.B, 0.82f)
                : new Color(SolarGold.R, SolarGold.G, SolarGold.B, 0.68f),
            Antialiased = true,
        };

        const int segmentCount = 8;
        for (int segment = 0; segment <= segmentCount; segment++)
        {
            float t = segment / (float)segmentCount;
            float zigzag = (segment % 2 == 0 ? -1f : 1f)
                * (7f + index % 4 * 2f)
                * Mathf.Sin(t * Mathf.Pi);
            line.AddPoint(dir * Mathf.Lerp(radius * 0.62f, radius * 1.30f, t) + tangent * zigzag);
        }

        return line;
    }

    private static Polygon2D CreateSolarSpark(int index, int count)
    {
        float angle = Mathf.Tau * index / count + 0.21f * (index % 3);
        float distance = 72f + (index * 41 % 118);
        float radius = 2f + index % 3 * 1.2f;
        Polygon2D spark = CreateFilledCircle(radius, index % 2 == 0
            ? new Color(SolarCore.R, SolarCore.G, SolarCore.B, 0.82f)
            : new Color(SolarGold.R, SolarGold.G, SolarGold.B, 0.66f));
        spark.Position = Vector2.Right.Rotated(angle) * distance;
        return spark;
    }

    private static Polygon2D CreateFilledCircle(float radius, Color color)
    {
        Polygon2D polygon = new()
        {
            Color = color,
            Antialiased = true,
        };
        polygon.Polygon = CirclePoints(radius, 64).ToArray();
        return polygon;
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

    private static Node2D CreateFullscreenThunderNet(Vector2 size)
    {
        Node2D root = new();
        Polygon2D wash = new()
        {
            Color = new Color(0.05f, 0.45f, 0.86f, 0.13f),
            Polygon = new[]
            {
                Vector2.Zero,
                new Vector2(size.X, 0f),
                size,
                new Vector2(0f, size.Y),
            },
        };
        root.AddChild(wash);

        (Vector2 From, Vector2 To)[] paths =
        {
            (new Vector2(-0.08f, 0.12f), new Vector2(1.08f, 0.72f)),
            (new Vector2(-0.08f, 0.44f), new Vector2(1.08f, 0.18f)),
            (new Vector2(-0.08f, 0.78f), new Vector2(1.08f, 0.48f)),
            (new Vector2(-0.05f, 0.96f), new Vector2(1.05f, 0.62f)),
            (new Vector2(0.08f, -0.08f), new Vector2(0.34f, 1.08f)),
            (new Vector2(0.38f, -0.08f), new Vector2(0.64f, 1.08f)),
            (new Vector2(0.74f, -0.08f), new Vector2(0.92f, 1.08f)),
            (new Vector2(0.12f, 1.08f), new Vector2(0.94f, -0.08f)),
        };

        for (int index = 0; index < paths.Length; index++)
        {
            (Vector2 fromRatio, Vector2 toRatio) = paths[index];
            Vector2 from = new(fromRatio.X * size.X, fromRatio.Y * size.Y);
            Vector2 to = new(toRatio.X * size.X, toRatio.Y * size.Y);
            root.AddChild(CreateScreenLightningBolt(from, to, index, 10f + index % 3 * 2f));
        }

        return root;
    }

    private static Node2D CreateScreenLightningBolt(Vector2 from, Vector2 to, int seed, float width)
    {
        Node2D bolt = new();
        Vector2 delta = to - from;
        Vector2 direction = delta.Normalized();
        Vector2 normal = new(-direction.Y, direction.X);
        const int segmentCount = 14;
        var points = new Vector2[segmentCount + 1];
        for (int index = 0; index <= segmentCount; index++)
        {
            float t = index / (float)segmentCount;
            float taper = Mathf.Sin(t * Mathf.Pi);
            float offset = Mathf.Sin(seed * 2.73f + index * 4.19f) * (44f + seed % 3 * 9f) * taper;
            points[index] = from.Lerp(to, t) + normal * offset;
        }

        Line2D glow = new()
        {
            Width = width * 2.8f,
            DefaultColor = new Color(ThunderBlue.R, ThunderBlue.G, ThunderBlue.B, 0.30f),
            Antialiased = true,
        };
        Line2D core = new()
        {
            Width = width,
            DefaultColor = new Color(ThunderCyan.R, ThunderCyan.G, ThunderCyan.B, 0.92f),
            Antialiased = true,
        };
        foreach (Vector2 point in points)
        {
            glow.AddPoint(point);
            core.AddPoint(point);
        }
        bolt.AddChild(glow);
        bolt.AddChild(core);

        for (int index = 3; index < segmentCount; index += 4)
        {
            float branchSide = ((index + seed) & 1) == 0 ? 1f : -1f;
            Vector2 branchEnd = points[index]
                + direction * (100f + seed % 4 * 18f)
                + normal * branchSide * (86f + index * 3f);
            Line2D branch = CreateCrackLine(points[index], branchEnd, ThunderCyan);
            branch.Width = width * 0.52f;
            bolt.AddChild(branch);
        }

        return bolt;
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
