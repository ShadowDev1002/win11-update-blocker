using Microsoft.Win32;
using Win11UpdateBlocker.Core.Logging;
using Win11UpdateBlocker.Core.Models;

namespace Win11UpdateBlocker.Core.Windows;

public static class RegistryManager
{
    private const string AuKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU";
    private const string WindowsUpdateKeyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string CurrentVersionKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public static void ApplyMode(UpdateBlockMode mode) =>
        ApplyPreferences(UpdatePreferences.FromLegacyMode(mode));

    public static void ApplyPreferences(UpdatePreferences preferences)
    {
        WindowsAdmin.EnsureCanModifySystem();
        BackupStore.SaveOriginals();
        RestoreDefaults();

        if (preferences.IsFullyAllowed())
        {
            FileLogger.Log("RegistryManager: applied AllowAll (defaults restored).");
            return;
        }

        if (preferences.IsFullyBlocked())
        {
            ApplyFullBlockRegistry();
            return;
        }

        ApplyPartialBlockRegistry(preferences);

        if (preferences.ShouldHideWindowsUpdateUi())
        {
            ApplyWindowsUpdateUiSuppression();
        }

        FileLogger.Log("RegistryManager: applied custom update preferences.");
    }

    private static void ApplyFullBlockRegistry()
    {
        SetDwordValue(AuKeyPath, "NoAutoUpdate", 1);
        ApplyWindowsUpdateUiSuppression();
        FileLogger.Log("RegistryManager: applied full block registry settings.");
    }

    private static void ApplyPartialBlockRegistry(UpdatePreferences preferences)
    {
        if (!preferences.AllowFeatureUpdates)
        {
            var (targetVersion, targetInfo) = GetCurrentWindowsVersion();
            SetDwordValue(AuKeyPath, "DisableOSUpgrade", 1);
            SetStringValue(AuKeyPath, "TargetReleaseVersion", targetVersion);
            SetStringValue(AuKeyPath, "TargetReleaseVersionInfo", targetInfo);
            FileLogger.Log("RegistryManager: blocked feature updates.");
        }

        if (!preferences.AllowDriverUpdates)
        {
            SetDwordValue(WindowsUpdateKeyPath, "ExcludeWUDriversInQualityUpdate", 1);
            FileLogger.Log("RegistryManager: blocked driver updates.");
        }

        if (!preferences.AllowOptionalUpdates)
        {
            SetDwordValue(WindowsUpdateKeyPath, "AllowOptionalUpdates", 0);
            FileLogger.Log("RegistryManager: blocked optional updates.");
        }

        if (!preferences.AllowQualityUpdates)
        {
            SetDwordValue(WindowsUpdateKeyPath, "DeferQualityUpdates", 1);
            SetDwordValue(WindowsUpdateKeyPath, "DeferQualityUpdatesPeriodInDays", 365);
            FileLogger.Log("RegistryManager: deferred quality updates.");
        }

        if (!preferences.AllowSecurityUpdates)
        {
            if (!preferences.AllowQualityUpdates)
            {
                SetDwordValue(AuKeyPath, "NoAutoUpdate", 1);
                FileLogger.Log("RegistryManager: blocked security updates (NoAutoUpdate).");
            }
            else
            {
                SetDwordValue(AuKeyPath, "AUOptions", 2);
                FileLogger.Log("RegistryManager: security updates set to notify before download.");
            }
        }
        else if (!preferences.AllowQualityUpdates || !preferences.AllowOptionalUpdates)
        {
            SetDwordValue(AuKeyPath, "AUOptions", 3);
        }
    }

    private static void ApplyWindowsUpdateUiSuppression()
    {
        SetDwordValue(AuKeyPath, "DisableWindowsUpdateAccess", 1);
        SetDwordValue(WindowsUpdateKeyPath, "SetDisableUXWUAccess", 1);
        SetDwordValue(WindowsUpdateKeyPath, "DoNotConnectToWindowsUpdateInternetLocations", 1);
        SetStringValue(WindowsUpdateKeyPath, "WUServer", "http://127.0.0.1");
        SetStringValue(WindowsUpdateKeyPath, "WUStatusServer", "http://127.0.0.1");
        FileLogger.Log("RegistryManager: suppressed Windows Update UI and online scan.");
    }

