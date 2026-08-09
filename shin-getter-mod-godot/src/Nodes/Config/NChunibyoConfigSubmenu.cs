#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using ShinGetterMod.Audio;
using ShinGetterMod.Config;
using ShinGetterMod.Diagnostics.CardExport;

namespace ShinGetterMod.Nodes.Config;

public partial class NChunibyoConfigSubmenu : NSubmenu
{
    private const string ManifestPath = "res://ShinGetterMod.json";
    private const string UpdateHistoryPath = "res://ShinGetterMod/update_history.json";
    private const string CharacterIconPath = "res://images/ui/top_panel/character_icon_shin_getter.png";
    private const string KreonFontPath = "res://themes/kreon_regular_shared.tres";
    private const string ConfigTickboxScenePath = "res://scenes/screens/settings_tickbox.tscn";
    private const string VoicePaginatorScenePath = "res://scenes/screens/paginator.tscn";
    private const string SettingsDropdownScenePath = "res://scenes/screens/settings_dropdown.tscn";
    private const string SettingsArrowPath = "res://images/packed/common_ui/settings_tiny_left_arrow.png";
    private const string LocTable = "settings_ui";
    private const int PageTitleFontSize = 52;
    private const int SidebarTitleFontSize = 48;
    private const int SettingFontSize = 28;
    private const int NoteFontSize = 21;
    private const float SettingControlWidth = 560f;
    private const float BgmDropdownWidth = 400f;
    private const float BgmTextColumnMinimumWidth = 620f;
    private const float BgmPreviewControlsWidth = 108f;
    private const float BgmControlSeparation = 12f;
    private const float BgmTrackControlsWidth =
        BgmDropdownWidth + BgmPreviewControlsWidth + BgmControlSeparation;

    private Control? _initialFocus;
    private Label? _exportPathLabel;
    private NShinGetterVoicePaginator? _voiceModePaginator;
    private FileDialog? _folderDialog;

