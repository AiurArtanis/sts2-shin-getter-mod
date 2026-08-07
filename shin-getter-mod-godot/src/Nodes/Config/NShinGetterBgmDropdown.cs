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

    internal event Action<ShinGetterBgmTrack>? TrackChanged;

    internal ShinGetterBgmTrack SelectedTrack { get; private set; } =
        ShinGetterBgmCatalog.Tracks[0];

    public override void _Ready()
    {
        ConnectSignals();
        PopulateItems();
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

    private static string LocalizeTrack(ShinGetterBgmTrack track) =>
        LocString.GetIfExists(LocTable, track.LocalizationKey)?.GetFormattedText() ?? track.FallbackTitle;
}
