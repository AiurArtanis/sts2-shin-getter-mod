#nullable enable
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using ShinGetterMod.Config;

namespace ShinGetterMod.Nodes.Config;

/// <summary>
/// Non-interactive update marker shared by every Chunibyo Config entry point.
/// It owns its service subscription so freed UI nodes never remain in a static listener list.
/// </summary>
internal sealed partial class NShinGetterUpdateBadge : MegaRichTextLabel
{
    internal const string BadgeNodeName = "ShinGetterUpdateNewBadge";
    private static readonly Vector2 BadgeSize = new(58f, 28f);

    private bool _subscribed;

    private NShinGetterUpdateBadge()
    {
        Name = BadgeNodeName;
        BbcodeEnabled = true;
        Text = "[rainbow]NEW[/rainbow]";
        AutoSizeEnabled = false;
        FitContent = false;
        ScrollActive = false;
        AutowrapMode = TextServer.AutowrapMode.Off;
        CustomMinimumSize = BadgeSize;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ZIndex = 100;
        FontVariation font = PreloadManager.Cache.GetAsset<FontVariation>(
            "res://themes/kreon_bold_glyph_space_one.tres");
        AddThemeFontOverride("normal_font", font);
        AddThemeFontOverride("bold_font", font);
        AddThemeFontSizeOverride("normal_font_size", 22);
        AddThemeFontSizeOverride("bold_font_size", 22);
        AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.015f, 0.015f, 0.98f));
        AddThemeConstantOverride("outline_size", 6);

        ApplyPlacement(outsideLeft: false);
    }

    private void ApplyPlacement(bool outsideLeft)
    {
        AnchorLeft = outsideLeft ? 0f : 1f;
        AnchorRight = outsideLeft ? 0f : 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -66f;
        OffsetRight = -8f;
        OffsetTop = 2f;
        OffsetBottom = 30f;
    }

    public override void _Ready()
    {
        if (!_subscribed)
        {
            ShinGetterChunibyoConfigService.UpdateReadStateChanged += RefreshVisibility;
            _subscribed = true;
        }

        RefreshVisibility();
    }

    public override void _ExitTree()
    {
        if (!_subscribed)
            return;

        ShinGetterChunibyoConfigService.UpdateReadStateChanged -= RefreshVisibility;
        _subscribed = false;
    }

    internal static NShinGetterUpdateBadge AttachTo(Control host)
    {
        return Attach(host, outsideLeft: false);
    }

    internal static NShinGetterUpdateBadge AttachOutsideLeft(Control host)
    {
        return Attach(host, outsideLeft: true);
    }

    internal static void RemoveFrom(Control host)
    {
        if (host.GetNodeOrNull<NShinGetterUpdateBadge>(BadgeNodeName) is not { } existing)
            return;

        host.RemoveChild(existing);
        existing.QueueFree();
    }

    private static NShinGetterUpdateBadge Attach(Control host, bool outsideLeft)
    {
        if (host.GetNodeOrNull<NShinGetterUpdateBadge>(BadgeNodeName) is { } existing)
        {
            existing.ApplyPlacement(outsideLeft);
            existing.RefreshVisibility();
            return existing;
        }

        var badge = new NShinGetterUpdateBadge();
        badge.ApplyPlacement(outsideLeft);
        host.AddChild(badge);
        return badge;
    }

    internal void RefreshVisibility()
    {
        Visible = ShinGetterChunibyoConfigService.IsCurrentUpdateUnread;
    }
}