    protected override Control? InitialFocusedControl => _initialFocus;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildInterface();
        Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(OnConfigVisibilityChanged));
    }

    public override void OnSubmenuOpened()
    {
        base.OnSubmenuOpened();
        _initialFocus?.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void BuildInterface()
    {
        var shade = new ColorRect
        {
            Name = "Shade",
            Color = new Color(0.025f, 0.035f, 0.045f, 0.84f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var margin = new MarginContainer
        {
            Name = "ContentMargin",
            MouseFilter = MouseFilterEnum.Pass,
        };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 82);
        margin.AddThemeConstantOverride("margin_right", 82);
        margin.AddThemeConstantOverride("margin_top", 64);
        margin.AddThemeConstantOverride("margin_bottom", 118);
        AddChild(margin);

        var columns = new HBoxContainer
        {
            Name = "Columns",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        columns.AddThemeConstantOverride("separation", 28);
        margin.AddChild(columns);

        var backButton = PreloadManager.Cache
            .GetScene(SceneHelper.GetScenePath("ui/back_button"))
            .Instantiate<NBackButton>();
        backButton.Name = "BackButton";
        backButton.ZIndex = 100;
        backButton.ZAsRelative = false;
        AddChild(backButton);
        ConnectSignals();

        columns.AddChild(BuildModList());
        columns.AddChild(BuildSettingsPanel());
    }

    private Control BuildModList()
    {
        var panel = CreatePanel(new Vector2(292f, 0f));
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateInnerMargin();
        panel.AddChild(margin);

        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 14);
        margin.AddChild(list);

        list.AddChild(CreateHeading(Localize("SHIN_GETTER_CHUNIBYO.MODS", "Mods"), SidebarTitleFontSize));
        list.AddChild(CreateDivider());

        var modButton = new Button
        {
            CustomMinimumSize = new Vector2(0f, 66f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All,
            ToggleMode = true,
            ButtonPressed = true,
            Flat = true,
        };
        AddSelectedModButtonStyles(modButton);

        var selectedBackdrop = new Panel
        {
            Name = "SelectedModBackdrop",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        selectedBackdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        selectedBackdrop.AddThemeStyleboxOverride(
            "panel",
            CreateModButtonStyle(new Color(0.15f, 0.15f, 0.15f, 0.5f)));
        modButton.AddChild(selectedBackdrop);

        var modButtonContent = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        modButtonContent.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        modButtonContent.OffsetLeft = 20f;
        modButtonContent.OffsetRight = -16f;
        modButtonContent.AddThemeConstantOverride("separation", 12);
        modButton.AddChild(modButtonContent);

        var icon = new TextureRect
        {
            Texture = ResourceLoader.Load<Texture2D>(CharacterIconPath),
            CustomMinimumSize = new Vector2(40f, 40f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        modButtonContent.AddChild(icon);

        var modLabel = new Label
        {
            Text = Localize("SHIN_GETTER_CHUNIBYO.MOD_NAME", "Shin Getter"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        ApplyKreonFont(modLabel, 24);
        modLabel.AddThemeColorOverride("font_color", StsColors.gold);
        modButtonContent.AddChild(modLabel);
        modButton.Toggled += pressed =>
        {
            if (!pressed)
                modButton.SetPressedNoSignal(true);
        };
        list.AddChild(modButton);
        _initialFocus = modButton;

        return panel;
    }

    private Control BuildSettingsPanel()
    {
        var margin = CreateInnerMargin();
        margin.Name = "OriginalStyleSettingsPanel";
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        margin.SizeFlagsVertical = SizeFlags.ExpandFill;

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 18);
        root.AddChild(header);

        var heading = CreateHeading(Localize("SHIN_GETTER_CHUNIBYO.TITLE", "Chunibyo Config"), PageTitleFontSize);
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(heading);

        var historyButton = CreateActionButton(
            Localize("SHIN_GETTER_CHUNIBYO.UPDATE_HISTORY", "Update History"),
            ShowUpdateHistory);
        header.AddChild(historyButton);

        string versionText = LocalizeWithVariable(
            "SHIN_GETTER_CHUNIBYO.VERSION",
            "Current version: {Version}",
            "Version",
            ReadManifestVersion());
        var versionLabel = new Label { Text = versionText };
        ApplyKreonFont(versionLabel, 24);
        versionLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.78f, 0.8f));
        root.AddChild(versionLabel);
        root.AddChild(CreateDivider());

        var scroll = new ScrollContainer
        {
            Name = "SettingsScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(scroll);

        var scrollContent = new MarginContainer
        {
            Name = "SettingsScrollContent",
            CustomMinimumSize = new Vector2(720f, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        scroll.AddChild(scrollContent);

        var options = new VBoxContainer
        {
            Name = "SettingsOptions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        options.AddThemeConstantOverride("separation", 18);
        scrollContent.AddChild(options);

        options.AddChild(BuildMainMenuToggle());
        options.AddChild(CreateDivider());
        options.AddChild(BuildVoiceModeRow());
        options.AddChild(CreateDivider());
        options.AddChild(BuildBgmSection());
        options.AddChild(CreateDivider());
        options.AddChild(BuildEventInvasionToggle());
        options.AddChild(CreateDivider());
        options.AddChild(BuildCardExportSection());

        return margin;
    }

    private Control BuildMainMenuToggle()
    {
        var row = CreateSettingRow(
            Localize(
                "SHIN_GETTER_CHUNIBYO.SHOW_IN_MAIN_MENU",
                "Show Chunibyo Config on the main menu"),
            noteText: Localize(
                "SHIN_GETTER_CHUNIBYO.SHOW_IN_MAIN_MENU_NOTE",
                "Requires restarting the game"));
        NShinGetterConfigTickbox toggle = CreateOriginalTickbox(
            ShinGetterChunibyoConfigService.Current.ShowInMainMenu,
            enabled =>
        {
            ShinGetterChunibyoConfigService.Current.ShowInMainMenu = enabled;
            SaveConfigOrShowError();
            if (!enabled)
            {
                ShowPopup(
                    Localize("SHIN_GETTER_CHUNIBYO.HIDDEN_TITLE", "Chunibyo Config entry hidden"),
                    Localize(
                        "SHIN_GETTER_CHUNIBYO.HIDDEN_BODY",
                        "After restarting, the main-menu entry will no longer be shown. You can find it under Settings - Game Settings."));
            }
        });
        row.AddChild(toggle);
        return row;
    }

    private Control BuildVoiceModeRow()
    {
        var row = CreateSettingRow(
            Localize("SHIN_GETTER_CHUNIBYO.VOICE_AMOUNT", "Voice Amount"),
            104f);
        _voiceModePaginator = InstantiateOriginalControl<NShinGetterVoicePaginator>(VoicePaginatorScenePath);
        ConfigureVoicePaginatorLayout(_voiceModePaginator);
        _voiceModePaginator.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        row.AddChild(_voiceModePaginator);
        string silent = Localize(
            "SHIN_GETTER_CHUNIBYO.VOICE.SILENT",
            "[white]Mature Professional[/white]");
        string oncePerCombat = Localize(
            "SHIN_GETTER_CHUNIBYO.VOICE.ONCE_PER_COMBAT",
            "I'm an adult, but hot blood still feels pretty good");
        string always = Localize(
            "SHIN_GETTER_CHUNIBYO.VOICE.ALWAYS",
            "[red][sine]Set Me Ablaze![/sine][/red]");
        _voiceModePaginator.Configure(
            new[]
            {
                silent,
                oncePerCombat,
                always,
            },
            new[]
            {
                StripVoicePresentationTags(silent),
                oncePerCombat,
                StripVoicePresentationTags(always),
            },
            (int)ShinGetterChunibyoConfigService.Current.VoiceMode);
        _voiceModePaginator.IndexChanged += index =>
        {
            ShinGetterChunibyoConfigService.Current.VoiceMode = (ShinGetterVoiceMode)index;
            UpdateVoiceModePresentation();
            SaveConfigOrShowError();
        };
        UpdateVoiceModePresentation();
        return row;
    }

    private Control BuildBgmSection()
    {
        var section = new VBoxContainer
        {
            Name = "BgmSettingsSection",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        section.AddThemeConstantOverride("separation", 12);

        var details = new VBoxContainer
        {
            Name = "BgmSettingsDetails",
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        details.AddThemeConstantOverride("separation", 12);

        section.AddChild(CreateBgmSectionHeader(details));
        section.AddChild(details);
        details.AddChild(BuildBgmTrackRow(
            ShinGetterBgmCategory.Execution,
            "SHIN_GETTER_CHUNIBYO.BGM.EXECUTION",
            "Execution Theme",
            "SHIN_GETTER_CHUNIBYO.BGM.EXECUTION_NOTE",
            "Triggers after a finisher enters your hand from turn two onward."));
        details.AddChild(CreateDivider());
        details.AddChild(BuildBgmTrackRow(
            ShinGetterBgmCategory.NormalCombat,
            "SHIN_GETTER_CHUNIBYO.BGM.NORMAL",
            "Normal Combat"));
        details.AddChild(CreateDivider());
        details.AddChild(BuildBgmTrackRow(
            ShinGetterBgmCategory.EventCombat,
            "SHIN_GETTER_CHUNIBYO.BGM.EVENT",
            "Encounter Combat",
            "SHIN_GETTER_CHUNIBYO.BGM.EVENT_NOTE",
            "Combat entered from a ? room event."));
        details.AddChild(CreateDivider());
        details.AddChild(BuildBgmTrackRow(
            ShinGetterBgmCategory.EliteCombat,
            "SHIN_GETTER_CHUNIBYO.BGM.ELITE",
            "Elite Combat"));
        details.AddChild(CreateDivider());
        details.AddChild(BuildBgmTrackRow(
            ShinGetterBgmCategory.BossCombat,
            "SHIN_GETTER_CHUNIBYO.BGM.BOSS",
            "Boss Combat"));
        details.AddChild(CreateDivider());
        details.AddChild(BuildBgmOtherCharactersToggle());
        return section;
    }

    private Control CreateBgmSectionHeader(Control details)
    {
        var button = new Button
        {
            Name = "BgmSettingsToggle",
            CustomMinimumSize = new Vector2(0f, 72f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            FocusMode = FocusModeEnum.All,
            ToggleMode = true,
            Text = Localize("SHIN_GETTER_CHUNIBYO.BGM.TITLE", "BGM Settings"),
            Alignment = HorizontalAlignment.Left,
        };
        button.AddThemeFontOverride("font", PreloadManager.Cache.GetAsset<Font>(KreonFontPath));
        button.AddThemeFontSizeOverride("font_size", 34);
        button.AddThemeColorOverride("font_color", new Color(0.91f, 0.86f, 0.74f));
        button.AddThemeColorOverride("font_hover_color", StsColors.gold);
        button.AddThemeConstantOverride("outline_size", 4);
        button.AddThemeStyleboxOverride("normal", CreateBgmHeaderStyle(new Color(0.07f, 0.13f, 0.16f, 0.92f)));
        button.AddThemeStyleboxOverride("hover", CreateBgmHeaderStyle(new Color(0.10f, 0.20f, 0.24f, 0.96f)));
        button.AddThemeStyleboxOverride("pressed", CreateBgmHeaderStyle(new Color(0.12f, 0.24f, 0.28f, 0.96f)));
        button.AddThemeStyleboxOverride("focus", CreateBgmHeaderStyle(new Color(0.10f, 0.20f, 0.24f, 0.96f)));

        var arrow = new TextureRect
        {
            Name = "ExpandArrow",
            Texture = ResourceLoader.Load<Texture2D>(SettingsArrowPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        arrow.SetAnchorsPreset(LayoutPreset.CenterRight);
        arrow.OffsetLeft = -52f;
        arrow.OffsetTop = -18f;
        arrow.OffsetRight = -16f;
        arrow.OffsetBottom = 18f;
        arrow.PivotOffset = new Vector2(18f, 18f);
        arrow.Rotation = 0f;
        button.AddChild(arrow);

        button.Pressed += () =>
        {
            details.Visible = button.ButtonPressed;
            arrow.Rotation = button.ButtonPressed ? -Mathf.Pi * 0.5f : 0f;
        };
        return button;
    }

    private Control BuildBgmTrackRow(
        ShinGetterBgmCategory category,
        string labelKey,
        string labelFallback,
        string? noteKey = null,
        string? noteFallback = null)
    {
        string? note = noteKey == null ? null : Localize(noteKey, noteFallback ?? string.Empty);
        var row = CreateSettingRow(
            Localize(labelKey, labelFallback),
            88f,
            note,
            BgmTextColumnMinimumWidth);
        var controls = new HBoxContainer
        {
            Name = category + "Controls",
            CustomMinimumSize = new Vector2(BgmTrackControlsWidth, 64f),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        controls.AddThemeConstantOverride("separation", (int)BgmControlSeparation);
        row.AddChild(controls);

        // The selected option is a real child of this row. Only its expanded list
        // becomes top-level, matching BaseLib's NConfigDropdown behavior.
        var dropdownSlot = new Control
        {
            Name = category + "DropdownSlot",
            CustomMinimumSize = new Vector2(BgmDropdownWidth, 64f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        controls.AddChild(dropdownSlot);

        NShinGetterBgmDropdown dropdown =
            InstantiateOriginalControl<NShinGetterBgmDropdown>(SettingsDropdownScenePath);
        dropdown.Name = category + "Dropdown";
        dropdown.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dropdown.ConfigureLayout(BgmDropdownWidth);
        dropdown.Configure(
            ShinGetterBgmCatalog.Tracks,
            ShinGetterChunibyoConfigService.GetBgmTrackId(category));
        dropdownSlot.AddChild(dropdown);

        var preview = new NShinGetterBgmPreviewControls { Name = category + "Preview" };
        controls.AddChild(preview);
        preview.Configure(category, () => dropdown.SelectedTrack);

        dropdown.TrackChanged += track =>
        {
            preview.OnSelectionChanged();
            ShinGetterChunibyoConfigService.SetBgmTrackId(category, track.Id);
            SaveConfigOrShowError();
        };
        return row;
    }

    private Control BuildBgmOtherCharactersToggle()
    {
        var row = CreateSettingRow(
            Localize("SHIN_GETTER_CHUNIBYO.BGM.OTHER_CHARACTERS", "Enable for other characters"),
            noteText: Localize(
                "SHIN_GETTER_CHUNIBYO.BGM.OTHER_CHARACTERS_NOTE",
                "Also applies these BGM replacements while playing another character."));
        NShinGetterConfigTickbox toggle = CreateOriginalTickbox(
            ShinGetterChunibyoConfigService.Current.BgmForOtherCharacters,
            enabled =>
            {
                ShinGetterChunibyoConfigService.Current.BgmForOtherCharacters = enabled;
                SaveConfigOrShowError();
            });
        row.AddChild(toggle);
        return row;
    }

    private Control BuildEventInvasionToggle()
    {
        var row = CreateSettingRow(
            Localize("SHIN_GETTER_CHUNIBYO.EVENT_INVASION", "Event Invasion"),
            noteText: Localize(
                "SHIN_GETTER_CHUNIBYO.EVENT_INVASION_NOTE",
                "Adds extra options to some events in ? rooms."));
        NShinGetterConfigTickbox toggle = CreateOriginalTickbox(
            ShinGetterChunibyoConfigService.Current.EventInvasionEnabled,
            enabled =>
        {
            ShinGetterChunibyoConfigService.Current.EventInvasionEnabled = enabled;
            SaveConfigOrShowError();
        });
        row.AddChild(toggle);
        return row;
    }

    private Control BuildCardExportSection()
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 10);
        section.AddChild(CreateHeading(Localize("SHIN_GETTER_CHUNIBYO.CARD_EXPORT", "Card Export"), 34));

        var pathRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pathRow.AddThemeConstantOverride("separation", 12);
        section.AddChild(pathRow);

        _exportPathLabel = new Label
        {
            Text = ShinGetterChunibyoConfigService.GetCardExportDirectory(),
            CustomMinimumSize = new Vector2(0f, 52f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        ApplyKreonFont(_exportPathLabel, 22);
        pathRow.AddChild(_exportPathLabel);

        pathRow.AddChild(CreateActionButton(
            Localize("SHIN_GETTER_CHUNIBYO.BROWSE", "Browse"),
            OpenFolderDialog));

        var exportButton = CreateActionButton(
            Localize("SHIN_GETTER_CHUNIBYO.EXPORT", "Export Cards"),
            ExportCards,
            new HoverTip(new LocString(LocTable, "SHIN_GETTER_CHUNIBYO.EXPORT_TOOLTIP")));
        exportButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        section.AddChild(exportButton);

        return section;
    }

    private void OpenFolderDialog()
    {
        if (_folderDialog == null)
        {
            _folderDialog = new FileDialog
            {
                Name = "CardExportFolderDialog",
                Access = FileDialog.AccessEnum.Filesystem,
                FileMode = FileDialog.FileModeEnum.OpenDir,
                UseNativeDialog = true,
                Title = Localize("SHIN_GETTER_CHUNIBYO.CARD_EXPORT", "Card Export"),
            };
            _folderDialog.DirSelected += OnExportDirectorySelected;
            AddChild(_folderDialog);
        }

        _folderDialog.CurrentDir = ShinGetterChunibyoConfigService.GetCardExportDirectory();
        _folderDialog.PopupCenteredRatio(0.72f);
    }

    private void OnExportDirectorySelected(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        ShinGetterChunibyoConfigService.Current.CardExportDirectory = Path.GetFullPath(path);
        if (_exportPathLabel != null)
            _exportPathLabel.Text = ShinGetterChunibyoConfigService.Current.CardExportDirectory;
        SaveConfigOrShowError();
    }

    private void ExportCards()
    {
        string outputDirectory = ShinGetterChunibyoConfigService.GetCardExportDirectory();
        if (!ShinGetterCardPngExporter.TryNormalizeOutputDirectory(outputDirectory, out string normalized, out string error))
        {
            ShowPopup(Localize("SHIN_GETTER_CHUNIBYO.EXPORT_ERROR_TITLE", "Unable to export cards"), error);
            return;
        }

        if (!ShinGetterCardPngExporter.TryValidateExportEnvironment(out error))
        {
            ShowPopup(Localize("SHIN_GETTER_CHUNIBYO.EXPORT_ERROR_TITLE", "Unable to export cards"), error);
            return;
        }

        var request = ShinGetterCardPngExportRequest.CreateDefault("SHIN_GETTER", normalized) with
        {
            Scale = 2f,
            IncludeUpgradedVariants = true,
        };

        ShinGetterCardPngExporter.BeginExport(request, message => GD.Print($"[ShinGetterCardExport] {message}"));
        ShowPopup(
            Localize("SHIN_GETTER_CHUNIBYO.EXPORT_STARTED_TITLE", "Card export started"),
            LocalizeWithVariable(
                "SHIN_GETTER_CHUNIBYO.EXPORT_STARTED_BODY",
                "Exporting Shin Getter cards and upgrades at 2x resolution.\nOutput: {Path}",
                "Path",
                normalized));
    }

    private void ShowUpdateHistory()
    {
        var entries = ReadUpdateHistory()
            .OrderByDescending(entry => entry.Date, StringComparer.Ordinal)
            .ThenByDescending(entry => entry.Version, StringComparer.Ordinal)
            .ToList();

        var body = new StringBuilder();
        foreach (UpdateHistoryEntry entry in entries)
        {
            if (body.Length > 0)
                body.AppendLine().AppendLine();
            body.Append(entry.Version).Append("  ").AppendLine(entry.Date);
            body.Append(Localize(entry.LocalizationKey, entry.Version));
        }

        ShowUpdateHistoryPopup(
            Localize("SHIN_GETTER_CHUNIBYO.UPDATE_TITLE", "Shin Getter Update History"),
            body.Length == 0 ? ReadManifestVersion() : body.ToString());
    }

    private void SaveConfigOrShowError()
    {
        if (!ShinGetterChunibyoConfigService.Save(out string error))
            ShowPopup(Localize("SHIN_GETTER_CHUNIBYO.SAVE_ERROR_TITLE", "Unable to save config"), error);
    }

    private void UpdateVoiceModePresentation()
    {
        if (_voiceModePaginator == null)
            return;

        ShinGetterVoiceMode mode = ShinGetterChunibyoConfigService.Current.VoiceMode;
        string tooltipKey = mode switch
        {
            ShinGetterVoiceMode.Silent => "SHIN_GETTER_CHUNIBYO.VOICE.SILENT_TOOLTIP",
            ShinGetterVoiceMode.Always => "SHIN_GETTER_CHUNIBYO.VOICE.ALWAYS_TOOLTIP",
            _ => "SHIN_GETTER_CHUNIBYO.VOICE.ONCE_PER_COMBAT_TOOLTIP",
        };
        _voiceModePaginator.SetHoverTip(new HoverTip(new LocString(LocTable, tooltipKey)));
    }

    private static PanelContainer CreatePanel(Vector2 minimumSize)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.075f, 0.09f, 0.96f),
            BorderColor = new Color(0.22f, 0.34f, 0.39f, 0.95f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };

        var panel = new PanelContainer { CustomMinimumSize = minimumSize };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static StyleBoxFlat CreateBgmHeaderStyle(Color background) =>
        new()
        {
            BgColor = background,
            BorderColor = new Color(0.22f, 0.34f, 0.39f, 0.95f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 22f,
            ContentMarginRight = 62f,
        };

    private static MarginContainer CreateInnerMargin()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        return margin;
    }

    private static Label CreateHeading(string text, int fontSize)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        ApplyKreonFont(label, fontSize);
        label.AddThemeColorOverride("font_color", new Color(0.91f, 0.86f, 0.74f));
        return label;
    }

    private static HSeparator CreateDivider()
    {
        var divider = new HSeparator { CustomMinimumSize = new Vector2(0f, 2f) };
        divider.AddThemeConstantOverride("separation", 2);
        return divider;
    }

    private static HBoxContainer CreateSettingRow(
        string labelText,
        float minimumHeight = 72f,
        string? noteText = null,
        float minimumTextWidth = 0f)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, minimumHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 18);

        var textColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(minimumTextWidth, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        textColumn.AddThemeConstantOverride("separation", 2);

        var label = new Label
        {
            Text = labelText,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyKreonFont(label, SettingFontSize);
        textColumn.AddChild(label);

        if (!string.IsNullOrWhiteSpace(noteText))
        {
            var note = new Label
            {
                Text = noteText,
                CustomMinimumSize = new Vector2(minimumTextWidth, 0f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            ApplyKreonFont(note, NoteFontSize);
            note.AddThemeColorOverride("font_color", new Color(0.68f, 0.72f, 0.75f));
            textColumn.AddChild(note);
        }

        row.AddChild(textColumn);
        return row;
    }

    private static NShinGetterConfigTickbox CreateOriginalTickbox(bool isTicked, Action<bool> onChanged)
    {
        var tickbox = InstantiateOriginalControl<NShinGetterConfigTickbox>(ConfigTickboxScenePath);
        tickbox.CustomMinimumSize = new Vector2(320f, 64f);
        tickbox.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        tickbox.InitialIsTicked = isTicked;
        tickbox.Connect(
            NTickbox.SignalName.Toggled,
            Callable.From<NTickbox>(changed => onChanged(changed.IsTicked)));
        return tickbox;
    }

    private static void ConfigureVoicePaginatorLayout(NShinGetterVoicePaginator paginator)
    {
        paginator.CustomMinimumSize = new Vector2(SettingControlWidth, 104f);
        foreach (string path in new[] { "LabelContainer/Mask/Label", "LabelContainer/Mask/VfxLabel" })
        {
            var label = paginator.GetNode<MegaCrit.Sts2.addons.mega_text.MegaLabel>(path);
            label.AddThemeFontSizeOverride("font_size", 28);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.MinFontSize = 16;
            label.MaxFontSize = 28;
        }
    }

    private static T InstantiateOriginalControl<T>(string scenePath) where T : Control, new()
    {
        Control template = ResourceLoader.Load<PackedScene>(scenePath).Instantiate<Control>();
        var control = new T
        {
            Name = template.Name,
            CustomMinimumSize = template.CustomMinimumSize,
            FocusMode = template.FocusMode,
            MouseFilter = template.MouseFilter,
            SizeFlagsHorizontal = template.SizeFlagsHorizontal,
            SizeFlagsVertical = template.SizeFlagsVertical,
        };

        while (template.GetChildCount() > 0)
        {
            Node child = template.GetChild(0);
            ClearSceneOwner(child);
            child.Reparent(control, keepGlobalTransform: false);
            ReassignSceneOwner(child, control);
        }

        template.Free();
        return control;
    }

    private static void ClearSceneOwner(Node node)
    {
        foreach (Node child in node.GetChildren())
            ClearSceneOwner(child);
        node.Owner = null;
    }

    private static void ReassignSceneOwner(Node node, Node owner)
    {
        node.Owner = owner;
        foreach (Node child in node.GetChildren())
            ReassignSceneOwner(child, owner);
    }

    private static NShinGetterConfigActionButton CreateActionButton(
        string text,
        Action action,
        IHoverTip? hoverTip = null)
    {
        var button = new NShinGetterConfigActionButton();
        button.Initialize(text, action, hoverTip);
        return button;
    }

    private static void AddSelectedModButtonStyles(Button button)
    {
        var transparent = new StyleBoxEmpty();
        button.AddThemeStyleboxOverride("normal", transparent);
        button.AddThemeStyleboxOverride("hover", transparent);
        button.AddThemeStyleboxOverride("pressed", transparent);
        button.AddThemeStyleboxOverride("hover_pressed", transparent);
        button.AddThemeStyleboxOverride("focus", transparent);
    }

    private static StyleBoxFlat CreateModButtonStyle(Color background)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = StsColors.gold,
            BorderWidthLeft = 4,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 16f,
            ContentMarginRight = 16f,
        };
    }

    private static void ApplyKreonFont(Control control, int fontSize)
    {
        control.AddThemeFontOverride("font", PreloadManager.Cache.GetAsset<Font>(KreonFontPath));
        control.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private static void ShowPopup(string title, string body)
    {
        NErrorPopup? popup = NErrorPopup.Create(title, body, showReportBugButton: false);
        if (popup != null && NModalContainer.Instance != null)
            NModalContainer.Instance.Add(popup);
        else
            GD.Print($"[ShinGetterChunibyo] {title}: {body}");
    }

    private void ShowUpdateHistoryPopup(string title, string body)
    {
        NModalContainer? modalContainer = NModalContainer.Instance;
        if (modalContainer == null)
        {
            GD.Print($"[ShinGetterChunibyo] {title}: {body}");
            return;
        }

        Control? returnFocus = GetViewport().GuiGetFocusOwner();
        modalContainer.Add(NChunibyoUpdateHistoryPopup.Create(title, body, returnFocus));
    }

    private void OnConfigVisibilityChanged()
    {
        if (!IsVisibleInTree())
            ShinGetterBgmPreviewService.Stop();
    }

    private static string Localize(string key, string fallback)
    {
        return LocString.GetIfExists(LocTable, key)?.GetFormattedText() ?? fallback;
    }

    private static string StripVoicePresentationTags(string text)
    {
        return text
            .Replace("[white]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[/white]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[color=white]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[/color]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[red]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[/red]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[sine]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[/sine]", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string LocalizeWithVariable(
        string key,
        string fallback,
        string variableName,
        string variableValue)
    {
        LocString? localized = LocString.GetIfExists(LocTable, key);
        if (localized == null)
            return fallback.Replace("{" + variableName + "}", variableValue, StringComparison.Ordinal);

        localized.Add(variableName, variableValue);
        return localized.GetFormattedText();
    }

    private static string ReadManifestVersion()
    {
        try
        {
            string json = Godot.FileAccess.GetFileAsString(ManifestPath);
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.GetProperty("version").GetString() ?? "unknown";
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Shin Getter could not read manifest version: {ex.Message}");
            return "unknown";
        }
    }

    private static IEnumerable<UpdateHistoryEntry> ReadUpdateHistory()
    {
        try
        {
            string json = Godot.FileAccess.GetFileAsString(UpdateHistoryPath);
            return JsonSerializer.Deserialize<List<UpdateHistoryEntry>>(json) ?? new List<UpdateHistoryEntry>();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Shin Getter could not read update history: {ex.Message}");
            return Array.Empty<UpdateHistoryEntry>();
        }
    }

    private sealed class UpdateHistoryEntry
    {
        public UpdateHistoryEntry()
        {
        }

        [System.Text.Json.Serialization.JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("localization_key")]
        public string LocalizationKey { get; set; } = string.Empty;
    }
}
