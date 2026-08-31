using System.ServiceProcess;
using Microsoft.Win32;
using Win11UpdateBlocker.Core.Logging;
using Win11UpdateBlocker.Core.Models;

namespace Win11UpdateBlocker.Core.Windows;

public static class ServiceManager
{
    private static readonly string[] ManagedServices = ["wuauserv", "UsoSvc"];

    private const int StartAutomatic = 2;
    private const int StartManual = 3;
    private const int StartDisabled = 4;

    private static readonly TimeSpan ServiceOperationTimeout = TimeSpan.FromSeconds(30);

    public static void ApplyMode(UpdateBlockMode mode) =>
        ApplyPreferences(UpdatePreferences.FromLegacyMode(mode));

    public static void ApplyPreferences(UpdatePreferences preferences)
    {
        WindowsAdmin.EnsureCanModifySystem();
        BackupStore.SaveOriginals();
        RestoreDefaults();

        if (preferences.IsFullyBlocked() || preferences.ShouldHideWindowsUpdateUi())
        {
            foreach (var serviceName in ManagedServices)
            {
                StopService(serviceName);
                SetServiceStartType(serviceName, StartDisabled);
                BackupStore.RecordManagedService(serviceName);
                FileLogger.Log($"ServiceManager: stopped and disabled {serviceName}.");
            }

            return;
        }

        foreach (var serviceName in ManagedServices)
        {
            RestoreServiceDefaults(serviceName);
            BackupStore.RecordManagedService(serviceName);
        }

        FileLogger.Log("ServiceManager: ensured Windows Update services are enabled.");
    }

    public static void RestoreDefaults()
    {
        WindowsAdmin.EnsureCanModifySystem();

        foreach (var serviceName in BackupStore.GetManagedServices())
        {
            RestoreServiceDefaults(serviceName);
            FileLogger.Log($"ServiceManager: restored defaults for {serviceName}.");
        }

        BackupStore.ClearManagedServiceEntries();
        FileLogger.Log("ServiceManager: restored service defaults.");
    }

    internal static int GetServiceStartType(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
        return Convert.ToInt32(key?.GetValue("Start") ?? StartManual);
    }

    internal static void SetServiceStartType(string serviceName, int startType)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}",
            writable: true)
            ?? throw new InvalidOperationException($"Service '{serviceName}' was not found.");

        key.SetValue("Start", startType, RegistryValueKind.DWord);
    }

    internal static bool AreUpdateServicesDisabled() =>
        GetServiceStartType("wuauserv") == StartDisabled
        && GetServiceStartType("UsoSvc") == StartDisabled;

    private static void RestoreServiceDefaults(string serviceName)
    {
        var originalStartType = BackupStore.GetOriginalServiceStartType(serviceName);
        var startType = originalStartType ?? GetDefaultStartType(serviceName);
        SetServiceStartType(serviceName, startType);

        if (startType != StartDisabled)
        {
            StartService(serviceName);
        }
    }

    private static int GetDefaultStartType(string serviceName) =>
        string.Equals(serviceName, "wuauserv", StringComparison.OrdinalIgnoreCase)
            ? StartAutomatic
            : StartManual;

    private static void StopService(string serviceName)
    {
        using var controller = new ServiceController(serviceName);
        if (controller.Status == ServiceControllerStatus.Stopped)
        {
            return;
        }

        controller.Stop();
        controller.WaitForStatus(ServiceControllerStatus.Stopped, ServiceOperationTimeout);
    }

    private static void StartService(string serviceName)
    {
        using var controller = new ServiceController(serviceName);
        if (controller.Status == ServiceControllerStatus.Running)
        {
            return;
        }

        if (controller.Status == ServiceControllerStatus.Stopped)
        {
            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, ServiceOperationTimeout);
        }
    }
}
