#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Config;

public enum ShinGetterVoiceMode
{
    Silent,
    OncePerCombat,
    Always,
}

public sealed class ShinGetterChunibyoConfig
{
    public bool ShowInMainMenu { get; set; } = true;
    public string LastReadUpdateVersion { get; set; } = string.Empty;
    public ShinGetterVoiceMode VoiceMode { get; set; } = ShinGetterVoiceMode.OncePerCombat;
    public bool BgmEnabled { get; set; } = true;
    public string ExecutionBgmTrackId { get; set; } = ShinGetterBgmCatalog.DefaultTrackId;
    public string NormalCombatBgmTrackId { get; set; } = ShinGetterBgmCatalog.DefaultTrackId;
    public string EventCombatBgmTrackId { get; set; } = ShinGetterBgmCatalog.DefaultTrackId;
    public string EliteCombatBgmTrackId { get; set; } = ShinGetterBgmCatalog.DefaultTrackId;
    public string BossCombatBgmTrackId { get; set; } = ShinGetterBgmCatalog.DefaultTrackId;
    public bool BgmForOtherCharacters { get; set; }
    public bool EventInvasionEnabled { get; set; } = true;
    public string CardExportDirectory { get; set; } = string.Empty;
}

public static class ShinGetterChunibyoConfigService
{
    private const string ConfigDirectoryName = "mod_configs";
    private const string ConfigFileName = "shin_getter_chunibyo.json";
    private const string ManifestPath = "res://ShinGetterMod.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static bool _loaded;

    internal static event Action? UpdateReadStateChanged;
    internal static event Action? BgmEnabledChanged;

    internal static bool IsBgmEnabled
    {
        get { Load(); return Current.BgmEnabled; }
    }

    public static ShinGetterChunibyoConfig Current { get; private set; } = new();

    internal static string CurrentManifestVersion => ReadCurrentManifestVersion();

    internal static bool IsCurrentUpdateUnread
    {
        get
        {
            Load();
            string currentVersion = CurrentManifestVersion;
            return currentVersion.Length > 0
                && !string.Equals(
                    NormalizeVersion(Current.LastReadUpdateVersion),
                    currentVersion,
                    StringComparison.Ordinal);
        }
    }

