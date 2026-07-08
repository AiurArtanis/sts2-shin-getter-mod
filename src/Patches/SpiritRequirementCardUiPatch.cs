#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class SpiritRequirementCardUiPatch
{
    private const string IconContainerName = "ShinGetterSpiritRequirementIcons";
    private const string KiIconPath = "res://images/atlases/power_atlas.sprites/s_g_p_ki.tres";
    private const float IconSize = 22f;
    private const float IconStep = 18f;
    private const float Left = -96f;
    private const float Top = -221f;

    private static Texture2D? _kiIcon;

    private static void Postfix(NCard __instance, CardPreviewMode previewMode)
    {
        int amount = GetSpiritRequirement(__instance, previewMode);
        Control container = GetOrCreateContainer(__instance);
        RefreshIcons(container, amount);
    }

    private static int GetSpiritRequirement(NCard cardNode, CardPreviewMode previewMode)
    {
        if (cardNode.Visibility != ModelVisibility.Visible)
            return 0;

        if (cardNode.Model is not ShinGetterCardBase card)
            return 0;

        return previewMode == CardPreviewMode.Upgrade
            ? card.UpgradePreviewSpiritRequirement
            : card.SpiritRequirement;
    }

    private static Control GetOrCreateContainer(NCard cardNode)
    {
        if (cardNode.Body.GetNodeOrNull<Control>(IconContainerName) is { } existing)
            return existing;

        var container = new Control
        {
            Name = IconContainerName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 0,
        };
        cardNode.Body.AddChild(container);
        return container;
    }

    private static void RefreshIcons(Control container, int amount)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }

        if (amount <= 0)
        {
            container.Visible = false;
            return;
        }

        container.Visible = true;
        container.SetAnchorsPreset(Control.LayoutPreset.Center);
        container.OffsetLeft = Left;
        container.OffsetTop = Top;
        container.OffsetRight = Left + IconSize + IconStep * (amount - 1);
        container.OffsetBottom = Top + IconSize;

        Texture2D iconTexture = GetKiIcon();
        for (int i = 0; i < amount; i++)
        {
            var icon = new TextureRect
            {
                Name = $"KiIcon{i + 1}",
                Texture = iconTexture,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ZIndex = 0,
            };
            icon.OffsetLeft = IconStep * i;
            icon.OffsetTop = 0f;
            icon.OffsetRight = icon.OffsetLeft + IconSize;
            icon.OffsetBottom = IconSize;
            container.AddChild(icon);
        }
    }

    private static Texture2D GetKiIcon()
    {
        if (_kiIcon != null)
            return _kiIcon;

        _kiIcon = PreloadManager.Cache.ContainsKey(KiIconPath)
            ? PreloadManager.Cache.GetTexture2D(KiIconPath)
            : ResourceLoader.Load<Texture2D>(KiIconPath);

        return _kiIcon;
    }
}
