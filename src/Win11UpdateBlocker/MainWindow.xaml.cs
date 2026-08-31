using System.ComponentModel;
using System.Windows;
using Win11UpdateBlocker.Tray;
using Win11UpdateBlocker.ViewModels;

namespace Win11UpdateBlocker;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly TrayIconManager _trayManager;

    public MainWindow()
    {
        WindowIconHelper.Apply(this);
        DataContext = _viewModel;
        InitializeComponent();
        _trayManager = new TrayIconManager(this, _viewModel);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_trayManager.ShouldMinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayManager.Dispose();
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
