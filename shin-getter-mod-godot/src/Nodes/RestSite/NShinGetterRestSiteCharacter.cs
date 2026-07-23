#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace ShinGetterMod.Nodes.RestSite;

public partial class NShinGetterRestSiteCharacter : NRestSiteCharacter
{
    private readonly record struct SeatPresentation(
        float LightStrength,
        float LightRadius,
        float FlickerPhase,
        float ShadowWidth,
        float ShadowOffsetX,
        float ShadowOpacity);

    private static readonly SeatPresentation[] SeatPresentations =
    {
        new(0.34f, 0.76f, 0.3f, 430f, -24f, 0.30f),
        new(0.32f, 0.78f, 1.7f, 420f, -22f, 0.29f),
        new(0.46f, 0.66f, 2.9f, 470f, -38f, 0.34f),
        new(0.43f, 0.68f, 4.1f, 460f, -35f, 0.33f),
    };

    public override void _Ready()
    {
        base._Ready();

        int seatIndex = ResolveSeatIndex();
        SeatPresentation presentation = SeatPresentations[seatIndex];

        Sprite2D sprite = GetNode<Sprite2D>("%RyomaRestSprite");
        if (sprite.Material is ShaderMaterial firelightMaterial)
        {
            firelightMaterial.SetShaderParameter("light_strength", presentation.LightStrength);
            firelightMaterial.SetShaderParameter("light_radius", presentation.LightRadius);
            firelightMaterial.SetShaderParameter("flicker_phase", presentation.FlickerPhase);
        }

        ColorRect shadow = GetNode<ColorRect>("%GroundShadow");
        shadow.Size = new Vector2(presentation.ShadowWidth, 96f);
        shadow.Position = new Vector2(
            -presentation.ShadowWidth * 0.5f + presentation.ShadowOffsetX,
            -32f);
        shadow.Rotation = -0.055f;

        if (shadow.Material is ShaderMaterial shadowMaterial)
            shadowMaterial.SetShaderParameter("shadow_opacity", presentation.ShadowOpacity);
    }

    private int ResolveSeatIndex()
    {
        string parentName = GetParent()?.Name.ToString() ?? string.Empty;
        for (int i = 0; i < SeatPresentations.Length; i++)
        {
            if (parentName.EndsWith((i + 1).ToString(), System.StringComparison.Ordinal))
                return i;
        }

        return 0;
    }
}