    public static void Load()
    {
        if (_loaded)
            return;

        _loaded = true;
        try
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
                return;

            string json = File.ReadAllText(path);
            Current = JsonSerializer.Deserialize<ShinGetterChunibyoConfig>(json, JsonOptions) ?? new();
            NormalizeBgmSelections(Current);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Shin Getter could not load chunibyo config: {ex.Message}");
            Current = new();
        }
    }

    // Startup migration is in-memory: never overwrite unrelated config or fail startup
    // when the config file is read-only. The next successful Save persists the repair.
    internal static void NormalizeBgmSelections(ShinGetterChunibyoConfig config)
    {
        config.ExecutionBgmTrackId = ShinGetterBgmCatalog.ResolveOrDefault(config.ExecutionBgmTrackId).Id;
        config.NormalCombatBgmTrackId = ShinGetterBgmCatalog.ResolveOrDefault(config.NormalCombatBgmTrackId).Id;
        config.EventCombatBgmTrackId = ShinGetterBgmCatalog.ResolveOrDefault(config.EventCombatBgmTrackId).Id;
        config.EliteCombatBgmTrackId = ShinGetterBgmCatalog.ResolveOrDefault(config.EliteCombatBgmTrackId).Id;
        config.BossCombatBgmTrackId = ShinGetterBgmCatalog.ResolveOrDefault(config.BossCombatBgmTrackId).Id;
    }

    public static bool Save(out string error)
    {
        Load();
        try
        {
            string path = GetConfigPath();
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            GD.PushError($"Shin Getter could not save chunibyo config: {ex}");
            return false;
        }
    }

    internal static bool TrySetBgmEnabled(bool enabled, out string error)
    {
        Load();
        bool previous = Current.BgmEnabled;
        if (previous == enabled) { error = string.Empty; return true; }
        Current.BgmEnabled = enabled;
        if (!Save(out error))
        {
            Current.BgmEnabled = previous;
            return false;
        }

        // Only a persisted change affects playback. Keep all per-category selections.
        // Re-enable permits the next normal trigger; it does not replay a consumed finisher.
        if (!enabled)
        {
            ShinGetterBgmPreviewService.Stop();
            ShinGetterEncounterMusicService.StopActiveAndRestore();
            ShinGetterExecutionMusicService.StopImmediatelyAndRestore();
        }
        BgmEnabledChanged?.Invoke();
        return true;
    }

    internal static bool MarkCurrentUpdateRead(out string error)
    {
        Load();
        string currentVersion = CurrentManifestVersion;
        if (currentVersion.Length == 0)
        {
            error = "ShinGetterMod.json.version";
            return false;
        }

        string previousVersion = Current.LastReadUpdateVersion;
        if (string.Equals(NormalizeVersion(previousVersion), currentVersion, StringComparison.Ordinal))
        {
            error = string.Empty;
            return true;
        }

        Current.LastReadUpdateVersion = currentVersion;
        if (!Save(out error))
        {
            Current.LastReadUpdateVersion = previousVersion;
            return false;
        }

        UpdateReadStateChanged?.Invoke();
        return true;
    }

    public static string GetDefaultCardExportDirectory()
    {
        return Path.Combine(OS.GetUserDataDir(), "shin_getter_cards");
    }

    public static string GetCardExportDirectory()
    {
        Load();
        return string.IsNullOrWhiteSpace(Current.CardExportDirectory)
            ? GetDefaultCardExportDirectory()
            : Current.CardExportDirectory;
    }

    internal static string GetBgmTrackId(ShinGetterBgmCategory category)
    {
        Load();
        return category switch
        {
            ShinGetterBgmCategory.Execution => Current.ExecutionBgmTrackId,
            ShinGetterBgmCategory.NormalCombat => Current.NormalCombatBgmTrackId,
            ShinGetterBgmCategory.EventCombat => Current.EventCombatBgmTrackId,
            ShinGetterBgmCategory.EliteCombat => Current.EliteCombatBgmTrackId,
            ShinGetterBgmCategory.BossCombat => Current.BossCombatBgmTrackId,
            _ => ShinGetterBgmCatalog.DefaultTrackId,
        };
    }

    internal static void SetBgmTrackId(ShinGetterBgmCategory category, string trackId)
    {
        string normalized = ShinGetterBgmCatalog.ResolveOrDefault(trackId).Id;
        switch (category)
        {
            case ShinGetterBgmCategory.Execution:
                Current.ExecutionBgmTrackId = normalized;
                break;
            case ShinGetterBgmCategory.NormalCombat:
                Current.NormalCombatBgmTrackId = normalized;
                break;
            case ShinGetterBgmCategory.EventCombat:
                Current.EventCombatBgmTrackId = normalized;
                break;
            case ShinGetterBgmCategory.EliteCombat:
                Current.EliteCombatBgmTrackId = normalized;
                break;
            case ShinGetterBgmCategory.BossCombat:
                Current.BossCombatBgmTrackId = normalized;
                break;
        }
    }

    private static string GetConfigPath()
    {
        return Path.Combine(OS.GetUserDataDir(), ConfigDirectoryName, ConfigFileName);
    }

    private static string ReadCurrentManifestVersion()
    {
        try
        {
            string json = Godot.FileAccess.GetFileAsString(ManifestPath);
            using JsonDocument document = JsonDocument.Parse(json);
            return NormalizeVersion(document.RootElement.GetProperty("version").GetString());
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Shin Getter could not read manifest version: {ex.Message}");
            return string.Empty;
        }
    }

    private static string NormalizeVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? string.Empty : version.Trim();
}
