#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterVoicePaginator : NPaginator
{
    private IHoverTip? _presentationHoverTip;
    private readonly List<string> _plainOptions = new();
    private MegaRichTextLabel? _richPresentationLabel;
    private Control? _hoverBounds;

    public event Action<int>? IndexChanged;

    public override void _Ready()
    {
        ConnectSignals();
        CreateRichPresentationLayer();
        PrioritizeOriginalArrows();
        RefreshLabel();
    }

    public void Configure(
        IReadOnlyList<string> richOptions,
        IReadOnlyList<string> plainOptions,
        int selectedIndex)
    {
        if (richOptions.Count != plainOptions.Count)
            throw new ArgumentException("Voice display and measurement options must have matching counts.");

        _options.Clear();
        _options.AddRange(richOptions);
        _plainOptions.Clear();
        _plainOptions.AddRange(plainOptions);
        _currentIndex = Mathf.Clamp(selectedIndex, 0, _options.Count - 1);
        if (IsNodeReady())
            RefreshLabel();
    }

    public void SetHoverTip(IHoverTip hoverTip)
    {
        _presentationHoverTip = hoverTip;
    }

    protected override void OnIndexChanged(int index)
    {
        RefreshLabel();
        IndexChanged?.Invoke(index);
    }

    private void RefreshLabel()
    {
        if (_options.Count == 0)
            return;

        string plainText = _plainOptions[_currentIndex];
        _label.Text = plainText;
        _label.Visible = false;
        GetNode<MegaLabel>("LabelContainer/Mask/VfxLabel").Visible = false;
        if (_richPresentationLabel == null)
            return;

        _richPresentationLabel.MaxFontSize = _currentIndex == 2 ? 32 : 28;
        _richPresentationLabel.SetTextAutoSize(_options[_currentIndex]);
        _richPresentationLabel.Visible = true;
    }

    private void CreateRichPresentationLayer()
    {
        Control mask = GetNode<Control>("LabelContainer/Mask");
        _label.MouseFilter = MouseFilterEnum.Ignore;
        GetNode<MegaLabel>("LabelContainer/Mask/VfxLabel").MouseFilter = MouseFilterEnum.Ignore;

        _richPresentationLabel = new MegaRichTextLabel
        {
            Name = "RichPresentationLabel",
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoSizeEnabled = true,
            MinFontSize = 16,
            MaxFontSize = 28,
            IsHorizontallyBound = true,
            IsVerticallyBound = true,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        _richPresentationLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        FontVariation font = PreloadManager.Cache.GetAsset<FontVariation>(
            "res://themes/kreon_bold_glyph_space_one.tres");
        _richPresentationLabel.AddThemeFontOverride("normal_font", font);
        _richPresentationLabel.AddThemeFontOverride("bold_font", font);
        _richPresentationLabel.AddThemeColorOverride("default_color", new Color(0.91f, 0.86f, 0.74f));
        _richPresentationLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.25f));
        _richPresentationLabel.AddThemeConstantOverride("shadow_offset_x", 3);
        _richPresentationLabel.AddThemeConstantOverride("shadow_offset_y", 2);
        mask.AddChild(_richPresentationLabel);

        _hoverBounds = new Control
        {
            Name = "VoiceOptionHoverBounds",
            MouseFilter = MouseFilterEnum.Stop,
        };
        _hoverBounds.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _hoverBounds.Connect(Control.SignalName.MouseEntered, Callable.From(ShowHoverTip));
        _hoverBounds.Connect(Control.SignalName.MouseExited, Callable.From(HideHoverTip));
        mask.AddChild(_hoverBounds);
    }

    private void PrioritizeOriginalArrows()
    {
        GetNode<Control>("LeftArrow").MoveToFront();
        GetNode<Control>("RightArrow").MoveToFront();
    }

    private void ShowHoverTip()
    {
        if (_presentationHoverTip == null)
            return;

        if (_hoverBounds == null)
            return;

        NHoverTipSet.CreateAndShow(
            _hoverBounds,
            _presentationHoverTip,
            HoverTip.GetHoverTipAlignment(_hoverBounds));
    }

    private void HideHoverTip()
    {
        if (_hoverBounds != null)
            NHoverTipSet.Remove(_hoverBounds);
    }
}
