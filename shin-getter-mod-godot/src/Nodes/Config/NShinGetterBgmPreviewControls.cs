#nullable enable
using System;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterBgmPreviewControls : HBoxContainer
{
    private const string AtlasPath = "res://images/ui/chunibyo/bgm_controls_atlas.png";
    private const string LocTable = "settings_ui";
    private const int AtlasCellSize = 64;

    private readonly Button _playPauseButton;
    private readonly Button _stopButton;
    private readonly Texture2D _playIcon;
    private readonly Texture2D _pauseIcon;
    private Func<ShinGetterBgmTrack>? _trackProvider;
    private ShinGetterBgmCategory _category;

    public NShinGetterBgmPreviewControls()
    {
        CustomMinimumSize = new Vector2(108f, 64f);
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        AddThemeConstantOverride("separation", 4);

        _playIcon = CreateAtlasIcon(0);
        _pauseIcon = CreateAtlasIcon(1);
        _playPauseButton = CreateIconButton(_playIcon, "SHIN_GETTER_CHUNIBYO.BGM.PLAY");
        _playPauseButton.Pressed += OnPlayPausePressed;
        AddChild(_playPauseButton);

        _stopButton = CreateIconButton(CreateAtlasIcon(2), "SHIN_GETTER_CHUNIBYO.BGM.STOP");
        _stopButton.Pressed += ShinGetterBgmPreviewService.Stop;
        AddChild(_stopButton);
    }

    public override void _Ready()
    {
        ShinGetterBgmPreviewService.StateChanged += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        ShinGetterBgmPreviewService.StateChanged -= Refresh;
    }

    internal void Configure(ShinGetterBgmCategory category, Func<ShinGetterBgmTrack> trackProvider)
    {
        _category = category;
        _trackProvider = trackProvider;
        Refresh();
    }

    internal void OnSelectionChanged()
    {
        ShinGetterBgmTrack? track = _trackProvider?.Invoke();
        if (ShinGetterBgmPreviewService.ActiveCategory == _category
            && track?.Id != ShinGetterBgmPreviewService.ActiveTrackId)
        {
            ShinGetterBgmPreviewService.Stop();
        }
        else
        {
            Refresh();
        }
    }

    private void OnPlayPausePressed()
    {
        if (_trackProvider?.Invoke() is { } track)
            ShinGetterBgmPreviewService.Toggle(track, _category);
    }

    private void Refresh()
    {
        ShinGetterBgmTrack? track = _trackProvider?.Invoke();
        bool hasPreview = track != null
            && track.Id != ShinGetterBgmCatalog.DefaultTrackId
            && !string.IsNullOrWhiteSpace(track.ResourcePath);
        bool isActive = hasPreview
            && ShinGetterBgmPreviewService.ActiveCategory == _category
            && ShinGetterBgmPreviewService.ActiveTrackId == track!.Id;
        bool isPlaying = isActive
            && ShinGetterBgmPreviewService.State == ShinGetterBgmPreviewState.Playing;

        _playPauseButton.Disabled = !hasPreview;
        _playPauseButton.Icon = isPlaying ? _pauseIcon : _playIcon;
        _playPauseButton.TooltipText = Localize(
            isPlaying ? "SHIN_GETTER_CHUNIBYO.BGM.PAUSE" : "SHIN_GETTER_CHUNIBYO.BGM.PLAY",
            isPlaying ? "Pause preview" : "Play preview");
        _stopButton.Visible = isActive;
    }

    private static Button CreateIconButton(Texture2D icon, string tooltipKey)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(52f, 52f),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            FocusMode = FocusModeEnum.All,
            Flat = true,
            Icon = icon,
            ExpandIcon = true,
            TooltipText = Localize(tooltipKey, tooltipKey),
        };
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.Resized += () => button.PivotOffset = button.Size * 0.5f;
        button.MouseEntered += () => button.Scale = Vector2.One * 1.2f;
        button.MouseExited += () => button.Scale = Vector2.One;
        return button;
    }

    private static Texture2D CreateAtlasIcon(int index)
    {
        Texture2D atlas = ResourceLoader.Load<Texture2D>(AtlasPath)
            ?? throw new InvalidOperationException($"Unable to load BGM controls atlas: {AtlasPath}");
        return new AtlasTexture
        {
            Atlas = atlas,
            Region = new Rect2(index * AtlasCellSize, 0f, AtlasCellSize, AtlasCellSize),
        };
    }

    private static string Localize(string key, string fallback) =>
        LocString.GetIfExists(LocTable, key)?.GetFormattedText() ?? fallback;
}
