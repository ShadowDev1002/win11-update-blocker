using System.ServiceProcess;
using Microsoft.Win32;
using Win11UpdateBlocker.Core.Config;
using Win11UpdateBlocker.Core.Logging;

namespace Win11UpdateBlocker.Core;

public static class AppSettingsManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ServiceName = AppMetadata.ServiceInternalName;
    private static readonly TimeSpan ServiceQueryTimeout = TimeSpan.FromMilliseconds(800);

    public static bool IsBackgroundServiceRunning() =>
        QueryServiceStatus(controller => controller.Status == ServiceControllerStatus.Running);

    public static bool IsBackgroundServiceInstalled() =>
        QueryServiceStatus(_ => true);

    public static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(AppMetadata.AutostartRegistryValueName) is not null
               || key?.GetValue(AppMetadata.LegacyAutostartRegistryValueName) is not null;
    }

    public static void ApplySettings(bool backgroundServiceEnabled, bool autostartEnabled, bool trayEnabled)
    {
        ApplyAutostart(autostartEnabled);

        var config = ConfigStore.Load();
        config.BackgroundServiceEnabled = backgroundServiceEnabled;
        config.AutostartEnabled = autostartEnabled;
        config.TrayEnabled = trayEnabled;
        ConfigStore.Save(config);

        FileLogger.Log(
            $"AppSettingsManager: service={backgroundServiceEnabled}, autostart={autostartEnabled}, tray={trayEnabled}.");
    }

    public static void RemoveAllAppSettings()
    {
        ApplyAutostart(false);

        var config = ConfigStore.Load();
        config.BackgroundServiceEnabled = false;
        config.AutostartEnabled = false;
        config.TrayEnabled = false;
        ConfigStore.Save(config);
    }

    private static void ApplyAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                        ?? throw new InvalidOperationException("Autostart registry key could not be opened.");

        if (!enabled)
        {
            key.DeleteValue(AppMetadata.AutostartRegistryValueName, throwOnMissingValue: false);
            key.DeleteValue(AppMetadata.LegacyAutostartRegistryValueName, throwOnMissingValue: false);
            FileLogger.Log("AppSettingsManager: autostart disabled.");
            return;
        }

        key.DeleteValue(AppMetadata.LegacyAutostartRegistryValueName, throwOnMissingValue: false);
        var exePath = AppPaths.GetGuiExecutablePath();
        key.SetValue(AppMetadata.AutostartRegistryValueName, $"\"{exePath}\"");
        FileLogger.Log("AppSettingsManager: autostart enabled.");
    }

    private static bool QueryServiceStatus(Func<ServiceController, bool> predicate)
    {
        try
        {
            return RunWithTimeout(() =>
            {
                using var controller = new ServiceController(ServiceName);
                return predicate(controller);
            });
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static T RunWithTimeout<T>(Func<T> action)
    {
        var task = Task.Run(action);
        return task.Wait(ServiceQueryTimeout) ? task.Result : default!;
    }
}
