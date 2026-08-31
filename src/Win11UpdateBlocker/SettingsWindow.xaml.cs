using System.Windows;

namespace Win11UpdateBlocker;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        WindowIconHelper.Apply(this);
        InitializeComponent();
        DataContext = new ViewModels.SettingsViewModel(this);
    }
}