    public static void RestoreDefaults()
    {
        WindowsAdmin.EnsureCanModifySystem();

        foreach (var managed in BackupStore.GetManagedRegistryValues())
        {
            var original = BackupStore.GetOriginalRegistryValue(managed.KeyPath, managed.ValueName);
            if (original is null)
            {
                DeleteValue(managed.KeyPath, managed.ValueName);
                continue;
            }

            if (!original.Existed)
            {
                DeleteValue(managed.KeyPath, managed.ValueName);
                FileLogger.Log($"RegistryManager: removed {managed.KeyPath}\\{managed.ValueName}.");
                continue;
            }

            RestoreOriginalValue(original);
            FileLogger.Log($"RegistryManager: restored {managed.KeyPath}\\{managed.ValueName} to original value.");
        }

        BackupStore.ClearManagedRegistryEntries();
        FileLogger.Log("RegistryManager: restored registry defaults.");
    }

    internal static bool IsFeatureUpdatesBlocked() =>
        GetRegistryDword(AuKeyPath, "DisableOSUpgrade") == 1;

    internal static bool IsSecurityUpdatesBlocked() =>
        GetRegistryDword(AuKeyPath, "NoAutoUpdate") == 1
        || GetRegistryDword(AuKeyPath, "AUOptions") == 2;

    internal static bool IsQualityUpdatesBlocked() =>
        GetRegistryDword(WindowsUpdateKeyPath, "DeferQualityUpdates") == 1;

    internal static bool IsDriverUpdatesBlocked() =>
        GetRegistryDword(WindowsUpdateKeyPath, "ExcludeWUDriversInQualityUpdate") == 1;

    internal static bool IsOptionalUpdatesBlocked() =>
        GetRegistryDword(WindowsUpdateKeyPath, "AllowOptionalUpdates") == 0;

    internal static bool IsFullyBlockedInRegistry() =>
        GetRegistryDword(AuKeyPath, "NoAutoUpdate") == 1
        && GetRegistryDword(AuKeyPath, "DisableWindowsUpdateAccess") == 1;

    internal static UpdatePreferences InferPreferencesFromRegistry() => new()
    {
        AllowFeatureUpdates = !IsFeatureUpdatesBlocked(),
        AllowSecurityUpdates = !IsSecurityUpdatesBlocked(),
        AllowQualityUpdates = !IsQualityUpdatesBlocked(),
        AllowDriverUpdates = !IsDriverUpdatesBlocked(),
        AllowOptionalUpdates = !IsOptionalUpdatesBlocked()
    };

    private static int? GetRegistryDword(string keyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        var value = key?.GetValue(valueName);
        return value is null ? null : Convert.ToInt32(value);
    }

    private static void SetDwordValue(string keyPath, string valueName, int value)
    {
        using var key = BackupStore.EnsureRegistryKey(keyPath, writable: true);
        key.SetValue(valueName, value, RegistryValueKind.DWord);
        BackupStore.RecordManagedRegistry(keyPath, valueName);
    }

    private static void SetStringValue(string keyPath, string valueName, string value)
    {
        using var key = BackupStore.EnsureRegistryKey(keyPath, writable: true);
        key.SetValue(valueName, value, RegistryValueKind.String);
        BackupStore.RecordManagedRegistry(keyPath, valueName);
    }

    private static void DeleteValue(string keyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static void RestoreOriginalValue(RegistryValueBackup original)
    {
        if (!original.Existed || original.ValueKind is null || original.Value is null)
        {
            DeleteValue(original.KeyPath, original.ValueName);
            return;
        }

        using var key = BackupStore.EnsureRegistryKey(original.KeyPath, writable: true);
        var kind = Enum.Parse<RegistryValueKind>(original.ValueKind);
        var value = DeserializeRegistryValue(original.Value, kind);
        key.SetValue(original.ValueName, value, kind);
    }

    private static (string TargetReleaseVersion, string TargetReleaseVersionInfo) GetCurrentWindowsVersion()
    {
        using var key = Registry.LocalMachine.OpenSubKey(CurrentVersionKeyPath);
        var build = key?.GetValue("CurrentBuildNumber")?.ToString()
                    ?? key?.GetValue("CurrentBuild")?.ToString()
                    ?? "22631";
        var displayVersion = key?.GetValue("DisplayVersion")?.ToString()
                             ?? key?.GetValue("ReleaseId")?.ToString()
                             ?? string.Empty;
        return (build, displayVersion);
    }

    private static object DeserializeRegistryValue(string value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => long.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.MultiString => value.Split('\0'),
        _ => value
    };
}
