using System.Text.Json;
using System.Text.Json.Serialization;
using Win11UpdateBlocker.Core.Models;

namespace Win11UpdateBlocker.Core.Config;

public static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static string ConfigDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AppMetadata.ConfigFolderName);

    private static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    public static BlockerConfig Load()
    {
        EnsureDirectoryExists();

        if (!File.Exists(ConfigFilePath))
        {
            return CreateDefaultConfig();
        }

        var json = File.ReadAllText(ConfigFilePath);
        var config = JsonSerializer.Deserialize<BlockerConfig>(json, JsonOptions) ?? CreateDefaultConfig();
        NormalizeConfig(config);
        MigrateSettings(config);
        return config;
    }

    public static void Save(BlockerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        NormalizeConfig(config);

        EnsureDirectoryExists();

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    private static void NormalizeConfig(BlockerConfig config)
    {
        config.Preferences ??= UpdatePreferences.FromLegacyMode(config.Mode);
        config.Mode = config.Preferences.ToLegacyMode();
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(ConfigDirectory))
        {
            Directory.CreateDirectory(ConfigDirectory);
        }
    }

    private static BlockerConfig CreateDefaultConfig() => new()
    {
        Mode = UpdateBlockMode.AllowAll,
        Preferences = UpdatePreferences.CreateAllowAll(),
        TrayEnabled = true,
        AutostartEnabled = true,
        BackgroundServiceEnabled = true,
        SettingsVersion = 2
    };

    private static void MigrateSettings(BlockerConfig config)
    {
        if (config.SettingsVersion >= 2)
        {
            return;
        }

        config.BackgroundServiceEnabled = true;
        config.AutostartEnabled = true;
        config.TrayEnabled = true;
        config.SettingsVersion = 2;
        Save(config);
    }
}
