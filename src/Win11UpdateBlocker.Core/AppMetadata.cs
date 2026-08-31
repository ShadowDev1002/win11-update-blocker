namespace Win11UpdateBlocker.Core;

public static class AppMetadata
{
    public const string DisplayName = "Win11 Update Blocker";

    public const string Version = "1.0.2";

    public const string GitHubOwner = "ShadowDev1002";

    public const string GitHubRepo = "win11-update-blocker";

    public const string ReleaseAssetFileName = "Win11-Update-Blocker-Setup.exe";

    public static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);

    public const string ServiceInternalName = "Win11UpdateBlockerService";

    public const string ConfigFolderName = "Win11UpdateBlocker";

    public const string AutostartRegistryValueName = "Win11 Update Blocker";

    public const string LegacyAutostartRegistryValueName = "Win11UpdateBlocker";
}
