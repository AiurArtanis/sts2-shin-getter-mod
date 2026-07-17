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
    private static readonly Color GetterRay = new(0.109804f, 0.752941f, 0.6f, 1f);
    private static readonly Color GetterPink = new(1f, 0.19f, 0.62f, 1f);
    private static readonly Color GetterWhite = new(1f, 0.94f, 1f, 1f);

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
            if (style == ShinGetterBeamStyle.FinalGetterBeam)
                AddCenterGetterBeam(owner, livingTargets.Last());
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
            TintCanvasItems(beam, GetterRay, GetterRay);
            if (beam.GetNodeOrNull<Line2D>("laser/vfx_hyperbeam_laser_line") is { } line)
            {
                line.Width *= 1.43f;
                line.DefaultColor = GetterRay;
                line.SelfModulate = GetterRay;
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
            TintCanvasItems(impact, GetterRay, GetterRay);
        else
            TintCanvasItems(impact, GetterPink, GetterWhite);

        impact.Scale = style == ShinGetterBeamStyle.GetterBeam
            ? new Vector2(0.65f, 0.65f)
            : new Vector2(1.15f, 1.15f);
    }

    private static void RemapBlueToGetterPink(Node node)
    {
        foreach (Node child in node.GetChildren())
            RemapBlueToGetterPink(child);

        if (node is not CanvasItem canvasItem)
            return;

        canvasItem.SelfModulate = RemapBlueToGetterPink(canvasItem.SelfModulate);
        canvasItem.Modulate = RemapBlueToGetterPink(canvasItem.Modulate);
        RemapShaderPalette(canvasItem, RemapBlueToGetterPink);

        if (node is Line2D line)
        {
            line.DefaultColor = RemapBlueToGetterPink(line.DefaultColor);
            Gradient? gradient = DuplicateRemappedGradient(line.GetGradient(), RemapBlueToGetterPink);
            if (gradient != null)
                line.SetGradient(gradient);
        }
    }

    private static Color RemapBlueToGetterPink(Color color)
    {
        if (color.S < 0.08f || color.H < 0.5f || color.H > 0.72f)
            return color;

        float hue = GetterPink.H + (color.H - 0.58f) * 0.12f;
        float saturation = Mathf.Clamp(color.S * 0.9f, 0.35f, 0.96f);
        return Color.FromHsv(hue, saturation, color.V, color.A);
    }

    private static void RemapShaderPalette(CanvasItem canvasItem, System.Func<Color, Color> remap)
    {
        if (canvasItem.Material is not ShaderMaterial sourceMaterial)
            return;

        Variant lutParameter = sourceMaterial.GetShaderParameter("lut");
        if (lutParameter.VariantType != Variant.Type.Object
            || lutParameter.AsGodotObject() is not GradientTexture1D sourceLut)
        {
            return;
        }

        Gradient? gradient = DuplicateRemappedGradient(sourceLut.Gradient, remap);
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

    private static void AddCenterGetterBeam(Creature owner, Creature target)
    {
        NHyperbeamVfx? template = NHyperbeamVfx.Create(owner, target);
        if (template == null)
            return;

        Node2D center = new()
        {
            Name = "shin_getter_center_getter_beam",
            Position = template.Position,
            Rotation = template.Rotation,
            Scale = new Vector2(1f, 1.40f),
            ZIndex = 8,
        };

        // Keep the complete Hyperbeam visual tree without running a second copy of its SFX and shake sequence.
        while (template.GetChildCount() > 0)
        {
            Node child = template.GetChild(0);
            template.RemoveChild(child);
            center.AddChild(child);
        }
        template.Free();

        ApplyBeamSkin(center, ShinGetterBeamStyle.GetterBeam);
        Node? vfxContainer = NCombatRoom.Instance?.CombatVfxContainer;
        if (vfxContainer == null)
        {
            center.Free();
            return;
        }

        center.GetNodeOrNull<Node2D>("laser")?.Hide();
        vfxContainer.AddChildSafely(center);
        _ = TaskHelper.RunSafely(PlayCenterGetterBeamSequence(center));
    }

    private static async Task PlayCenterGetterBeamSequence(Node2D center)
    {
        RestartParticles(center.GetNodeOrNull<Node>("anticipation"));
        await Cmd.Wait(NHyperbeamVfx.hyperbeamAnticipationDuration);
        if (!GodotObject.IsInstanceValid(center))
            return;

        Node2D? laser = center.GetNodeOrNull<Node2D>("laser");
        laser?.Show();
        RestartParticles(laser);
        await Cmd.Wait(NHyperbeamVfx.hyperbeamLaserDuration);
        if (!GodotObject.IsInstanceValid(center))
            return;

        laser?.Hide();
        RestartParticles(center.GetNodeOrNull<Node>("end"));
        await Cmd.Wait(2f);
        if (GodotObject.IsInstanceValid(center))
            center.QueueFreeSafely();
    }

    private static void RestartParticles(Node? root)
    {
        if (root == null)
            return;

        if (root is GpuParticles2D particles)
            particles.Restart();
        foreach (Node child in root.GetChildren())
            RestartParticles(child);
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
