namespace Win11UpdateBlocker.Core.Models;

public class BlockerStatus
{
    public UpdatePreferences ActivePreferences { get; set; } = UpdatePreferences.CreateAllowAll();

    public bool WindowsUpdateRunning { get; set; }

    public bool FeatureUpdatesBlocked { get; set; }

    public bool SecurityUpdatesBlocked { get; set; }

    public bool QualityUpdatesBlocked { get; set; }

    public bool DriverUpdatesBlocked { get; set; }

    public bool OptionalUpdatesBlocked { get; set; }

    public bool ServiceRunning { get; set; }

    public DateTime? LastCheck { get; set; }

    public bool HasDrift { get; set; }
}
