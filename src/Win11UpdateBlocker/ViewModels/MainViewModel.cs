using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Win11UpdateBlocker.Core;
using Win11UpdateBlocker.Core.Ipc;
using Win11UpdateBlocker.Core.Logging;
using Win11UpdateBlocker.Core.Models;

namespace Win11UpdateBlocker.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly BlockerEngine _engine = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dispatcher _dispatcher;
    private int _refreshInProgress;
    private bool _isBusy;
    private bool _hasSystemAccess;
    private bool _hasDrift;
    private NavigationSection _selectedSection = NavigationSection.Updates;
    private string _windowsUpdateStatus = "—";
    private string _backgroundServiceStatus = "—";
    private string _lastCheckText = "—";

    public MainViewModel()
    {
        _dispatcher = Application.Current.Dispatcher;

        Categories = new ObservableCollection<UpdateCategoryViewModel>
        {
            new("feature", "Feature-Updates", "Große Windows-Versionssprünge (z. B. 24H2 → 25H2)"),
            new("security", "Sicherheitsupdates", "Kritische Sicherheitspatches für Windows"),
            new("quality", "Qualitätsupdates", "Monatliche kumulative Updates und Fehlerbehebungen"),
            new("driver", "Treiber-Updates", "Automatische Treiber-Updates über Windows Update"),
            new("optional", "Optionale Updates", "Optionale und manuelle Update-Pakete")
        };

        ApplyCommand = new RelayCommand(() => _ = ApplyPreferencesAsync(), () => !IsBusy);
        RefreshCommand = new RelayCommand(() => _ = RefreshStatusAsync());
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);
        AllowAllCommand = new RelayCommand(SetAllowAll);
        BlockAllCommand = new RelayCommand(SetBlockAll);
        NavigateUpdatesCommand = new RelayCommand(() => SelectedSection = NavigationSection.Updates);
        NavigateStatusCommand = new RelayCommand(() => SelectedSection = NavigationSection.Status);
        NavigateSettingsCommand = new RelayCommand(() => SelectedSection = NavigationSection.Settings);

        Settings = new SettingsViewModel();
        Settings.SettingsSaved += () => _ = RefreshStatusAsync();

        foreach (var category in Categories)
        {
            category.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(UpdateCategoryViewModel.IsAllowed))
                {
                    OnPropertyChanged(nameof(ShowSecurityWarning));
                }
            };
        }

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += (_, _) => _ = RefreshStatusAsync();
        _refreshTimer.Start();

        _ = RefreshStatusAsync();
    }

    public ObservableCollection<UpdateCategoryViewModel> Categories { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string WindowsUpdateStatus
    {
        get => _windowsUpdateStatus;
        private set => SetProperty(ref _windowsUpdateStatus, value);
    }

    public string BackgroundServiceStatus
    {
        get => _backgroundServiceStatus;
        private set => SetProperty(ref _backgroundServiceStatus, value);
    }

    public string LastCheckText
    {
        get => _lastCheckText;
        private set => SetProperty(ref _lastCheckText, value);
    }

    public bool ShowSecurityWarning => !GetCategory("security").IsAllowed;

    public bool HasDrift
    {
        get => _hasDrift;
        private set => SetProperty(ref _hasDrift, value);
    }

    public bool HasSystemAccess
    {
        get => _hasSystemAccess;
        private set
        {
            if (SetProperty(ref _hasSystemAccess, value))
            {
                OnPropertyChanged(nameof(NeedsSystemAccessHelp));
                OnPropertyChanged(nameof(SystemAccessText));
            }
        }
    }

    public bool NeedsSystemAccessHelp => !HasSystemAccess;

    public string SystemAccessText => HasSystemAccess ? "Bereit" : "Dienst offline";

    public SettingsViewModel Settings { get; }

    public NavigationSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(PageSubtitle));
                OnPropertyChanged(nameof(IsUpdatesPage));
                OnPropertyChanged(nameof(IsStatusPage));
                OnPropertyChanged(nameof(IsSettingsPage));

                if (value == NavigationSection.Settings)
                {
                    Settings.Reload();
                }
            }
        }
    }

    public string PageTitle => SelectedSection switch
    {
        NavigationSection.Status => "Systemstatus",
        NavigationSection.Settings => "Einstellungen",
        _ => "Updates steuern"
    };

    public string PageSubtitle => SelectedSection switch
    {
        NavigationSection.Status => "Live-Übersicht deines Systems",
        NavigationSection.Settings => "App-Verhalten und Hintergrund-Dienst",
        _ => "Wähle, welche Updates erlaubt sind"
    };

    public bool IsUpdatesPage => SelectedSection == NavigationSection.Updates;

    public bool IsStatusPage => SelectedSection == NavigationSection.Status;

    public bool IsSettingsPage => SelectedSection == NavigationSection.Settings;

    public RelayCommand NavigateUpdatesCommand { get; }

    public RelayCommand NavigateStatusCommand { get; }

    public RelayCommand NavigateSettingsCommand { get; }

    public RelayCommand ApplyCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand RestartAsAdminCommand { get; }

    public RelayCommand AllowAllCommand { get; }

    public RelayCommand BlockAllCommand { get; }

    public void RefreshStatus() => _ = RefreshStatusAsync();

    public async Task RefreshStatusAsync()
    {
        if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await ServiceAvailabilityCache.RefreshAsync().ConfigureAwait(false);

            var snapshot = await Task.Run(() =>
            {
                var status = _engine.GetStatus();
                var serviceRunning = AppSettingsManager.IsBackgroundServiceRunning();
                var serviceInstalled = AppSettingsManager.IsBackgroundServiceInstalled();
                var hasAccess = _engine.HasPrivilegedAccess();

                return new StatusSnapshot(
                    status,
                    serviceRunning,
                    serviceInstalled,
                    hasAccess);
            }).ConfigureAwait(false);

            await _dispatcher.InvokeAsync(() => ApplyStatusSnapshot(snapshot));
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                WindowsUpdateStatus = "Unbekannt";
                BackgroundServiceStatus = "Unbekannt";
                LastCheckText = "Fehler beim Lesen";
            });

            FileLogger.Log($"RefreshStatus failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInProgress, 0);
        }
    }

    public UpdatePreferences BuildPreferences() => new()
    {
        AllowFeatureUpdates = GetCategory("feature").IsAllowed,
        AllowSecurityUpdates = GetCategory("security").IsAllowed,
        AllowQualityUpdates = GetCategory("quality").IsAllowed,
        AllowDriverUpdates = GetCategory("driver").IsAllowed,
        AllowOptionalUpdates = GetCategory("optional").IsAllowed
    };

    private void ApplyStatusSnapshot(StatusSnapshot snapshot)
    {
        HasSystemAccess = snapshot.HasAccess;
        LoadPreferences(snapshot.Status.ActivePreferences);

        WindowsUpdateStatus = snapshot.Status.WindowsUpdateRunning ? "Aktiv" : "Gestoppt";
        BackgroundServiceStatus = snapshot.ServiceRunning
            ? "Aktiv"
            : snapshot.ServiceInstalled ? "Installiert" : "Inaktiv";
        LastCheckText = snapshot.Status.LastCheck?.ToLocalTime().ToString("g") ?? "—";
        HasDrift = snapshot.Status.HasDrift;
        OnPropertyChanged(nameof(ShowSecurityWarning));
    }

    private void LoadPreferences(UpdatePreferences preferences)
    {
        GetCategory("feature").IsAllowed = preferences.AllowFeatureUpdates;
        GetCategory("security").IsAllowed = preferences.AllowSecurityUpdates;
        GetCategory("quality").IsAllowed = preferences.AllowQualityUpdates;
        GetCategory("driver").IsAllowed = preferences.AllowDriverUpdates;
        GetCategory("optional").IsAllowed = preferences.AllowOptionalUpdates;
    }

    private UpdateCategoryViewModel GetCategory(string key) =>
        Categories.First(c => c.Key == key);

    private void SetAllowAll() => LoadPreferences(UpdatePreferences.CreateAllowAll());

    private void SetBlockAll() => LoadPreferences(UpdatePreferences.CreateBlockAll());

    private async Task ApplyPreferencesAsync()
    {
        if (!HasSystemAccess)
        {
            await ServiceAvailabilityCache.RefreshAsync().ConfigureAwait(false);
            await _dispatcher.InvokeAsync(() => HasSystemAccess = _engine.HasPrivilegedAccess());
        }

        if (!HasSystemAccess)
        {
            MessageBox.Show(
                "Der Hintergrund-Dienst ist nicht erreichbar.\n\n" +
                "Starte den Windows-Dienst „Win11 Update Blocker“ in den Windows-Einstellungen " +
                "oder installiere die App erneut über den Installer.",
                "Win11 Update Blocker",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var preferences = BuildPreferences();

        try
        {
            SetBusy(true);
            await Task.Run(() => _engine.ApplyPreferences(preferences)).ConfigureAwait(false);
            ServiceAvailabilityCache.Invalidate();
            await RefreshStatusAsync().ConfigureAwait(false);

            await _dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    "Deine Update-Einstellungen wurden erfolgreich angewendet.",
                    "Win11 Update Blocker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information));
        }
        catch (Exception ex)
        {
            await _dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    $"Fehler beim Anwenden:\n{ex.Message}",
                    "Win11 Update Blocker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
        }
        finally
        {
            await _dispatcher.InvokeAsync(() => SetBusy(false));
        }
    }

    private void SetBusy(bool isBusy)
    {
        IsBusy = isBusy;
        ApplyCommand.RaiseCanExecuteChanged();
    }

    private void RestartAsAdmin()
    {
        var exePath = Environment.ProcessPath
                      ?? Process.GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrWhiteSpace(exePath))
        {
            MessageBox.Show(
                "Programmdatei konnte nicht ermittelt werden.",
                "Win11 Update Blocker",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Neustart als Administrator fehlgeschlagen:\n{ex.Message}",
                "Win11 Update Blocker",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public void Dispose() => _refreshTimer.Stop();

    private sealed record StatusSnapshot(
        BlockerStatus Status,
        bool ServiceRunning,
        bool ServiceInstalled,
        bool HasAccess);
}
