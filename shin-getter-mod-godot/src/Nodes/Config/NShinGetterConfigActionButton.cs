#nullable enable
using System;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterConfigActionButton : NSettingsButton
{
    private const string BaseLibButtonTexturePath = "res://BaseLib/images/config/configbutton.png";
    private const string FallbackButtonTexturePath = "res://images/atlases/ui_atlas.sprites/popup_confirm_button.tres";
    private readonly MegaLabel _label;
    private Action? _action;
    private IHoverTip? _hoverTip;

    public NShinGetterConfigActionButton()
    {
        CustomMinimumSize = new Vector2(324f, 64f);
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        SizeFlagsVertical = SizeFlags.Fill;
        FocusMode = FocusModeEnum.All;

        var image = new TextureRect
        {
            Name = "Image",
            Texture = LoadButtonTexture(),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            SelfModulate = Color.FromHtml("#3b7a83"),
        };
        image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(image);

        _label = new MegaLabel
        {
            Name = "Label",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutoSizeEnabled = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _label.AddThemeFontOverride(
            "font",
            PreloadManager.Cache.GetAsset<FontVariation>("res://themes/kreon_bold_glyph_space_two.tres"));
        _label.AddThemeFontSizeOverride("font_size", 28);
        _label.AddThemeColorOverride("font_color", new Color(0.91f, 0.86f, 0.74f));
        _label.AddThemeConstantOverride("outline_size", 12);
        _label.AddThemeColorOverride("font_outline_color", new Color(0.29f, 0.14f, 0.14f));
        AddChild(_label);

        NSelectionReticle reticle = PreloadManager.Cache
            .GetScene(SceneHelper.GetScenePath("ui/selection_reticle"))
            .Instantiate<NSelectionReticle>();
        reticle.Name = "SelectionReticle";
        reticle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(reticle);
    }

    public override void _Ready()
    {
        ConnectSignals();
        Connect(Control.SignalName.MouseEntered, Callable.From(ShowHoverTip));
        Connect(Control.SignalName.MouseExited, Callable.From(HideHoverTip));
    }

    public void Initialize(string text, Action action, IHoverTip? hoverTip = null)
    {
        _label.SetTextAutoSize(text);
        _action = action;
        _hoverTip = hoverTip;
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        _action?.Invoke();
    }

    private void ShowHoverTip()
    {
        if (_hoverTip == null)
            return;

        NHoverTipSet.CreateAndShow(
            this,
            _hoverTip,
            HoverTip.GetHoverTipAlignment(this));
    }

    private void HideHoverTip()
    {
        NHoverTipSet.Remove(this);
    }

    private static Texture2D LoadButtonTexture()
    {
        string path = ResourceLoader.Exists(BaseLibButtonTexturePath)
            ? BaseLibButtonTexturePath
            : FallbackButtonTexturePath;
        return ResourceLoader.Load<Texture2D>(path)
            ?? throw new InvalidOperationException($"Unable to load config action button texture: {path}");
    }
}
