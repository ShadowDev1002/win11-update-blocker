using Win11UpdateBlocker.Core.Models;

namespace Win11UpdateBlocker.Core.Ipc;

public enum BlockerPipeCommand
{
    Ping,
    ApplyPreferences,
    RestoreAll
}

public sealed class BlockerPipeRequest
{
    public BlockerPipeCommand Command { get; set; }

    public UpdatePreferences? Preferences { get; set; }
}

public sealed class BlockerPipeResponse
{
    public bool Success { get; set; }

    public string? Error { get; set; }
}
