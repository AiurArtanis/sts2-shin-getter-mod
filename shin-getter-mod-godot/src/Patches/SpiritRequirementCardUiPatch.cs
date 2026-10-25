#nullable enable
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NCard))]
internal static class SpiritRequirementCardUiPatch
{
    private const string KiIconPath = "res://images/atlases/power_atlas.sprites/s_g_p_ki.tres";
    private const float LeftPadding = 13f;
    private const float TypeIconGap = 10f;
    private const float IconSize = 20f;
    private const float IconTop = 8f;
    private const float IconAmountGap = 6f;
    private const float MinimumAmountWidth = 18f;
    private const float RightPadding = 17f;

    private static readonly ConditionalWeakTable<NCard, PlaqueState> States = new();
    private static Texture2D? _kiIcon;
    private static bool _failureLogged;

    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    [HarmonyPostfix]
    private static void UpdateVisualsPostfix(NCard __instance, CardPreviewMode previewMode)
    {
        Refresh(__instance, previewMode);
    }

    [HarmonyPatch("Reload")]
    [HarmonyPostfix]
    private static void ReloadPostfix(NCard __instance)
    {
        // A pooled NCard may now represent a different model or a hidden library entry.
        Refresh(__instance, CardPreviewMode.Normal);
    }

    [HarmonyPatch("UpdateTypePlaqueSizeAndPosition")]
    [HarmonyPostfix]
    private static void TypePlaqueSizePostfix(NCard __instance)
    {
        // Reload defers this native resize; reapply after it, without queuing another resize.
        CardPreviewMode mode = States.TryGetValue(__instance, out PlaqueState? state)
            ? state.PreviewMode
            : CardPreviewMode.Normal;
        Refresh(__instance, mode);
    }

    private static void Refresh(NCard cardNode, CardPreviewMode previewMode)
    {
        if (!cardNode.IsNodeReady())
            return;

        try
        {
            int amount = GetSpiritRequirement(cardNode, previewMode);
            if (amount <= 0)
            {
                RestoreNative(cardNode);
                return;
            }

            Texture2D? texture = GetKiIcon();
            if (texture == null)
            {
                RestoreNative(cardNode);
                WarnOnce("Ki texture is unavailable.");
                return;
            }

            if (!States.TryGetValue(cardNode, out PlaqueState? state))
            {
                NinePatchRect? plaque = cardNode.GetNodeOrNull<NinePatchRect>("%TypePlaque");
                MegaLabel? typeLabel = cardNode.GetNodeOrNull<MegaLabel>("%TypeLabel");
                if (plaque == null || typeLabel == null)
                {
                    WarnOnce("Native TypePlaque or TypeLabel is unavailable.");
                    return;
                }

                state = CreateState(plaque, typeLabel);
                States.Add(cardNode, state);
            }

            state.PreviewMode = previewMode;
            ApplyLayout(state, texture, amount);
        }
        catch (Exception exception)
        {
            RestoreNative(cardNode);
            WarnOnce(exception.Message);
        }
    }

    private static int GetSpiritRequirement(NCard cardNode, CardPreviewMode previewMode)
    {
        if (cardNode.Visibility != ModelVisibility.Visible ||
            cardNode.Model is not ShinGetterCardBase card ||
            card.Rarity == CardRarity.Ancient ||
            card.Type is not (CardType.Attack or CardType.Skill or CardType.Power))
            return 0;

        return previewMode == CardPreviewMode.Upgrade
            ? card.UpgradePreviewSpiritRequirement
            : card.SpiritRequirement;
    }

