using System.Windows;
using Win11UpdateBlocker.Core;
using Win11UpdateBlocker.Core.Config;

namespace Win11UpdateBlocker.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly Window? _owner;
    private bool _backgroundServiceEnabled;
    private bool _autostartEnabled;
    private bool _trayEnabled;
    private string _serviceStatusText = string.Empty;

    public SettingsViewModel(Window? owner = null)
    {
        _owner = owner;
        SaveCommand = new RelayCommand(Save);
        if (owner is not null)
        {
            CancelCommand = new RelayCommand(Cancel);
        }

        LoadSettings();
    }

    public event Action? SettingsSaved;

    public bool BackgroundServiceEnabled
    {
        get => _backgroundServiceEnabled;
        set => SetProperty(ref _backgroundServiceEnabled, value);
    }

    public bool AutostartEnabled
    {
        get => _autostartEnabled;
        set => SetProperty(ref _autostartEnabled, value);
    }

    public bool TrayEnabled
    {
        get => _trayEnabled;
        set => SetProperty(ref _trayEnabled, value);
    }

    public string ServiceStatusText
    {
        get => _serviceStatusText;
        private set => SetProperty(ref _serviceStatusText, value);
    }

    public bool IsEmbedded => _owner is null;

    public RelayCommand SaveCommand { get; }

    public RelayCommand? CancelCommand { get; }

    public void Reload() => LoadSettings();

    private void LoadSettings()
    {
        var config = ConfigStore.Load();
        BackgroundServiceEnabled = config.BackgroundServiceEnabled;
        AutostartEnabled = config.AutostartEnabled;
        TrayEnabled = config.TrayEnabled;
        RefreshServiceStatus();
    }

    private void RefreshServiceStatus()
    {
        if (!AppSettingsManager.IsBackgroundServiceInstalled())
        {
            ServiceStatusText = "Nicht installiert";
            return;
        }

        ServiceStatusText = AppSettingsManager.IsBackgroundServiceRunning() ? "Aktiv" : "Installiert, gestoppt";
    }

    private void Save()
    {
        try
        {
            var previousConfig = ConfigStore.Load();
            var trayChanged = previousConfig.TrayEnabled != TrayEnabled;

            AppSettingsManager.ApplySettings(BackgroundServiceEnabled, AutostartEnabled, TrayEnabled);
            RefreshServiceStatus();
            SettingsSaved?.Invoke();

            if (trayChanged)
            {
                MessageBox.Show(
                    "Das Tray-Icon wird nach dem Neustart der App aktiv.",
                    "Einstellungen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else if (IsEmbedded)
            {
                MessageBox.Show(
                    "Einstellungen gespeichert.",
                    "Einstellungen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            if (_owner is not null)
            {
                _owner.DialogResult = true;
                _owner.Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Einstellungen konnten nicht gespeichert werden:\n{ex.Message}",
                "Einstellungen",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Cancel()
    {
        if (_owner is not null)
        {
            _owner.DialogResult = false;
            _owner.Close();
        }
    }
}
