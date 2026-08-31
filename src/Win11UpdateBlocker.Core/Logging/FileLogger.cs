namespace Win11UpdateBlocker.Core.Logging;

public static class FileLogger
{
    private static readonly object SyncRoot = new();

    private static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AppMetadata.ConfigFolderName);

    private static string LogFilePath => Path.Combine(LogDirectory, "blocker.log");

    public static void Log(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        EnsureDirectoryExists();

        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

        lock (SyncRoot)
        {
            try
            {
                File.AppendAllText(LogFilePath, line);
            }
            catch
            {
                // Logging must never break app startup for non-admin users.
            }
        }
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }
    }
}
