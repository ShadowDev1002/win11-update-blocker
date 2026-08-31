using System.Text.Json;
using Microsoft.Win32;
using Win11UpdateBlocker.Core.Logging;

namespace Win11UpdateBlocker.Core.Windows;

public static class BackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly (string KeyPath, string ValueName)[] TrackedRegistryValues =
    [
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "DisableWindowsUpdateAccess"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "DisableOSUpgrade"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "TargetReleaseVersion"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "TargetReleaseVersionInfo"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "SetDisableUXWUAccess"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DoNotConnectToWindowsUpdateInternetLocations"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "WUServer"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "WUStatusServer"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "AllowOptionalUpdates"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferQualityUpdates"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferQualityUpdatesPeriodInDays"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate"),
        (@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions")
    ];

    private static readonly string[] TrackedServices = ["wuauserv", "UsoSvc"];

    private static string BackupDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Win11UpdateBlocker");

    private static string BackupFilePath => Path.Combine(BackupDirectory, "backup.json");

    public static void SaveOriginals()
    {
        EnsureDirectoryExists();

        if (File.Exists(BackupFilePath))
        {
            return;
        }

        var backup = new BackupData
        {
            RegistryValues = CollectRegistryBackups(),
            ServiceStartTypes = CollectServiceBackups(),
            ManagedRegistryValues = [],
            ManagedServices = []
        };

        var json = JsonSerializer.Serialize(backup, JsonOptions);
        File.WriteAllText(BackupFilePath, json);
        FileLogger.Log("BackupStore: saved original registry values and service start types.");
    }

    public static void RestoreFromBackup()
    {
        EnsureAdmin();

        if (!File.Exists(BackupFilePath))
        {
            FileLogger.Log("BackupStore: no backup file found; nothing to restore.");
            return;
        }

        var json = File.ReadAllText(BackupFilePath);
        var backup = JsonSerializer.Deserialize<BackupData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Backup file is invalid.");

        foreach (var entry in backup.RegistryValues)
        {
            RestoreRegistryValue(entry);
        }

        foreach (var entry in backup.ServiceStartTypes)
        {
            ServiceManager.SetServiceStartType(entry.ServiceName, entry.StartType);
        }

        backup.ManagedRegistryValues = [];
        backup.ManagedServices = [];
        SaveBackup(backup);

        FileLogger.Log("BackupStore: restored all values from backup.");
    }

    public static void RecordManagedRegistry(string keyPath, string valueName)
    {
        var backup = LoadOrCreateBackup();
        var id = FormatRegistryId(keyPath, valueName);

        if (!backup.ManagedRegistryValues.Contains(id))
        {
            backup.ManagedRegistryValues.Add(id);
            SaveBackup(backup);
        }
    }

    public static void RecordManagedService(string serviceName)
    {
        var backup = LoadOrCreateBackup();
        if (!backup.ManagedServices.Contains(serviceName))
        {
            backup.ManagedServices.Add(serviceName);
            SaveBackup(backup);
        }
    }

    public static IReadOnlyList<ManagedRegistryValue> GetManagedRegistryValues()
    {
        if (!File.Exists(BackupFilePath))
        {
            return [];
        }

        var backup = LoadBackup();
        return backup.ManagedRegistryValues
            .Select(ParseRegistryId)
            .ToList();
    }

    public static IReadOnlyList<string> GetManagedServices()
    {
        if (!File.Exists(BackupFilePath))
        {
            return [];
        }

        return LoadBackup().ManagedServices;
    }

    public static RegistryValueBackup? GetOriginalRegistryValue(string keyPath, string valueName)
    {
        if (!File.Exists(BackupFilePath))
        {
            return null;
        }

        return LoadBackup().RegistryValues
            .FirstOrDefault(v =>
                string.Equals(v.KeyPath, keyPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(v.ValueName, valueName, StringComparison.OrdinalIgnoreCase));
    }

    public static int? GetOriginalServiceStartType(string serviceName)
    {
        if (!File.Exists(BackupFilePath))
        {
            return null;
        }

        return LoadBackup().ServiceStartTypes
            .FirstOrDefault(s => string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
            ?.StartType;
    }

    public static void ClearManagedRegistryEntries()
    {
        if (!File.Exists(BackupFilePath))
        {
            return;
        }

        var backup = LoadBackup();
        backup.ManagedRegistryValues = [];
        SaveBackup(backup);
    }

    public static void ClearManagedServiceEntries()
    {
        if (!File.Exists(BackupFilePath))
        {
            return;
        }

        var backup = LoadBackup();
        backup.ManagedServices = [];
        SaveBackup(backup);
    }

    private static List<RegistryValueBackup> CollectRegistryBackups()
    {
        var values = new List<RegistryValueBackup>();

        foreach (var (keyPath, valueName) in TrackedRegistryValues)
        {
            values.Add(ReadRegistryValue(keyPath, valueName));
        }

        return values;
    }

    private static List<ServiceStartTypeBackup> CollectServiceBackups()
    {
        var services = new List<ServiceStartTypeBackup>();

        foreach (var serviceName in TrackedServices)
        {
            services.Add(new ServiceStartTypeBackup
            {
                ServiceName = serviceName,
                StartType = ServiceManager.GetServiceStartType(serviceName)
            });
        }

        return services;
    }

    private static RegistryValueBackup ReadRegistryValue(string keyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        var value = key?.GetValue(valueName);

        if (value is null)
        {
            return new RegistryValueBackup
            {
                KeyPath = keyPath,
                ValueName = valueName,
                Existed = false
            };
        }

        var kind = key!.GetValueKind(valueName);
        return new RegistryValueBackup
        {
            KeyPath = keyPath,
            ValueName = valueName,
            Existed = true,
            ValueKind = kind.ToString(),
            Value = SerializeRegistryValue(value, kind)
        };
    }

    private static void RestoreRegistryValue(RegistryValueBackup entry)
    {
        if (!entry.Existed)
        {
            using var key = Registry.LocalMachine.OpenSubKey(entry.KeyPath, writable: true);
            key?.DeleteValue(entry.ValueName, throwOnMissingValue: false);
            FileLogger.Log($"BackupStore: removed registry value {entry.KeyPath}\\{entry.ValueName}.");
            return;
        }

        using var writableKey = EnsureRegistryKey(entry.KeyPath, writable: true);
        var kind = Enum.Parse<RegistryValueKind>(entry.ValueKind!);
        var value = DeserializeRegistryValue(entry.Value!, kind);
        writableKey.SetValue(entry.ValueName, value, kind);
        FileLogger.Log($"BackupStore: restored registry value {entry.KeyPath}\\{entry.ValueName}.");
    }

    internal static RegistryKey EnsureRegistryKey(string keyPath, bool writable)
    {
        if (writable)
        {
            return Registry.LocalMachine.CreateSubKey(keyPath, writable: true)
                ?? throw new InvalidOperationException($"Unable to create registry key '{keyPath}'.");
        }

        return Registry.LocalMachine.OpenSubKey(keyPath, writable: false)
            ?? throw new InvalidOperationException($"Registry key '{keyPath}' does not exist.");
    }

    private static string SerializeRegistryValue(object value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord or RegistryValueKind.QWord => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        RegistryValueKind.String or RegistryValueKind.ExpandString => value.ToString() ?? string.Empty,
        RegistryValueKind.MultiString => string.Join("\0", (string[])value),
        _ => value.ToString() ?? string.Empty
    };

    private static object DeserializeRegistryValue(string value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => long.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.MultiString => value.Split('\0'),
        _ => value
    };

    private static BackupData LoadOrCreateBackup()
    {
        if (!File.Exists(BackupFilePath))
        {
            return new BackupData
            {
                RegistryValues = CollectRegistryBackups(),
                ServiceStartTypes = CollectServiceBackups(),
                ManagedRegistryValues = [],
                ManagedServices = []
            };
        }

        return LoadBackup();
    }

    private static BackupData LoadBackup()
    {
        var json = File.ReadAllText(BackupFilePath);
        return JsonSerializer.Deserialize<BackupData>(json, JsonOptions)
            ?? throw new InvalidOperationException("Backup file is invalid.");
    }

    private static void SaveBackup(BackupData backup)
    {
        EnsureDirectoryExists();
        var json = JsonSerializer.Serialize(backup, JsonOptions);
        File.WriteAllText(BackupFilePath, json);
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(BackupDirectory))
        {
            Directory.CreateDirectory(BackupDirectory);
        }
    }

    private static void EnsureAdmin()
    {
        if (!WindowsAdmin.CanModifySystem())
        {
            throw new InvalidOperationException(
                "Administrator privileges are required to restore registry and service settings.");
        }
    }

    private static string FormatRegistryId(string keyPath, string valueName) => $"{keyPath}|{valueName}";

    private static ManagedRegistryValue ParseRegistryId(string id)
    {
        var separatorIndex = id.LastIndexOf('|');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException($"Invalid managed registry id '{id}'.");
        }

        return new ManagedRegistryValue(
            id[..separatorIndex],
            id[(separatorIndex + 1)..]);
    }

    private sealed class BackupData
    {
        public List<RegistryValueBackup> RegistryValues { get; set; } = [];

        public List<ServiceStartTypeBackup> ServiceStartTypes { get; set; } = [];

        public List<string> ManagedRegistryValues { get; set; } = [];

        public List<string> ManagedServices { get; set; } = [];
    }
}

public sealed class RegistryValueBackup
{
    public string KeyPath { get; set; } = string.Empty;

    public string ValueName { get; set; } = string.Empty;

    public bool Existed { get; set; }

    public string? ValueKind { get; set; }

    public string? Value { get; set; }
}

public sealed class ServiceStartTypeBackup
{
    public string ServiceName { get; set; } = string.Empty;

    public int StartType { get; set; }
}

public readonly record struct ManagedRegistryValue(string KeyPath, string ValueName);
