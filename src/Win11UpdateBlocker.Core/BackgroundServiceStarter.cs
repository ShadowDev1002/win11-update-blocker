using System.ServiceProcess;
using Win11UpdateBlocker.Core.Config;
using Win11UpdateBlocker.Core.Logging;

namespace Win11UpdateBlocker.Core;

public static class BackgroundServiceStarter
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);

    public static void EnsureRunningIfConfigured()
    {
        var config = ConfigStore.Load();
        if (!config.BackgroundServiceEnabled)
        {
            return;
        }

        if (!AppSettingsManager.IsBackgroundServiceInstalled())
        {
            return;
        }

        if (AppSettingsManager.IsBackgroundServiceRunning())
        {
            return;
        }

        try
        {
            using var controller = new ServiceController(AppMetadata.ServiceInternalName);
            if (controller.Status == ServiceControllerStatus.Running)
            {
                return;
            }

            if (controller.Status == ServiceControllerStatus.StartPending)
            {
                controller.WaitForStatus(ServiceControllerStatus.Running, StartTimeout);
                return;
            }

            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, StartTimeout);
                FileLogger.Log("BackgroundServiceStarter: background service started.");
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"BackgroundServiceStarter: start failed — {ex.Message}");
        }
    }
}
