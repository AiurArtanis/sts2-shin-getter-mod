#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Nodes.Screens.Shops;

internal static class ShinGetterMerchantVisuals
{
    private const string SpineSpriteName = "SpineSprite";
    private const string GroundShadowName = "GroundShadow";
    private const string NormalSpriteName = "RyomaNormalSprite";
    private const string CitizenSpriteName = "RyomaCitizenSprite";
    private const float SpriteScale = 0.376f;
    private const float SpriteFootYOffset = 70f;
    private const float ShadowWidth = 420f;
    private const float ShadowHeight = 132f;
    private const float ShadowCenterX = -34f;
    private const float ShadowCenterY = 42f;
    private const float ShadowOpacity = 0.40f;

    public static void RefreshCurrentRoom()
    {
        Refresh(NMerchantRoom.Instance);
    }

    public static void Refresh(NMerchantRoom? room)
    {
        if (room == null)
            return;

        bool useCitizenTexture = ShouldUseCitizenTexture();
        foreach (NMerchantCharacter visual in room.PlayerVisuals)
            Refresh(visual, useCitizenTexture);
    }

    private static void Refresh(NMerchantCharacter visual, bool useCitizenTexture)
    {
        var normalSprite = visual.GetNodeOrNull<Sprite2D>(NormalSpriteName);
        var citizenSprite = visual.GetNodeOrNull<Sprite2D>(CitizenSpriteName);
        if (normalSprite == null && citizenSprite == null)
            return;

        visual.GetNodeOrNull<CanvasItem>(SpineSpriteName)?.Hide();
        ConfigureGroundShadow(visual.GetNodeOrNull<ColorRect>(GroundShadowName));
        SetSpriteState(normalSprite, !useCitizenTexture);
        SetSpriteState(citizenSprite, useCitizenTexture);
    }

    private static void ConfigureGroundShadow(ColorRect? shadow)
    {
        if (shadow == null)
            return;

        shadow.Visible = true;
        shadow.Size = new Vector2(ShadowWidth, ShadowHeight);
        shadow.Position = new Vector2(
            ShadowCenterX - ShadowWidth * 0.5f,
            ShadowCenterY - ShadowHeight * 0.5f);
        shadow.Rotation = -0.025f;

        if (shadow.Material is ShaderMaterial shadowMaterial)
        {
            shadowMaterial.SetShaderParameter("shadow_opacity", ShadowOpacity);
            shadowMaterial.SetShaderParameter("tail_offset", new Vector2(0.22f, -0.36f));
            shadowMaterial.SetShaderParameter("tail_strength", 0.74f);
            shadowMaterial.SetShaderParameter("shadow_color", new Vector3(0.018f, 0.012f, 0.010f));
        }
    }

    private static void SetSpriteState(Sprite2D? sprite, bool visible)
    {
        if (sprite == null)
            return;

        sprite.Visible = visible;
        sprite.Centered = true;
        sprite.Scale = Vector2.One * SpriteScale;

        if (sprite.Texture != null)
            sprite.Position = new Vector2(0f, -sprite.Texture.GetHeight() * SpriteScale * 0.5f + SpriteFootYOffset);
    }

    private static bool ShouldUseCitizenTexture()
    {
        var runState = RunManager.Instance.DebugOnlyGetState();
        var player = LocalContext.GetMe(runState);
        var goodCitizenCard = player?.GetRelic<SGR_GoodCitizenCard>();
        return goodCitizenCard is { IsUsedUp: false };
    }
}
