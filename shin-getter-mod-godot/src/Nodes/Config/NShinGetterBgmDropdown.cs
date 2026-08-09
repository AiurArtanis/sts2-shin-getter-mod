#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterBgmDropdown : NSettingsDropdown
{
    private const string DropdownItemScenePath = "res://scenes/ui/dropdown_item.tscn";
    private const string LocTable = "settings_ui";

    private readonly List<ShinGetterBgmTrack> _tracks = new();
    private Control? _floatingContainer;
    private Control? _dismisser;

    internal event Action<ShinGetterBgmTrack>? TrackChanged;

    internal ShinGetterBgmTrack SelectedTrack { get; private set; } =
        ShinGetterBgmCatalog.Tracks[0];

    public override void _Ready()
    {
        ConnectSignals();
        _floatingContainer = GetNode<Control>("%DropdownContainer");
        _dismisser = GetNode<Control>("%Dismisser");
        _floatingContainer.ZIndex = 200;
        _floatingContainer.ZAsRelative = false;
        _floatingContainer.Connect(
            CanvasItem.SignalName.VisibilityChanged,
            Callable.From(OnDropdownContainerVisibilityChanged));
        PopulateItems();
    }

    public override void _Process(double delta)
    {
        if (_floatingContainer is { Visible: true })
            PositionFloatingContainer();
    }

    internal void ConfigureLayout(float width)
    {
        CustomMinimumSize = new Vector2(width, 64f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Control container = GetNode<Control>("%DropdownContainer");
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        container.Position = new Vector2(0f, 64f);
        container.Size = new Vector2(width, 600f);
    }

    internal void Configure(IReadOnlyList<ShinGetterBgmTrack> tracks, string selectedTrackId)
    {
        _tracks.Clear();
        _tracks.AddRange(tracks);
        SelectedTrack = ShinGetterBgmCatalog.ResolveOrDefault(selectedTrackId);
        if (IsNodeReady())
            PopulateItems();
    }

    private void PopulateItems()
    {
        if (_tracks.Count == 0)
            return;

        ClearDropdownItems();
        PackedScene itemScene = ResourceLoader.Load<PackedScene>(DropdownItemScenePath)
            ?? throw new InvalidOperationException($"Unable to load BGM dropdown item scene: {DropdownItemScenePath}");
        foreach (ShinGetterBgmTrack track in _tracks)
        {
            NDropdownItem item = itemScene.Instantiate<NDropdownItem>(PackedScene.GenEditState.Disabled);
            _dropdownItems.AddChild(item);
            item.Text = LocalizeTrack(track);
            item.Connect(
                NDropdownItem.SignalName.Selected,
                Callable.From<NDropdownItem>(_ => SelectTrack(track)));
        }

        _dropdownItems.GetParent<NDropdownContainer>().RefreshLayout();
        _currentOptionLabel.SetTextAutoSize(LocalizeTrack(SelectedTrack));
    }

    private void SelectTrack(ShinGetterBgmTrack track)
    {
        SelectedTrack = track;
        _currentOptionLabel.SetTextAutoSize(LocalizeTrack(track));
        CloseDropdown();
        TrackChanged?.Invoke(track);
    }

    private void OnDropdownContainerVisibilityChanged()
    {
        if (_floatingContainer == null)
            return;

        _floatingContainer.TopLevel = _floatingContainer.Visible;
        if (_dismisser != null)
            _dismisser.TopLevel = _floatingContainer.Visible;
        if (_floatingContainer.Visible)
            PositionFloatingContainer();
    }

    private void PositionFloatingContainer()
    {
        if (_floatingContainer == null)
            return;

        Vector2 viewportSize = GetViewportRect().Size;
        float belowY = GlobalPosition.Y + Size.Y;
        float popupY = belowY + _floatingContainer.Size.Y <= viewportSize.Y - 24f
            ? belowY
            : Mathf.Max(24f, GlobalPosition.Y - _floatingContainer.Size.Y);
        _floatingContainer.GlobalPosition = new Vector2(GlobalPosition.X, popupY);
    }

    private static string LocalizeTrack(ShinGetterBgmTrack track) =>
        LocString.GetIfExists(LocTable, track.LocalizationKey)?.GetFormattedText() ?? track.FallbackTitle;
}
