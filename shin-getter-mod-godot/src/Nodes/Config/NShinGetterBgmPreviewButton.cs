#nullable enable
using System;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterBgmPreviewButton : NSettingsButton
{
    private readonly TextureRect _icon;
    private Action? _action;
    private bool _requestedEnabled = true;
    private bool _signalsConnected;

    public NShinGetterBgmPreviewButton()
    {
        CustomMinimumSize = new Vector2(52f, 52f);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        FocusMode = FocusModeEnum.All;

        _icon = new TextureRect
        {
            Name = "Icon",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _icon.OffsetLeft = 5f;
        _icon.OffsetTop = 5f;
        _icon.OffsetRight = -5f;
        _icon.OffsetBottom = -5f;
        AddChild(_icon);

        NSelectionReticle reticle = PreloadManager.Cache
            .GetScene(SceneHelper.GetScenePath("ui/selection_reticle"))
            .Instantiate<NSelectionReticle>();
        reticle.Name = "SelectionReticle";
        reticle.MouseFilter = MouseFilterEnum.Ignore;
        reticle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(reticle);

        Resized += UpdatePivot;
    }

    public override void _Ready()
    {
        ConnectSignals();
        _signalsConnected = true;
        UpdatePivot();
        ApplyEnabledState();
    }

    internal void Initialize(Texture2D icon, string tooltip, Action action)
    {
        _icon.Texture = icon;
        TooltipText = tooltip;
        _action = action;
    }

    internal void SetIcon(Texture2D icon)
    {
        _icon.Texture = icon;
    }

    internal void SetTooltip(string tooltip)
    {
        TooltipText = tooltip;
    }

    internal void SetPreviewEnabled(bool enabled)
    {
        _requestedEnabled = enabled;
        if (_signalsConnected)
            ApplyEnabledState();
        else
            SelfModulate = enabled ? Colors.White : new Color(1f, 1f, 1f, 0.35f);
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(this, "scale", Vector2.One * 1.2f, 0.05);
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        _action?.Invoke();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SelfModulate = Colors.White;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        SelfModulate = new Color(1f, 1f, 1f, 0.35f);
        Scale = Vector2.One;
    }

    private void ApplyEnabledState()
    {
        SetEnabled(_requestedEnabled);
        SelfModulate = _requestedEnabled ? Colors.White : new Color(1f, 1f, 1f, 0.35f);
    }

    private void UpdatePivot()
    {
        PivotOffset = Size * 0.5f;
    }
}
