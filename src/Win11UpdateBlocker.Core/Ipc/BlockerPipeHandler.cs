using Win11UpdateBlocker.Core.Logging;

namespace Win11UpdateBlocker.Core.Ipc;

public static class BlockerPipeHandler
{
    public static BlockerPipeResponse Handle(BlockerPipeRequest request, BlockerEngine engine)
    {
        try
        {
            return request.Command switch
            {
                BlockerPipeCommand.Ping => new BlockerPipeResponse { Success = true },
                BlockerPipeCommand.ApplyPreferences when request.Preferences is not null =>
                    HandleApplyPreferences(engine, request.Preferences),
                BlockerPipeCommand.RestoreAll => HandleRestoreAll(engine),
                _ => new BlockerPipeResponse
                {
                    Success = false,
                    Error = "Unbekannter oder unvollständiger Befehl."
                }
            };
        }
        catch (Exception ex)
        {
            FileLogger.Log($"BlockerPipeHandler: {request.Command} failed — {ex.Message}");
            return new BlockerPipeResponse { Success = false, Error = ex.Message };
        }
    }

    private static BlockerPipeResponse HandleApplyPreferences(BlockerEngine engine, Models.UpdatePreferences preferences)
    {
        engine.ApplyPreferencesDirect(preferences);
        return new BlockerPipeResponse { Success = true };
    }

    private static BlockerPipeResponse HandleRestoreAll(BlockerEngine engine)
    {
        engine.RestoreAllDirect();
        return new BlockerPipeResponse { Success = true };
    }
}
