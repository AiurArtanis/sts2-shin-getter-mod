#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Nodes.RestSite;

[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
internal static class ShinGetterRestSiteCharacterPatch
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

    private static void Postfix(NRestSiteCharacter __instance)
    {
        if (__instance.Player.Character is not ShinGetter)
            return;

        Sprite2D? sprite = __instance.GetNodeOrNull<Sprite2D>("%RyomaRestSprite");
        ColorRect? shadow = __instance.GetNodeOrNull<ColorRect>("%GroundShadow");
        if (sprite == null || shadow == null)
            return;

        SeatPresentation presentation = SeatPresentations[ResolveSeatIndex(__instance)];
        if (sprite.Material is ShaderMaterial firelightMaterial)
        {
            firelightMaterial.SetShaderParameter("light_strength", presentation.LightStrength);
            firelightMaterial.SetShaderParameter("light_radius", presentation.LightRadius);
            firelightMaterial.SetShaderParameter("flicker_phase", presentation.FlickerPhase);
        }

        shadow.Size = new Vector2(presentation.ShadowWidth, 96f);
        shadow.Position = new Vector2(
            -presentation.ShadowWidth * 0.5f + presentation.ShadowOffsetX,
            -32f);
        shadow.Rotation = -0.055f;

        if (shadow.Material is ShaderMaterial shadowMaterial)
            shadowMaterial.SetShaderParameter("shadow_opacity", presentation.ShadowOpacity);
    }

    private static int ResolveSeatIndex(NRestSiteCharacter character)
    {
        string parentName = character.GetParent()?.Name.ToString() ?? string.Empty;
        for (int i = 0; i < SeatPresentations.Length; i++)
        {
            if (parentName.EndsWith((i + 1).ToString(), System.StringComparison.Ordinal))
                return i;
        }

        return 0;
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom._Ready))]
internal static class ShinGetterRestSiteRoomPatch
{
    private static readonly Vector2 RyomaGroundPosition = new(526f, 786f);

    private static void Postfix(NRestSiteRoom __instance)
    {
        NRestSiteCharacter? displayedRyoma = null;
        foreach (NRestSiteCharacter character in __instance.characterAnims)
        {
            if (character.Player.Character is not ShinGetter)
                continue;

            bool shouldDisplay = displayedRyoma == null;
            SetRyomaVisible(character, shouldDisplay);
            if (shouldDisplay)
                displayedRyoma = character;
        }

        if (displayedRyoma == null)
            return;

        int seatIndex = ResolveSeatIndex(displayedRyoma);
        if (seatIndex % 2 == 1)
            displayedRyoma.FlipX();

        if (displayedRyoma.GetParent() is Control seatContainer)
            seatContainer.Position = RyomaGroundPosition;
    }

    private static void SetRyomaVisible(NRestSiteCharacter character, bool visible)
    {
        Sprite2D? sprite = character.GetNodeOrNull<Sprite2D>("%RyomaRestSprite");
        ColorRect? shadow = character.GetNodeOrNull<ColorRect>("%GroundShadow");
        if (sprite != null)
            sprite.Visible = visible;
        if (shadow != null)
            shadow.Visible = visible;
    }

    private static int ResolveSeatIndex(NRestSiteCharacter character)
    {
        string parentName = character.GetParent()?.Name.ToString() ?? string.Empty;
        for (int i = 0; i < 4; i++)
        {
            if (parentName.EndsWith((i + 1).ToString(), System.StringComparison.Ordinal))
                return i;
        }

        return 0;
    }
}
