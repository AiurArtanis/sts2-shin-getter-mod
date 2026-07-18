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
    private const string NormalSpriteName = "RyomaNormalSprite";
    private const string CitizenSpriteName = "RyomaCitizenSprite";
    private const float SpriteScale = 0.376f;
    private const float SpriteFootYOffset = 70f;

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
        SetSpriteState(normalSprite, !useCitizenTexture);
        SetSpriteState(citizenSprite, useCitizenTexture);
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
