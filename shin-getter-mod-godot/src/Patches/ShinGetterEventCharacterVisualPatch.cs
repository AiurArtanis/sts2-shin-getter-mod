#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Patches;

internal static class ShinGetterEventCharacterVisuals
{
    private const string FakeMerchantRyomaTexturePath =
        "res://images/characters/shin_getter/merchant/s_g_o_merchant_ryoma_normal.png";

    internal static bool TryShowRyoma(NCreatureVisuals visuals)
    {
        Node2D body = visuals.GetCurrentBody();
        if (body.GetNodeOrNull<AnimatedSprite2D>("GetterOne") == null)
            return false;

        body.GetNodeOrNull<CanvasItem>("GetterOne")?.Hide();
        body.GetNodeOrNull<CanvasItem>("GetterTwo")?.Hide();
        body.GetNodeOrNull<CanvasItem>("GetterThree")?.Hide();
        body.GetNodeOrNull<CanvasItem>("ShinDragon")?.Hide();
        body.SelfModulate = new Color(body.SelfModulate, 1f);

        if (body.GetNodeOrNull<Sprite2D>("FakeMerchantRyoma") == null)
        {
            Sprite2D ryoma = new()
            {
                Name = "FakeMerchantRyoma",
                Texture = PreloadManager.Cache.GetTexture2D(FakeMerchantRyomaTexturePath),
                Position = new Vector2(0f, -193.576f),
                Scale = new Vector2(0.376f, 0.376f),
            };
            body.AddChildSafely(ryoma);
        }

        return true;
    }
}

[HarmonyPatch(typeof(NFakeMerchant), "StartCharacterAnimation")]
internal static class ShinGetterFakeMerchantCharacterVisualPatch
{
    private static bool Prefix(NCreatureVisuals visuals) =>
        !ShinGetterEventCharacterVisuals.TryShowRyoma(visuals);
}

[HarmonyPatch(typeof(TheArchitect), nameof(TheArchitect.OnRoomEnter))]
internal static class ShinGetterArchitectCharacterVisualPatch
{
    private static void Postfix(TheArchitect __instance)
    {
        if (__instance.Owner?.Character is not ShinGetter)
            return;

        NShinGetterStaticVisuals.ShowForm(
                __instance.Owner.Creature,
                ShinGetterForm.Getter1,
                animate: false)
            .GetAwaiter()
            .GetResult();
    }
}
