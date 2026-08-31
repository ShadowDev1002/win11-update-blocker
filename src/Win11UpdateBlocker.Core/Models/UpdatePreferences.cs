namespace Win11UpdateBlocker.Core.Models;

public class UpdatePreferences
{
    public bool AllowFeatureUpdates { get; set; } = true;

    public bool AllowSecurityUpdates { get; set; } = true;

    public bool AllowQualityUpdates { get; set; } = true;

    public bool AllowDriverUpdates { get; set; } = true;

    public bool AllowOptionalUpdates { get; set; } = true;

    public bool IsFullyAllowed() =>
        AllowFeatureUpdates
        && AllowSecurityUpdates
        && AllowQualityUpdates
        && AllowDriverUpdates
        && AllowOptionalUpdates;

    public bool IsFullyBlocked() =>
        !AllowFeatureUpdates
        && !AllowSecurityUpdates
        && !AllowQualityUpdates
        && !AllowDriverUpdates
        && !AllowOptionalUpdates;

    /// <summary>
    /// When true, Windows Update must not scan for or display update offers.
    /// </summary>
    public bool ShouldHideWindowsUpdateUi() =>
        !AllowQualityUpdates
        || !AllowSecurityUpdates
        || !AllowOptionalUpdates;

    public bool Matches(UpdatePreferences? other) =>
        other is not null
        && AllowFeatureUpdates == other.AllowFeatureUpdates
        && AllowSecurityUpdates == other.AllowSecurityUpdates
        && AllowQualityUpdates == other.AllowQualityUpdates
        && AllowDriverUpdates == other.AllowDriverUpdates
        && AllowOptionalUpdates == other.AllowOptionalUpdates;

    public UpdatePreferences Clone() => new()
    {
        AllowFeatureUpdates = AllowFeatureUpdates,
        AllowSecurityUpdates = AllowSecurityUpdates,
        AllowQualityUpdates = AllowQualityUpdates,
        AllowDriverUpdates = AllowDriverUpdates,
        AllowOptionalUpdates = AllowOptionalUpdates
    };

    public static UpdatePreferences CreateAllowAll() => new();

    public static UpdatePreferences CreateBlockAll() => new()
    {
        AllowFeatureUpdates = false,
        AllowSecurityUpdates = false,
        AllowQualityUpdates = false,
        AllowDriverUpdates = false,
        AllowOptionalUpdates = false
    };

    public static UpdatePreferences FromLegacyMode(UpdateBlockMode mode) => mode switch
    {
        UpdateBlockMode.BlockAll => CreateBlockAll(),
        UpdateBlockMode.BlockFeatureUpdates => new UpdatePreferences
        {
            AllowFeatureUpdates = false
        },
        UpdateBlockMode.SecurityUpdatesOnly => new UpdatePreferences
        {
            AllowFeatureUpdates = false,
            AllowQualityUpdates = false,
            AllowDriverUpdates = false,
            AllowOptionalUpdates = false
        },
        _ => CreateAllowAll()
    };

    public UpdateBlockMode ToLegacyMode()
    {
        if (IsFullyAllowed())
        {
            return UpdateBlockMode.AllowAll;
        }

        if (IsFullyBlocked())
        {
            return UpdateBlockMode.BlockAll;
        }

        if (!AllowFeatureUpdates
            && AllowSecurityUpdates
            && AllowQualityUpdates
            && AllowDriverUpdates
            && AllowOptionalUpdates)
        {
            return UpdateBlockMode.BlockFeatureUpdates;
        }

        if (!AllowFeatureUpdates
            && !AllowQualityUpdates
            && !AllowDriverUpdates
            && !AllowOptionalUpdates
            && AllowSecurityUpdates)
        {
            return UpdateBlockMode.SecurityUpdatesOnly;
        }

        return UpdateBlockMode.AllowAll;
    }
}
