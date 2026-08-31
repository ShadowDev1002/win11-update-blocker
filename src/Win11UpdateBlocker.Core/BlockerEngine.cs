using System.ServiceProcess;

using Win11UpdateBlocker.Core.Config;

using Win11UpdateBlocker.Core.Ipc;

using Win11UpdateBlocker.Core.Logging;

using Win11UpdateBlocker.Core.Models;

using Win11UpdateBlocker.Core.Windows;



namespace Win11UpdateBlocker.Core;



public class BlockerEngine

{

    private const string BackgroundServiceName = AppMetadata.ServiceInternalName;



    public void ApplyMode(UpdateBlockMode mode) =>

        ApplyPreferences(UpdatePreferences.FromLegacyMode(mode));



    public void ApplyPreferences(UpdatePreferences preferences)

    {

        if (WindowsAdmin.CanModifySystem())

        {

            ApplyPreferencesDirect(preferences);

            return;

        }



        if (BlockerServiceClient.IsAvailable())

        {

            BlockerServiceClient.ApplyPreferences(preferences);

            return;

        }



        throw new InvalidOperationException(
            $"Der Hintergrund-Dienst ist nicht erreichbar. Bitte starte „{AppMetadata.DisplayName}“ in den Windows-Diensten oder installiere die App erneut.");

    }



    public void ApplyPreferencesDirect(UpdatePreferences preferences)

    {

        WindowsAdmin.EnsureCanModifySystem();

        BackupStore.SaveOriginals();

        RegistryManager.ApplyPreferences(preferences);

        ServiceManager.ApplyPreferences(preferences);



        var config = ConfigStore.Load();

        config.Preferences = preferences.Clone();

        config.Mode = preferences.ToLegacyMode();

        config.LastApplied = DateTime.UtcNow;

        ConfigStore.Save(config);



        FileLogger.Log("BlockerEngine: applied custom update preferences.");

    }



    public void RestoreAll()

    {

        if (WindowsAdmin.CanModifySystem())

        {

            RestoreAllDirect();

            return;

        }



        if (BlockerServiceClient.IsAvailable())

        {

            BlockerServiceClient.RestoreAll();

            return;

        }



        throw new InvalidOperationException(

            "Der Hintergrund-Dienst ist nicht erreichbar. Wiederherstellung nicht möglich.");

    }



    public void RestoreAllDirect()

    {

        WindowsAdmin.EnsureCanModifySystem();

        BackupStore.RestoreFromBackup();

        RegistryManager.RestoreDefaults();

        ServiceManager.RestoreDefaults();



        var config = ConfigStore.Load();

        config.Preferences = UpdatePreferences.CreateAllowAll();

        config.Mode = UpdateBlockMode.AllowAll;

        config.LastApplied = DateTime.UtcNow;

        ConfigStore.Save(config);



        FileLogger.Log("BlockerEngine: restored all settings to defaults.");

    }



    public BlockerStatus GetStatus()

    {

        var config = ConfigStore.Load();

        var inferred = InferPreferencesFromState();

        var hasDrift = !config.Preferences.Matches(inferred);



        return new BlockerStatus

        {

            ActivePreferences = hasDrift ? inferred : config.Preferences.Clone(),

            WindowsUpdateRunning = IsServiceRunning("wuauserv"),

            FeatureUpdatesBlocked = RegistryManager.IsFeatureUpdatesBlocked(),

            SecurityUpdatesBlocked = RegistryManager.IsSecurityUpdatesBlocked(),

            QualityUpdatesBlocked = RegistryManager.IsQualityUpdatesBlocked(),

            DriverUpdatesBlocked = RegistryManager.IsDriverUpdatesBlocked(),

            OptionalUpdatesBlocked = RegistryManager.IsOptionalUpdatesBlocked(),

            ServiceRunning = IsServiceRunning(BackgroundServiceName),

            LastCheck = DateTime.UtcNow,

            HasDrift = hasDrift

        };

    }



    public bool HasPrivilegedAccess() =>

        WindowsAdmin.CanModifySystem() || ServiceAvailabilityCache.IsAvailable();



    public bool IsRunningAsAdmin() => WindowsAdmin.IsRunningAsAdmin();



    public void EnforceCurrentMode()

    {

        var config = ConfigStore.Load();

        if (!config.BackgroundServiceEnabled)

        {

            return;

        }



        if (!WindowsAdmin.CanModifySystem())

        {

            FileLogger.Log("BlockerEngine: enforce skipped — insufficient privileges.");

            return;

        }



        if (ArePreferencesApplied(config.Preferences))

        {

            return;

        }



        FileLogger.Log("BlockerEngine: drift detected; re-applying saved preferences.");

        ApplyPreferencesDirect(config.Preferences);

    }



    private static bool ArePreferencesApplied(UpdatePreferences preferences)

    {

        var inferred = InferPreferencesFromState();

        return preferences.Matches(inferred);

    }



    private static UpdatePreferences InferPreferencesFromState()

    {

        var registryPreferences = RegistryManager.InferPreferencesFromRegistry();



        if (registryPreferences.IsFullyBlocked() && ServiceManager.AreUpdateServicesDisabled())

        {

            return UpdatePreferences.CreateBlockAll();

        }



        return registryPreferences;

    }



    private static bool IsServiceRunning(string serviceName)

    {

        try

        {

            using var controller = new ServiceController(serviceName);

            return controller.Status == ServiceControllerStatus.Running;

        }

        catch (InvalidOperationException)

        {

            return false;

        }

    }

}


