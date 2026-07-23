#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

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
    public ShinGetterVoiceMode VoiceMode { get; set; } = ShinGetterVoiceMode.OncePerCombat;
    public bool EventInvasionEnabled { get; set; } = true;
    public string CardExportDirectory { get; set; } = string.Empty;
}

public static class ShinGetterChunibyoConfigService
{
    private const string ConfigDirectoryName = "mod_configs";
    private const string ConfigFileName = "shin_getter_chunibyo.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static bool _loaded;

    public static ShinGetterChunibyoConfig Current { get; private set; } = new();

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
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Shin Getter could not load chunibyo config: {ex.Message}");
            Current = new();
        }
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

    private static string GetConfigPath()
    {
        return Path.Combine(OS.GetUserDataDir(), ConfigDirectoryName, ConfigFileName);
    }
}
