namespace Win11UpdateBlocker.Core.Models;

public class BlockerConfig
{
    public UpdateBlockMode Mode { get; set; } = UpdateBlockMode.AllowAll;

    public UpdatePreferences Preferences { get; set; } = UpdatePreferences.CreateAllowAll();

    public bool TrayEnabled { get; set; } = true;

    public bool AutostartEnabled { get; set; } = true;

    public bool BackgroundServiceEnabled { get; set; } = true;

    public DateTime? LastApplied { get; set; }

    public int SettingsVersion { get; set; } = 2;
}
