using Win11UpdateBlocker.Core.Models;

namespace Win11UpdateBlocker.Core.Ipc;

public static class BlockerServiceClient
{
    public static bool IsAvailable() => ServiceAvailabilityCache.IsAvailable();

    public static void ApplyPreferences(UpdatePreferences preferences)
    {
        var response = BlockerPipeTransport.Send(new BlockerPipeRequest
        {
            Command = BlockerPipeCommand.ApplyPreferences,
            Preferences = preferences
        });

        if (!response.Success)
        {
            throw new InvalidOperationException(
                response.Error ?? "Der Hintergrund-Dienst konnte die Einstellungen nicht anwenden.");
        }

        ServiceAvailabilityCache.Invalidate();
    }

    public static void RestoreAll()
    {
        var response = BlockerPipeTransport.Send(new BlockerPipeRequest
        {
            Command = BlockerPipeCommand.RestoreAll
        });

        if (!response.Success)
        {
            throw new InvalidOperationException(
                response.Error ?? "Der Hintergrund-Dienst konnte die Einstellungen nicht zurücksetzen.");
        }

        ServiceAvailabilityCache.Invalidate();
    }
}
