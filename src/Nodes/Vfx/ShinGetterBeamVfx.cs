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
    private static readonly Color GetterRayGlow = new("CFFFF0");

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
        AddWrapLine(source, -44f, -1.7f, 72f);
        AddWrapLine(source, 44f, 1.7f, 56f);
        AddWrapLine(source, 0f, 0.8f, 34f);
    }

    private static void AddWrapLine(Line2D source, float yOffset, float rotationDegrees, float width)
    {
        var wrap = (Line2D)source.Duplicate();
        wrap.Name = "shin_getter_pink_wrap";
        wrap.Position += new Vector2(0f, yOffset);
        wrap.RotationDegrees += rotationDegrees;
        wrap.ZIndex = source.ZIndex + 2;
        ConfigureLine(wrap, width, GetterPink);
        source.GetParent()?.AddChild(wrap);
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
