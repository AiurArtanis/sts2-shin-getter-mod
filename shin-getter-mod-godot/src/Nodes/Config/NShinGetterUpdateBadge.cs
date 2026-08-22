#nullable enable
using Godot;
using ShinGetterMod.Config;

namespace ShinGetterMod.Nodes.Config;

/// <summary>
/// Non-interactive update marker shared by every Chunibyo Config entry point.
/// It owns its service subscription so freed UI nodes never remain in a static listener list.
/// </summary>
internal sealed partial class NShinGetterUpdateBadge : Label
{
    internal const string BadgeNodeName = "ShinGetterUpdateNewBadge";
    private static readonly Vector2 BadgeSize = new(58f, 28f);

    private bool _subscribed;

    private NShinGetterUpdateBadge()
    {
        Name = BadgeNodeName;
        Text = "NEW";
        CustomMinimumSize = BadgeSize;
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ZIndex = 100;
        AddThemeFontSizeOverride("font_size", 20);
        AddThemeColorOverride("font_color", new Color(1f, 0.12f, 0.12f));
        AddThemeColorOverride("font_outline_color", new Color(0.08f, 0.015f, 0.015f, 0.98f));
        AddThemeConstantOverride("outline_size", 6);

        AnchorLeft = 1f;
        AnchorRight = 1f;
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
        if (host.GetNodeOrNull<NShinGetterUpdateBadge>(BadgeNodeName) is { } existing)
        {
            existing.RefreshVisibility();
            return existing;
        }

        var badge = new NShinGetterUpdateBadge();
        host.AddChild(badge);
        return badge;
    }

    internal void RefreshVisibility()
    {
        Visible = ShinGetterChunibyoConfigService.IsCurrentUpdateUnread;
    }
}
