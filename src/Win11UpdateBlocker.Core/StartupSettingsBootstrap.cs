using Win11UpdateBlocker.Core.Config;
using Win11UpdateBlocker.Core.Logging;

namespace Win11UpdateBlocker.Core;

public static class StartupSettingsBootstrap
{
    public static void Initialize()
    {
        BackgroundServiceStarter.EnsureRunningIfConfigured();

        var config = ConfigStore.Load();

        try
        {
            AppSettingsManager.ApplySettings(
                config.BackgroundServiceEnabled,
                config.AutostartEnabled,
                config.TrayEnabled);
        }
        catch (Exception ex)
        {
            FileLogger.Log($"StartupSettingsBootstrap: apply failed — {ex.Message}");
        }
    }
}
