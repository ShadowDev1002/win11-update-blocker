using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Win11UpdateBlocker.Core;
using Win11UpdateBlocker.Core.Ipc;
using Win11UpdateBlocker.Core.Logging;
using Win11UpdateBlocker.Core.Models;
using Win11UpdateBlocker.Core.Updates;
using Win11UpdateBlocker.Tray;

namespace Win11UpdateBlocker.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly BlockerEngine _engine = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _updateCheckTimer;
    private readonly Dispatcher _dispatcher;
    private int _refreshInProgress;
    private bool _isLoadingPreferences;
    private bool _preferencesDirty;
    private bool _isBusy;
    private bool _hasSystemAccess;
    private bool _hasDrift;
    private NavigationSection _selectedSection = NavigationSection.Updates;
    private string _windowsUpdateStatus = "—";
    private string _backgroundServiceStatus = "—";
    private string _lastCheckText = "—";
    private bool _updateAvailable;
    private bool _isUpdateBusy;
    private string _updateBannerText = string.Empty;
    private string _updateStatusText = "Noch nicht geprüft";
    private AppUpdateInfo? _pendingUpdate;
    private string? _notifiedUpdateVersion;
    private TrayIconManager? _trayIconManager;

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
        CheckForUpdateCommand = new RelayCommand(() => _ = CheckForAppUpdateAsync(showUpToDateMessage: true), () => !IsUpdateBusy);
        InstallUpdateCommand = new RelayCommand(() => _ = InstallUpdateAsync(), () => UpdateAvailable && !IsUpdateBusy);

        Settings = new SettingsViewModel();
        Settings.SettingsSaved += () => _ = RefreshStatusAsync();

        foreach (var category in Categories)
        {
            category.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(UpdateCategoryViewModel.IsAllowed))
                {
                    return;
                }

                if (!_isLoadingPreferences)
                {
                    _preferencesDirty = true;
                    HasDrift = false;
                }

                OnPropertyChanged(nameof(ShowSecurityWarning));
            };
        }

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += (_, _) => _ = RefreshStatusAsync();
        _refreshTimer.Start();

        _updateCheckTimer = new DispatcherTimer { Interval = AppMetadata.UpdateCheckInterval };
        _updateCheckTimer.Tick += (_, _) => _ = CheckForAppUpdateAsync(trayNotify: true);

        _ = RefreshStatusAsync();
    }

    public void AttachTrayIconManager(TrayIconManager trayIconManager)
    {
        _trayIconManager = trayIconManager;
        _updateCheckTimer.Start();
        _ = CheckForAppUpdateAsync(notifyUser: true, trayNotify: true);
    }

    public string CurrentVersionText => $"Version {AppMetadata.Version}";

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

    public bool UpdateAvailable
    {
        get => _updateAvailable;
        private set
        {
            if (SetProperty(ref _updateAvailable, value))
            {
                InstallUpdateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsUpdateBusy
    {
        get => _isUpdateBusy;
        private set
        {
            if (SetProperty(ref _isUpdateBusy, value))
            {
                CheckForUpdateCommand.RaiseCanExecuteChanged();
                InstallUpdateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string UpdateBannerText
    {
        get => _updateBannerText;
        private set => SetProperty(ref _updateBannerText, value);
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

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

    public RelayCommand CheckForUpdateCommand { get; }

    public RelayCommand InstallUpdateCommand { get; }

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

        if (!_preferencesDirty)
        {
            LoadPreferences(snapshot.Status.ActivePreferences);
        }

        WindowsUpdateStatus = snapshot.Status.WindowsUpdateRunning ? "Aktiv" : "Gestoppt";
        BackgroundServiceStatus = snapshot.ServiceRunning
            ? "Aktiv"
            : snapshot.ServiceInstalled ? "Installiert" : "Inaktiv";
        LastCheckText = snapshot.Status.LastCheck?.ToLocalTime().ToString("g") ?? "—";
        HasDrift = snapshot.Status.HasDrift && !_preferencesDirty;
        OnPropertyChanged(nameof(ShowSecurityWarning));
    }

    private void LoadPreferences(UpdatePreferences preferences)
    {
        _isLoadingPreferences = true;
        try
        {
            GetCategory("feature").IsAllowed = preferences.AllowFeatureUpdates;
            GetCategory("security").IsAllowed = preferences.AllowSecurityUpdates;
            GetCategory("quality").IsAllowed = preferences.AllowQualityUpdates;
            GetCategory("driver").IsAllowed = preferences.AllowDriverUpdates;
            GetCategory("optional").IsAllowed = preferences.AllowOptionalUpdates;
        }
        finally
        {
            _isLoadingPreferences = false;
        }
    }

    private UpdateCategoryViewModel GetCategory(string key) =>
        Categories.First(c => c.Key == key);

    private void SetAllowAll()
    {
        LoadPreferences(UpdatePreferences.CreateAllowAll());
        _preferencesDirty = true;
        HasDrift = false;
    }

    private void SetBlockAll()
    {
        LoadPreferences(UpdatePreferences.CreateBlockAll());
        _preferencesDirty = true;
        HasDrift = false;
    }

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
            _preferencesDirty = false;
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

    private async Task CheckForAppUpdateAsync(bool showUpToDateMessage = false, bool notifyUser = false, bool trayNotify = false)
    {
        if (IsUpdateBusy)
        {
            return;
        }

        try
        {
            IsUpdateBusy = true;
            UpdateStatusText = "Suche nach Updates…";

            var update = await GitHubReleaseUpdateChecker.CheckForUpdateAsync().ConfigureAwait(false);

            await _dispatcher.InvokeAsync(() =>
            {
                _pendingUpdate = update;

                if (update is null)
                {
                    UpdateAvailable = false;
                    UpdateBannerText = string.Empty;
                    UpdateStatusText = $"Version {AppMetadata.Version} ist aktuell.";

                    if (showUpToDateMessage)
                    {
                        MessageBox.Show(
                            $"Du verwendest bereits die neueste Version ({AppMetadata.Version}).",
                            AppMetadata.DisplayName,
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    return;
                }

                UpdateAvailable = true;
                UpdateBannerText = $"Version {update.LatestVersion} verfügbar";
                UpdateStatusText = $"Update {update.LatestVersion} auf GitHub verfügbar.";
                NotifyAboutUpdate(update, notifyUser, trayNotify);
            });
        }
        catch (Exception ex)
        {
            FileLogger.Log($"UpdateChecker: check failed — {ex.Message}");

            await _dispatcher.InvokeAsync(() =>
            {
                UpdateStatusText = "Update-Prüfung fehlgeschlagen.";

                if (showUpToDateMessage)
                {
                    MessageBox.Show(
                        $"Update-Prüfung fehlgeschlagen:\n{ex.Message}",
                        AppMetadata.DisplayName,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            });
        }
        finally
        {
            await _dispatcher.InvokeAsync(() => IsUpdateBusy = false);
        }
    }

    private void NotifyAboutUpdate(AppUpdateInfo update, bool notifyUser, bool trayNotify)
    {
        if (_notifiedUpdateVersion == update.LatestVersion)
        {
            return;
        }

        _notifiedUpdateVersion = update.LatestVersion;

        if (notifyUser)
        {
            MessageBox.Show(
                $"Version {update.LatestVersion} ist verfügbar.\n\n" +
                "Du kannst das Update in der Sidebar oder unter Einstellungen → Software-Update installieren.",
                AppMetadata.DisplayName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        if (trayNotify)
        {
            _trayIconManager?.ShowUpdateNotification(update.LatestVersion);
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (_pendingUpdate is null)
        {
            await CheckForAppUpdateAsync();
        }

        var update = _pendingUpdate;
        if (update is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Version {update.LatestVersion} wird heruntergeladen und installiert.\n\n" +
            "Die App schließt sich danach. Der Installer startet mit Administratorrechten.",
            AppMetadata.DisplayName,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information);

        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            IsUpdateBusy = true;
            UpdateStatusText = "Update wird heruntergeladen…";

            var installerPath = await GitHubReleaseUpdateChecker.DownloadInstallerAsync(update).ConfigureAwait(false);

            await _dispatcher.InvokeAsync(() =>
            {
                UpdateStatusText = "Installer wird gestartet…";
                GitHubReleaseUpdateChecker.LaunchInstaller(installerPath);
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            FileLogger.Log($"UpdateChecker: install failed — {ex.Message}");

            await _dispatcher.InvokeAsync(() =>
            {
                UpdateStatusText = "Update fehlgeschlagen.";
                MessageBox.Show(
                    $"Update konnte nicht installiert werden:\n{ex.Message}",
                    AppMetadata.DisplayName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
        finally
        {
            await _dispatcher.InvokeAsync(() => IsUpdateBusy = false);
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

    public void Dispose()
    {
        _refreshTimer.Stop();
        _updateCheckTimer.Stop();
    }

    private sealed record StatusSnapshot(
        BlockerStatus Status,
        bool ServiceRunning,
        bool ServiceInstalled,
        bool HasAccess);
}