    private static PlaqueState CreateState(NinePatchRect plaque, MegaLabel nativeTypeLabel)
    {
        var content = new Control
        {
            Name = "ShinGetterSpiritTypePlaque",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            UseParentMaterial = false,
            Visible = false,
        };
        try
        {
            // Preserve the native label for the game's deferred text measurement and pooling.
            // Duplicates retain its current locale font, colors and non-autosizing 16pt style.
            MegaLabel typeLabel = DuplicateLabel(nativeTypeLabel, "SpiritTypeLabel");
            content.AddChild(typeLabel);
            MegaLabel amountLabel = DuplicateLabel(nativeTypeLabel, "SpiritRequirementAmount");
            content.AddChild(amountLabel);
            var icon = new TextureRect
            {
                Name = "SpiritRequirementIcon",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                UseParentMaterial = false,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            content.AddChild(icon);
            var state = new PlaqueState(plaque, nativeTypeLabel, content, typeLabel, amountLabel, icon);
            plaque.AddChild(content);
            return state;
        }
        catch
        {
            content.Free();
            throw;
        }
    }

    private static MegaLabel DuplicateLabel(MegaLabel source, string name)
    {
        var label = (MegaLabel)source.Duplicate((int)Node.DuplicateFlags.Scripts);
        label.Name = name;
        label.UniqueNameInOwner = false;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.UseParentMaterial = false;
        label.Visible = true;
        label.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        return label;
    }

    private static void ApplyLayout(PlaqueState state, Texture2D texture, int amount)
    {
        // The native plaque keeps its texture, NinePatch margins and BannerMaterial.
        // Thus rarity colors remain independent of ShinGetterCardFramePatch's form tint.
        CopyNativeFont(state.NativeTypeLabel, state.TypeLabel);
        CopyNativeFont(state.NativeTypeLabel, state.AmountLabel);
        state.TypeLabel.SetTextAutoSize(state.NativeTypeLabel.Text);
        state.AmountLabel.SetTextAutoSize(amount.ToString(CultureInfo.InvariantCulture));

        float typeWidth = Mathf.Max(state.NativeTypeLabel.Size.X, state.TypeLabel.GetMinimumSize().X);
        float amountWidth = Mathf.Max(MinimumAmountWidth, state.AmountLabel.GetMinimumSize().X);
        float iconLeft = LeftPadding + typeWidth + TypeIconGap;
        float amountLeft = iconLeft + IconSize + IconAmountGap;
        float width = amountLeft + amountWidth + RightPadding;
        float labelHeight = state.NativeTypeLabel.Size.Y;

        state.Applied = true;
        state.Plaque.Size = new Vector2(width, state.Height);
        state.Plaque.Position = new Vector2(state.CenterX - width * 0.5f, state.Top);
        state.Content.Size = state.Plaque.Size;
        state.TypeLabel.Size = new Vector2(typeWidth, labelHeight);
        state.TypeLabel.Position = new Vector2(LeftPadding, (state.Height - labelHeight) * 0.5f);
        state.Icon.Texture = texture;
        state.Icon.Size = Vector2.One * IconSize;
        state.Icon.Position = new Vector2(iconLeft, IconTop);
        state.AmountLabel.Size = new Vector2(amountWidth, labelHeight);
        state.AmountLabel.Position = new Vector2(amountLeft, (state.Height - labelHeight) * 0.5f);
        state.NativeTypeLabel.Visible = false;
        state.Content.Visible = true;
    }

    private static void CopyNativeFont(MegaLabel source, MegaLabel target)
    {
        Font font = source.GetThemeFont("font");
        if (target.GetThemeFont("font") != font)
            target.AddThemeFontOverride("font", font);
        int size = source.GetThemeFontSize("font_size");
        if (target.GetThemeFontSize("font_size") != size)
            target.AddThemeFontSizeOverride("font_size", size);
    }

    private static void RestoreNative(NCard cardNode)
    {
        if (!States.TryGetValue(cardNode, out PlaqueState? state))
            return;

        state.PreviewMode = CardPreviewMode.Normal;
        if (!state.Applied)
            return;

        try
        {
            state.Content.Visible = false;
            state.NativeTypeLabel.Visible = state.NativeTypeVisible;
            float width = Mathf.Max(state.NativeTypeLabel.Size.X + 17f, 61f);
            state.Plaque.Size = new Vector2(width, state.Height);
            state.Plaque.Position = new Vector2(state.CenterX - width * 0.5f, state.Top);
            state.Applied = false;
        }
        catch (Exception exception)
        {
            WarnOnce($"Native plaque restore failed: {exception.Message}");
        }
    }

    private static Texture2D? GetKiIcon()
    {
        if (_kiIcon == null)
        {
            _kiIcon = PreloadManager.Cache.ContainsKey(KiIconPath)
                ? PreloadManager.Cache.GetTexture2D(KiIconPath)
                : ResourceLoader.Load<Texture2D>(KiIconPath);
        }
        return _kiIcon;
    }

    private static void WarnOnce(string detail)
    {
        if (_failureLogged)
            return;
        _failureLogged = true;
        Log.Warn($"ShinGetterMod: spirit plaque unavailable; retaining native type plaque. {detail}");
    }

    private sealed class PlaqueState
    {
        public readonly NinePatchRect Plaque;
        public readonly MegaLabel NativeTypeLabel;
        public readonly Control Content;
        public readonly MegaLabel TypeLabel;
        public readonly MegaLabel AmountLabel;
        public readonly TextureRect Icon;
        public readonly float CenterX;
        public readonly float Top;
        public readonly float Height;
        public readonly bool NativeTypeVisible;
        public CardPreviewMode PreviewMode;
        public bool Applied;

        public PlaqueState(NinePatchRect plaque, MegaLabel nativeTypeLabel, Control content,
            MegaLabel typeLabel, MegaLabel amountLabel, TextureRect icon)
        {
            Plaque = plaque;
            NativeTypeLabel = nativeTypeLabel;
            Content = content;
            TypeLabel = typeLabel;
            AmountLabel = amountLabel;
            Icon = icon;
            CenterX = plaque.Position.X + plaque.Size.X * 0.5f;
            Top = plaque.Position.Y;
            Height = plaque.Size.Y;
            NativeTypeVisible = nativeTypeLabel.Visible;
        }
    }
}
