namespace Win11UpdateBlocker.Core.Ipc;

public static class ServiceAvailabilityCache
{
    private static readonly object SyncRoot = new();
    private static bool _isAvailable;
    private static DateTime _lastCheckedUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(8);

    public const int PingTimeoutMs = 350;

    public static bool IsAvailable()
    {
        lock (SyncRoot)
        {
            if (DateTime.UtcNow - _lastCheckedUtc < CacheDuration)
            {
                return _isAvailable;
            }
        }

        return RefreshSync();
    }

    public static bool RefreshSync()
    {
        var available = TryPing();
        lock (SyncRoot)
        {
            _isAvailable = available;
            _lastCheckedUtc = DateTime.UtcNow;
            return _isAvailable;
        }
    }

    public static async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var available = await Task.Run(TryPing, cancellationToken).ConfigureAwait(false);
        lock (SyncRoot)
        {
            _isAvailable = available;
            _lastCheckedUtc = DateTime.UtcNow;
        }
    }

    public static void Invalidate()
    {
        lock (SyncRoot)
        {
            _lastCheckedUtc = DateTime.MinValue;
        }
    }

    private static bool TryPing()
    {
        try
        {
            var response = BlockerPipeTransport.Send(
                new BlockerPipeRequest { Command = BlockerPipeCommand.Ping },
                PingTimeoutMs);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }
}
