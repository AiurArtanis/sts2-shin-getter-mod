#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using ShinGetterMod.Config;
using ShinGetterMod.Diagnostics.CardExport;

namespace ShinGetterMod.Nodes.Config;

public partial class NChunibyoConfigSubmenu : NSubmenu
{
    private const string ManifestPath = "res://ShinGetterMod.json";
    private const string UpdateHistoryPath = "res://ShinGetterMod/update_history.json";
    private const string CharacterIconPath = "res://images/ui/top_panel/character_icon_shin_getter.png";
    private const string KreonFontPath = "res://themes/kreon_regular_shared.tres";
    private const string LocTable = "settings_ui";
    private const int PageTitleFontSize = 52;
    private const int SidebarTitleFontSize = 48;
    private const int SettingFontSize = 28;
    private const int OptionFontSize = 27;
    private const int ActionFontSize = 26;
    private const int NoteFontSize = 21;
    private const float SettingControlWidth = 560f;

    private Control? _initialFocus;
    private Label? _exportPathLabel;
    private OptionButton? _voiceModeDropdown;
    private FileDialog? _folderDialog;

    protected override Control? InitialFocusedControl => _initialFocus;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildInterface();
        ConnectSignals();
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

        columns.AddChild(BuildModList());
        columns.AddChild(BuildSettingsPanel());

        var backButton = PreloadManager.Cache
            .GetScene(SceneHelper.GetScenePath("ui/back_button"))
            .Instantiate<NBackButton>();
        backButton.Name = "BackButton";
        backButton.ZIndex = 100;
        backButton.ZAsRelative = false;
        AddChild(backButton);
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
        var panel = CreatePanel(Vector2.Zero);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateInnerMargin();
        panel.AddChild(margin);

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

        var options = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(720f, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        options.AddThemeConstantOverride("separation", 18);
        scroll.AddChild(options);

        options.AddChild(BuildMainMenuToggle());
        options.AddChild(CreateDivider());
        options.AddChild(BuildVoiceModeRow());
        options.AddChild(CreateDivider());
        options.AddChild(BuildEventInvasionToggle());
        options.AddChild(CreateDivider());
        options.AddChild(BuildCardExportSection());

        return panel;
    }

    private Control BuildMainMenuToggle()
    {
        var row = CreateSettingRow(Localize(
            "SHIN_GETTER_CHUNIBYO.SHOW_IN_MAIN_MENU",
            "Show Chunibyo Config on the main menu (restart required)"));
        var toggle = new CheckButton
        {
            ButtonPressed = ShinGetterChunibyoConfigService.Current.ShowInMainMenu,
            CustomMinimumSize = new Vector2(SettingControlWidth, 64f),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
        };
        toggle.Toggled += enabled =>
        {
            ShinGetterChunibyoConfigService.Current.ShowInMainMenu = enabled;
            SaveConfigOrShowError();
            if (!enabled)
            {
                ShowPopup(
                    Localize("SHIN_GETTER_CHUNIBYO.HIDDEN_TITLE", "Chunibyo Config entry hidden"),
                    Localize(
                        "SHIN_GETTER_CHUNIBYO.HIDDEN_BODY",
                        "The entry will be hidden after restarting. Enter chunibyo on in the console to restore it."));
            }
        };
        row.AddChild(toggle);
        return row;
    }

    private Control BuildVoiceModeRow()
    {
        var row = CreateSettingRow(Localize("SHIN_GETTER_CHUNIBYO.VOICE_AMOUNT", "Voice Amount"));
        _voiceModeDropdown = new OptionButton
        {
            CustomMinimumSize = new Vector2(SettingControlWidth, 64f),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            FocusMode = FocusModeEnum.All,
        };
        ApplyKreonFont(_voiceModeDropdown, OptionFontSize);
        _voiceModeDropdown.AddItem(
            StripColorTags(Localize("SHIN_GETTER_CHUNIBYO.VOICE.SILENT", "[white]Mature Professional[/white]")),
            (int)ShinGetterVoiceMode.Silent);
        _voiceModeDropdown.AddItem(
            Localize("SHIN_GETTER_CHUNIBYO.VOICE.ONCE_PER_COMBAT", "I'm an adult, but hot blood still feels pretty good"),
            (int)ShinGetterVoiceMode.OncePerCombat);
        _voiceModeDropdown.AddItem(
            StripColorTags(Localize("SHIN_GETTER_CHUNIBYO.VOICE.ALWAYS", "[red]Set Me Ablaze![/red]")),
            (int)ShinGetterVoiceMode.Always);
        _voiceModeDropdown.Select((int)ShinGetterChunibyoConfigService.Current.VoiceMode);
        UpdateVoiceModePresentation();
        _voiceModeDropdown.ItemSelected += index =>
        {
            ShinGetterChunibyoConfigService.Current.VoiceMode = (ShinGetterVoiceMode)_voiceModeDropdown.GetItemId((int)index);
            UpdateVoiceModePresentation();
            SaveConfigOrShowError();
        };
        row.AddChild(_voiceModeDropdown);
        return row;
    }

