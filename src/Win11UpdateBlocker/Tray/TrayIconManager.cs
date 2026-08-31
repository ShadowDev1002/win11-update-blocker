using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Win11UpdateBlocker.Core;
using Win11UpdateBlocker.Core.Config;
using Win11UpdateBlocker.Core.Models;
using Win11UpdateBlocker.ViewModels;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Win11UpdateBlocker.Tray;

public sealed class TrayIconManager : IDisposable
{
    private readonly MainWindow _mainWindow;
    private readonly MainViewModel _viewModel;
    private readonly BlockerEngine _engine = new();
    private readonly bool _trayEnabled;
    private NotifyIcon? _notifyIcon;
    private bool _explicitExit;
    private bool _disposed;

    public TrayIconManager(MainWindow mainWindow, MainViewModel viewModel)
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;
        _trayEnabled = ConfigStore.Load().TrayEnabled;

        if (_trayEnabled)
        {
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            InitializeTrayIcon();
        }
    }

    public bool TrayEnabled => _trayEnabled;

    public bool ShouldMinimizeToTray => _trayEnabled && !_explicitExit;

    public void ShowMainWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void RequestExit()
    {
        _explicitExit = true;
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    private void InitializeTrayIcon()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Öffnen", null, (_, _) => ShowMainWindow());
        contextMenu.Items.Add("Alles erlauben", null, (_, _) => ApplyPreset(UpdatePreferences.CreateAllowAll()));
        contextMenu.Items.Add("Alles blocken", null, (_, _) => ApplyPreset(UpdatePreferences.CreateBlockAll()));
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Beenden", null, (_, _) => RequestExit());

        _notifyIcon = new NotifyIcon
        {
            Text = "Win11 Update Blocker",
            Icon = LoadTrayIcon(),
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static Icon LoadTrayIcon()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            var extracted = Icon.ExtractAssociatedIcon(exePath);
            if (extracted is not null)
            {
                return extracted;
            }
        }

        return SystemIcons.Application;
    }

    private void ApplyPreset(UpdatePreferences preferences)
    {
        try
        {
            _engine.ApplyPreferences(preferences);
            _viewModel.RefreshStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Fehler beim Anwenden:\n{ex.Message}",
                "Win11 Update Blocker",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
