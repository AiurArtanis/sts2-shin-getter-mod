#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterVoicePaginator : NPaginator
{
    private Color _presentationColor = Colors.White;
    private IHoverTip? _presentationHoverTip;
    private MegaLabel? _vfxPresentationLabel;
    private int _currentFontSize = 28;

    public event Action<int>? IndexChanged;

    public override void _Ready()
    {
        ConnectSignals();
        _vfxPresentationLabel = GetNode<MegaLabel>("LabelContainer/Mask/VfxLabel");
        _label.MouseFilter = MouseFilterEnum.Pass;
        _vfxPresentationLabel.MouseFilter = MouseFilterEnum.Ignore;
        _label.Connect(Control.SignalName.MouseEntered, Callable.From(ShowHoverTip));
        _label.Connect(Control.SignalName.MouseExited, Callable.From(HideHoverTip));
        RefreshLabel();
        ApplyVfxFontSize(_currentFontSize);
        ApplyPresentation();
    }

    public void Configure(IReadOnlyList<string> options, int selectedIndex)
    {
        _options.Clear();
        _options.AddRange(options);
        _currentIndex = Mathf.Clamp(selectedIndex, 0, _options.Count - 1);
        if (IsNodeReady())
            RefreshLabel();
    }

    public void SetPresentation(Color color, IHoverTip hoverTip)
    {
        _presentationColor = color;
        _presentationHoverTip = hoverTip;
        if (IsNodeReady())
            ApplyPresentation();
    }

    protected override void OnIndexChanged(int index)
    {
        ApplyVfxFontSize(_currentFontSize);
        RefreshLabel();
        IndexChanged?.Invoke(index);
    }

    private void RefreshLabel()
    {
        if (_options.Count == 0)
            return;

        string text = _options[_currentIndex];
        _currentFontSize = text.Length switch
        {
            <= 14 => 28,
            <= 30 => 24,
            <= 44 => 22,
            _ => 20,
        };

        _label.AutoSizeEnabled = false;
        _label.AddThemeFontSizeOverride("font_size", _currentFontSize);
        _label.Text = text;
    }

    private void ApplyPresentation()
    {
        _label.Modulate = _presentationColor;
    }

    private void ApplyVfxFontSize(int fontSize)
    {
        if (_vfxPresentationLabel == null)
            return;

        _vfxPresentationLabel.AutoSizeEnabled = false;
        _vfxPresentationLabel.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private void ShowHoverTip()
    {
        if (_presentationHoverTip == null)
            return;

        NHoverTipSet.CreateAndShow(_label, _presentationHoverTip)?
            .SetGlobalPosition(_label.GlobalPosition + NSettingsScreen.settingTipsOffset);
    }

    private void HideHoverTip()
    {
        NHoverTipSet.Remove(_label);
    }
}
