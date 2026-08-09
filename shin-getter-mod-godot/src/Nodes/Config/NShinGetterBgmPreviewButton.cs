#nullable enable
using System;
using Godot;

namespace ShinGetterMod.Nodes.Config;

/// <summary>
/// A native Godot button for BGM preview controls. Native Button input is used
/// deliberately so the controls share the same reliable release path as the
/// rest of the dynamically-built configuration page.
/// </summary>
public partial class NShinGetterBgmPreviewButton : Button
{
    private Action? _action;
    private Tween? _scaleTween;
    private bool _mouseHovered;
    private bool _signalsConnected;

    public NShinGetterBgmPreviewButton()
    {
        CustomMinimumSize = new Vector2(52f, 52f);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        Flat = true;
        ExpandIcon = true;
        AddThemeConstantOverride("icon_max_width", 42);

        var emptyStyle = new StyleBoxEmpty();
        AddThemeStyleboxOverride("normal", emptyStyle);
        AddThemeStyleboxOverride("hover", emptyStyle);
        AddThemeStyleboxOverride("pressed", emptyStyle);
        AddThemeStyleboxOverride("focus", emptyStyle);
        AddThemeStyleboxOverride("disabled", emptyStyle);
    }

    public override void _Ready()
    {
        if (!_signalsConnected)
        {
            Pressed += InvokeAction;
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
            FocusEntered += RefreshScale;
            FocusExited += RefreshScale;
            Resized += UpdatePivot;
            _signalsConnected = true;
        }

        UpdatePivot();
        RefreshScale();
    }

    internal void Initialize(Texture2D icon, string tooltip, Action action)
    {
        Icon = icon;
        TooltipText = tooltip;
        _action = action;
    }

    internal void SetIcon(Texture2D icon)
    {
        Icon = icon;
    }

    internal void SetTooltip(string tooltip)
    {
        TooltipText = tooltip;
    }

    internal void SetPreviewEnabled(bool enabled)
    {
        Disabled = !enabled;
        SelfModulate = enabled ? Colors.White : new Color(1f, 1f, 1f, 0.35f);
        RefreshScale();
    }

    private void InvokeAction()
    {
        if (!Disabled)
            _action?.Invoke();
    }

    private void OnMouseEntered()
    {
        _mouseHovered = true;
        RefreshScale();
    }

    private void OnMouseExited()
    {
        _mouseHovered = false;
        RefreshScale();
    }

    private void RefreshScale()
    {
        if (!IsInsideTree())
            return;

        Vector2 target = !Disabled && (_mouseHovered || HasFocus())
            ? Vector2.One * 1.2f
            : Vector2.One;
        _scaleTween?.Kill();
        _scaleTween = CreateTween();
        _scaleTween.TweenProperty(this, "scale", target, target == Vector2.One ? 0.16 : 0.05)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
    }

    private void UpdatePivot()
    {
        PivotOffset = Size * 0.5f;
    }
}
