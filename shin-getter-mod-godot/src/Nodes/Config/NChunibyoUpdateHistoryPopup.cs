#nullable enable
using System;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace ShinGetterMod.Nodes.Config;

public partial class NChunibyoUpdateHistoryPopup : Control, IScreenContext
{
    private const string KreonFontPath = "res://themes/kreon_regular_shared.tres";
    private const int TitleFontSize = 42;
    private const int BodyFontSize = 21;
    private const float PopupWidth = 1120f;
    private const float PopupHeight = 820f;
    private const float BodyMinimumWidth = 956f;

    private string _title = string.Empty;
    private string _body = string.Empty;
    private Control? _returnFocus;
    private ScrollContainer? _scrollContainer;
    private NShinGetterConfigActionButton? _closeButton;

    public Control? DefaultFocusedControl => _closeButton;

    public static NChunibyoUpdateHistoryPopup Create(
        string title,
        string body,
        Control? returnFocus)
    {
        return new NChunibyoUpdateHistoryPopup
        {
            Name = "ChunibyoUpdateHistoryPopup",
            _title = title,
            _body = body,
            _returnFocus = returnFocus,
        };
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        BuildPopup();
        _closeButton?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        int scrollDelta = inputEvent.IsActionPressed("ui_page_down") ? 420
            : inputEvent.IsActionPressed("ui_page_up") ? -420
            : inputEvent.IsActionPressed("ui_down") ? 72
            : inputEvent.IsActionPressed("ui_up") ? -72
            : 0;
        if (scrollDelta == 0 || _scrollContainer == null)
            return;

        int maximum = (int)Math.Ceiling(_scrollContainer.GetVScrollBar().MaxValue);
        _scrollContainer.ScrollVertical = Math.Clamp(
            _scrollContainer.ScrollVertical + scrollDelta,
            0,
            maximum);
        GetViewport().SetInputAsHandled();
    }

    private void BuildPopup()
    {
        var center = new CenterContainer
        {
            Name = "PopupCenter",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            Name = "PopupPanel",
            CustomMinimumSize = new Vector2(PopupWidth, PopupHeight),
            MouseFilter = MouseFilterEnum.Stop,
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        center.AddChild(panel);

        var outerMargin = new MarginContainer
        {
            Name = "PopupMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        outerMargin.AddThemeConstantOverride("margin_left", 44);
        outerMargin.AddThemeConstantOverride("margin_right", 44);
        outerMargin.AddThemeConstantOverride("margin_top", 34);
        outerMargin.AddThemeConstantOverride("margin_bottom", 28);
        panel.AddChild(outerMargin);

        var column = new VBoxContainer
        {
            Name = "PopupColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 18);
        outerMargin.AddChild(column);

        var titleLabel = new Label
        {
            Name = "Title",
            Text = _title,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        ApplyLabelFont(titleLabel, TitleFontSize);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.86f, 0.74f));
        column.AddChild(titleLabel);

        var divider = new HSeparator { Name = "TitleDivider" };
        divider.AddThemeConstantOverride("separation", 2);
        column.AddChild(divider);

        _scrollContainer = new ScrollContainer
        {
            Name = "HistoryScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowAlways,
            MouseFilter = MouseFilterEnum.Stop,
        };
        column.AddChild(_scrollContainer);

        var textMargin = new MarginContainer
        {
            Name = "HistoryTextMargin",
            CustomMinimumSize = new Vector2(BodyMinimumWidth, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        textMargin.AddThemeConstantOverride("margin_left", 8);
        textMargin.AddThemeConstantOverride("margin_right", 28);
        textMargin.AddThemeConstantOverride("margin_top", 8);
        textMargin.AddThemeConstantOverride("margin_bottom", 20);
        _scrollContainer.AddChild(textMargin);

        var bodyLabel = new RichTextLabel
        {
            Name = "HistoryText",
            Text = _body,
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SelectionEnabled = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        ApplyRichTextFont(bodyLabel, BodyFontSize);
        bodyLabel.AddThemeColorOverride("default_color", new Color(0.9f, 0.91f, 0.9f));
        bodyLabel.AddThemeConstantOverride("line_separation", 5);
        textMargin.AddChild(bodyLabel);

        VScrollBar scrollBar = _scrollContainer.GetVScrollBar();
        scrollBar.Name = "HistoryScrollbar";
        scrollBar.CustomMinimumSize = new Vector2(26f, 0f);
        scrollBar.Step = 24d;
        scrollBar.MouseFilter = MouseFilterEnum.Stop;

        var footer = new HBoxContainer
        {
            Name = "PopupFooter",
            Alignment = BoxContainer.AlignmentMode.End,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        column.AddChild(footer);

        _closeButton = new NShinGetterConfigActionButton
        {
            Name = "CloseButton",
            CustomMinimumSize = new Vector2(260f, 64f),
        };
        _closeButton.Initialize(
            new LocString("main_menu_ui", "GENERIC_POPUP.ok").GetFormattedText(),
            Close);
        footer.AddChild(_closeButton);
    }

    private void Close()
    {
        Control? returnFocus = _returnFocus;
        NModalContainer.Instance?.Clear();
        if (returnFocus != null && GodotObject.IsInstanceValid(returnFocus))
            returnFocus.CallDeferred(Control.MethodName.GrabFocus);
    }

    private static StyleBoxFlat CreatePanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.052f, 0.064f, 0.99f),
            BorderColor = new Color(0.32f, 0.49f, 0.53f, 0.98f),
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ShadowColor = new Color(0f, 0f, 0f, 0.58f),
            ShadowSize = 18,
        };
    }

    private static void ApplyLabelFont(Control control, int fontSize)
    {
        control.AddThemeFontOverride("font", PreloadManager.Cache.GetAsset<Font>(KreonFontPath));
        control.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private static void ApplyRichTextFont(RichTextLabel label, int fontSize)
    {
        Font font = PreloadManager.Cache.GetAsset<Font>(KreonFontPath);
        foreach (string fontKey in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font", "mono_font" })
            label.AddThemeFontOverride(fontKey, font);
        foreach (string sizeKey in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size", "mono_font_size" })
            label.AddThemeFontSizeOverride(sizeKey, fontSize);
    }
}