    private Control BuildEventInvasionToggle()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);

        var row = CreateSettingRow(Localize("SHIN_GETTER_CHUNIBYO.EVENT_INVASION", "Event Invasion"));
        var toggle = new CheckButton
        {
            ButtonPressed = ShinGetterChunibyoConfigService.Current.EventInvasionEnabled,
            CustomMinimumSize = new Vector2(SettingControlWidth, 64f),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
        };
        toggle.Toggled += enabled =>
        {
            ShinGetterChunibyoConfigService.Current.EventInvasionEnabled = enabled;
            SaveConfigOrShowError();
        };
        row.AddChild(toggle);
        box.AddChild(row);

        var note = new Label
        {
            Text = Localize(
                "SHIN_GETTER_CHUNIBYO.EVENT_INVASION_NOTE",
                "Stores the global option for future event integration."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        ApplyKreonFont(note, NoteFontSize);
        note.AddThemeColorOverride("font_color", new Color(0.68f, 0.72f, 0.75f));
        box.AddChild(note);

        return box;
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
            ExportCards);
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

        ShowPopup(
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
        if (_voiceModeDropdown == null)
            return;

        ShinGetterVoiceMode mode = ShinGetterChunibyoConfigService.Current.VoiceMode;
        Color color = mode switch
        {
            ShinGetterVoiceMode.Silent => Colors.White,
            ShinGetterVoiceMode.Always => new Color(0.96f, 0.28f, 0.2f),
            _ => new Color(0.91f, 0.86f, 0.74f),
        };
        _voiceModeDropdown.AddThemeColorOverride("font_color", color);
        _voiceModeDropdown.TooltipText = mode switch
        {
            ShinGetterVoiceMode.Silent => Localize(
                "SHIN_GETTER_CHUNIBYO.VOICE.SILENT_TOOLTIP",
                "Disables voice lines. Transformation sound effects remain enabled."),
            ShinGetterVoiceMode.Always => Localize(
                "SHIN_GETTER_CHUNIBYO.VOICE.ALWAYS_TOOLTIP",
                "Plays a line every time its trigger condition is met."),
            _ => Localize(
                "SHIN_GETTER_CHUNIBYO.VOICE.ONCE_PER_COMBAT_TOOLTIP",
                "Each voice line plays at most once per combat."),
        };
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

    private static HBoxContainer CreateSettingRow(string labelText)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0f, 64f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 18);

        var label = new Label
        {
            Text = labelText,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyKreonFont(label, SettingFontSize);
        row.AddChild(label);
        return row;
    }

    private static Button CreateActionButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(190f, 52f),
            FocusMode = FocusModeEnum.All,
        };
        ApplyKreonFont(button, ActionFontSize);
        button.Pressed += action;
        return button;
    }

    private static void AddSelectedModButtonStyles(Button button)
    {
        button.AddThemeStyleboxOverride("normal", CreateModButtonStyle(new Color(0.15f, 0.15f, 0.15f, 0.5f)));
        button.AddThemeStyleboxOverride("hover", CreateModButtonStyle(new Color(0.2f, 0.2f, 0.2f, 0.65f)));
        button.AddThemeStyleboxOverride("pressed", CreateModButtonStyle(new Color(0.2f, 0.2f, 0.2f, 0.7f)));
        button.AddThemeStyleboxOverride("focus", CreateModButtonStyle(new Color(0.2f, 0.2f, 0.2f, 0.65f)));
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

    private static string Localize(string key, string fallback)
    {
        return LocString.GetIfExists(LocTable, key)?.GetFormattedText() ?? fallback;
    }

    private static string StripColorTags(string text)
    {
        return text
            .Replace("[white]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[/white]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[red]", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("[/red]", string.Empty, StringComparison.OrdinalIgnoreCase);
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
