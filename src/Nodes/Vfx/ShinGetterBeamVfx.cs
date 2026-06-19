#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace ShinGetterMod.Nodes.Vfx;

internal enum ShinGetterBeamStyle
{
    GetterBeam,
    FinalGetterBeam,
}

internal static class ShinGetterBeamVfx
{
    private static readonly Color GetterPink = new(1f, 0.19f, 0.62f, 1f);
    private static readonly Color GetterWhite = new(1f, 0.94f, 1f, 1f);
    private static readonly Color GetterRay = new("4BFEC4");
    private static readonly Color GetterRayGlow = new("A8FFE9");

    public static async Task Play(Creature owner, IEnumerable<Creature> targets, ShinGetterBeamStyle style)
    {
        List<Creature> livingTargets = targets.Where(target => target.IsAlive).ToList();
        if (livingTargets.Count == 0)
            return;

        NHyperbeamVfx? beam = NHyperbeamVfx.Create(owner, livingTargets.Last());
        if (beam != null)
        {
            ApplyBeamSkin(beam, style);
            NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(beam);
            await Cmd.Wait(NHyperbeamVfx.hyperbeamAnticipationDuration);
        }

        foreach (Creature target in livingTargets)
        {
            NHyperbeamImpactVfx? impact = NHyperbeamImpactVfx.Create(owner, target);
            if (impact == null)
                continue;

            ApplyImpactSkin(impact, style);
            NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(impact);
        }
    }

    private static void ApplyBeamSkin(Node2D beam, ShinGetterBeamStyle style)
    {
        Color primary = style == ShinGetterBeamStyle.FinalGetterBeam ? GetterRay : GetterPink;
        Color secondary = style == ShinGetterBeamStyle.FinalGetterBeam ? GetterRayGlow : GetterWhite;
        float width = style == ShinGetterBeamStyle.FinalGetterBeam ? 560f : 90f;

        TintCanvasItems(beam, primary, secondary);

        if (beam.GetNodeOrNull<Line2D>("laser/vfx_hyperbeam_laser_line") is { } line)
        {
            ConfigureLine(line, width, primary);
            if (style == ShinGetterBeamStyle.FinalGetterBeam)
                AddPinkWrapLines(line);
        }

        if (style == ShinGetterBeamStyle.GetterBeam)
        {
            ScaleNode(beam.GetNodeOrNull<Node2D>("anticipation"), new Vector2(0.55f, 0.55f));
            ScaleNode(beam.GetNodeOrNull<Node2D>("end"), new Vector2(0.65f, 0.65f));
        }
    }

    private static void ApplyImpactSkin(Node2D impact, ShinGetterBeamStyle style)
    {
        Color primary = style == ShinGetterBeamStyle.FinalGetterBeam ? GetterRay : GetterPink;
        Color secondary = style == ShinGetterBeamStyle.FinalGetterBeam ? GetterRayGlow : GetterWhite;
        TintCanvasItems(impact, primary, secondary);

        impact.Scale = style == ShinGetterBeamStyle.GetterBeam
            ? new Vector2(0.65f, 0.65f)
            : new Vector2(1.15f, 1.15f);
    }

    private static void ConfigureLine(Line2D line, float width, Color color)
    {
        line.Width = width;
        line.DefaultColor = color;
        line.SelfModulate = color;
        line.Material = null;
        line.Texture = null;
    }

    private static void AddPinkWrapLines(Line2D source)
    {
        float[] offsets = { -238f, -166f, -94f, 94f, 166f, 238f };
        for (int i = 0; i < offsets.Length; i++)
            AddWavyWrapLine(source, offsets[i], 30f + (i % 2) * 8f, 13f + (i % 3) * 2f, 2.3f + i * 0.12f, i * Mathf.Pi * 0.45f);
    }

    private static void AddWavyWrapLine(Line2D source, float offset, float width, float amplitude, float waves, float phase)
    {
        Node? parent = source.GetParent();
        if (parent == null)
            return;

        Vector2 start = source.GetPointCount() > 0 ? source.GetPointPosition(0) : new Vector2(-600f, 0f);
        Vector2 end = source.GetPointCount() > 1 ? source.GetPointPosition(source.GetPointCount() - 1) : new Vector2(600f, 0f);
        Vector2 direction = (end - start).Normalized();
        if (direction == Vector2.Zero)
            direction = Vector2.Right;
        Vector2 normal = new(-direction.Y, direction.X);

        Line2D wrap = new()
        {
            Name = "shin_getter_pink_wrap",
            Width = width,
            DefaultColor = GetterPink,
            SelfModulate = GetterPink,
            Antialiased = true,
            ZIndex = source.ZIndex + 2,
            Material = null,
            Texture = null,
            Transform = source.Transform,
        };

        const int segmentCount = 26;
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float wave = Mathf.Sin(t * Mathf.Tau * waves + phase) * amplitude;
            wrap.AddPoint(start.Lerp(end, t) + normal * (offset + wave));
        }

        parent.AddChild(wrap);
    }

    private static void TintCanvasItems(Node node, Color primary, Color secondary)
    {
        foreach (Node child in node.GetChildren())
            TintCanvasItems(child, primary, secondary);

        if (node is CanvasItem canvasItem)
        {
            canvasItem.SelfModulate = node.Name.ToString().Contains("glow") ? secondary : primary;
        }

        if (node is GpuParticles2D particles)
        {
            particles.SelfModulate = node.Name.ToString().Contains("glow") ? secondary : primary;
        }
    }

    private static void ScaleNode(Node2D? node, Vector2 scale)
    {
        if (node != null)
            node.Scale = scale;
    }
}

