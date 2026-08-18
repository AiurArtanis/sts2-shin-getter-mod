#nullable enable
using System;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterBgmPreviewControls : HBoxContainer
{
    private const string AtlasPath = "res://images/atlases/ui_atlas.png";
    private const string LocTable = "settings_ui";

    private readonly NShinGetterBgmPreviewButton _playPauseButton;
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
        _playPauseButton = CreateIconButton(
            "PlayPauseButton",
            _playIcon,
            "SHIN_GETTER_CHUNIBYO.BGM.PLAY",
            "Play preview",
            OnPlayPausePressed);
        AddChild(_playPauseButton);
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
            && (!CanPreviewSelection(track)
                || track?.Id != ShinGetterBgmPreviewService.ActiveTrackId))
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
        if (_trackProvider?.Invoke() is { } track && CanPreviewSelection(track))
            ShinGetterBgmPreviewService.Toggle(track, _category);
    }

    private void Refresh()
    {
        ShinGetterBgmTrack? track = _trackProvider?.Invoke();
        bool hasPreview = CanPreviewSelection(track);
        bool isActive = hasPreview
            && ShinGetterBgmPreviewService.ActiveCategory == _category
            && ShinGetterBgmPreviewService.ActiveTrackId == track!.Id;
        bool isPlaying = isActive
            && ShinGetterBgmPreviewService.State == ShinGetterBgmPreviewState.Playing;

        _playPauseButton.SetPreviewEnabled(hasPreview);
        _playPauseButton.SetIcon(isPlaying ? _pauseIcon : _playIcon);
        _playPauseButton.SetTooltip(Localize(
            isPlaying ? "SHIN_GETTER_CHUNIBYO.BGM.PAUSE" : "SHIN_GETTER_CHUNIBYO.BGM.PLAY",
            isPlaying ? "Pause preview" : "Play preview"));
    }

    private static bool CanPreviewSelection(ShinGetterBgmTrack? track) =>
        track != null
        && track.Id != ShinGetterBgmCatalog.RandomTrackId
        && ShinGetterBgmCatalog.CanPreview(track);

    private static NShinGetterBgmPreviewButton CreateIconButton(
        string name,
        Texture2D icon,
        string tooltipKey,
        string tooltipFallback,
        Action action)
    {
        var button = new NShinGetterBgmPreviewButton
        {
            Name = name,
        };
        button.Initialize(icon, Localize(tooltipKey, tooltipFallback), action);
        return button;
    }

    private static Texture2D CreateAtlasIcon(int index)
    {
        Rect2 region = index switch
        {
            0 => new Rect2(0f, 0f, 276f, 276f),
            1 => new Rect2(276f, 0f, 276f, 276f),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        Texture2D atlas = ResourceLoader.Load<Texture2D>(AtlasPath)
            ?? throw new InvalidOperationException($"Unable to load BGM controls atlas: {AtlasPath}");
        return new AtlasTexture
        {
            Atlas = atlas,
            Region = region,
        };
    }

    private static string Localize(string key, string fallback) =>
        LocString.GetIfExists(LocTable, key)?.GetFormattedText() ?? fallback;
}
