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
        if (style == ShinGetterBeamStyle.FinalGetterBeam)
        {
            RemapBlueToGetterRay(beam);
            if (beam.GetNodeOrNull<Line2D>("laser/vfx_hyperbeam_laser_line") is { } line)
            {
                line.Width *= 1.3f;
                AddCenterGetterBeam(line);
            }
            return;
        }

        TintCanvasItems(beam, GetterPink, GetterWhite);
        if (beam.GetNodeOrNull<Line2D>("laser/vfx_hyperbeam_laser_line") is { } getterLine)
        {
            getterLine.Width = 90f;
            getterLine.DefaultColor = GetterPink;
            getterLine.SelfModulate = GetterPink;
        }
        ScaleNode(beam.GetNodeOrNull<Node2D>("anticipation"), new Vector2(0.55f, 0.55f));
        ScaleNode(beam.GetNodeOrNull<Node2D>("end"), new Vector2(0.65f, 0.65f));
    }

    private static void ApplyImpactSkin(Node2D impact, ShinGetterBeamStyle style)
    {
        if (style == ShinGetterBeamStyle.FinalGetterBeam)
            RemapBlueToGetterRay(impact);
        else
            TintCanvasItems(impact, GetterPink, GetterWhite);

        impact.Scale = style == ShinGetterBeamStyle.GetterBeam
            ? new Vector2(0.65f, 0.65f)
            : new Vector2(1.15f, 1.15f);
    }

    private static void RemapBlueToGetterRay(Node node)
    {
        foreach (Node child in node.GetChildren())
            RemapBlueToGetterRay(child);

        if (node is not CanvasItem canvasItem)
            return;

        canvasItem.SelfModulate = RemapBlueColor(canvasItem.SelfModulate);
        canvasItem.Modulate = RemapBlueColor(canvasItem.Modulate);
        RemapShaderPalette(canvasItem);

        if (node is Line2D line)
        {
            line.DefaultColor = RemapBlueColor(line.DefaultColor);
            Gradient? gradient = DuplicateRemappedGradient(line.GetGradient(), RemapBlueColor);
            if (gradient != null)
                line.SetGradient(gradient);
        }
    }

    private static Color RemapBlueColor(Color color)
    {
        if (color.S < 0.08f || color.H < 0.5f || color.H > 0.72f)
            return color;

        float hue = GetterRay.H + (color.H - 0.58f) * 0.32f;
        return Color.FromHsv(hue, color.S, color.V, color.A);
    }

    private static void RemapShaderPalette(CanvasItem canvasItem)
    {
        if (canvasItem.Material is not ShaderMaterial sourceMaterial)
            return;

        Variant lutParameter = sourceMaterial.GetShaderParameter("lut");
        if (lutParameter.VariantType != Variant.Type.Object
            || lutParameter.AsGodotObject() is not GradientTexture1D sourceLut)
        {
            return;
        }

        Gradient? gradient = DuplicateRemappedGradient(sourceLut.Gradient, RemapBlueColor);
        if (gradient == null)
            return;

        ShaderMaterial material = (ShaderMaterial)sourceMaterial.Duplicate(true);
        GradientTexture1D lut = (GradientTexture1D)sourceLut.Duplicate(true);
        lut.Gradient = gradient;
        material.SetShaderParameter("lut", lut);
        canvasItem.Material = material;
    }

    private static Gradient? DuplicateRemappedGradient(Gradient? source, System.Func<Color, Color> remap)
    {
        if (source == null)
            return null;

        Gradient gradient = (Gradient)source.Duplicate(true);
        for (int i = 0; i < gradient.GetPointCount(); i++)
            gradient.SetColor(i, remap(gradient.GetColor(i)));
        return gradient;
    }

    private static void AddCenterGetterBeam(Line2D source)
    {
        Node? parent = source.GetParent();
        if (parent == null)
            return;

        Line2D center = new()
        {
            Name = "shin_getter_center_getter_beam",
            Width = Mathf.Max(54f, source.Width * 0.13f),
            DefaultColor = GetterWhite,
            Antialiased = true,
            ZIndex = source.ZIndex + 3,
            Transform = source.Transform,
            Texture = source.Texture,
            TextureMode = source.TextureMode,
            JointMode = source.JointMode,
            BeginCapMode = source.BeginCapMode,
            EndCapMode = source.EndCapMode,
        };

        for (int i = 0; i < source.GetPointCount(); i++)
            center.AddPoint(source.GetPointPosition(i));

        Gradient? gradient = DuplicateRemappedGradient(source.GetGradient(), RemapBlueToGetterPink);
        if (gradient != null)
            center.SetGradient(gradient);
        parent.AddChild(center);
    }

    private static Color RemapBlueToGetterPink(Color color)
    {
        if (color.S < 0.08f)
            return color;

        float saturation = Mathf.Clamp(color.S * 0.72f, 0.2f, 0.82f);
        return Color.FromHsv(GetterPink.H, saturation, color.V, color.A);
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
