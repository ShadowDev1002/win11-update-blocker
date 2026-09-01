using System.Windows;
using System.Windows.Controls;

namespace Win11UpdateBlocker.Dialogs;

public partial class AppDialogWindow : Window
{
    private MessageBoxResult _result = MessageBoxResult.None;

    public AppDialogWindow()
    {
        InitializeComponent();
    }

    public static MessageBoxResult Show(
        Window? owner,
        string title,
        string message,
        MessageBoxButton buttons = MessageBoxButton.OK,
        string? primaryText = null,
        string? secondaryText = null)
    {
        var dialog = new AppDialogWindow
        {
            Owner = owner,
            Title = title
        };

        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.BuildButtons(buttons, primaryText, secondaryText);
        dialog.ShowDialog();
        return dialog._result;
    }

    public static MessageBoxResult ShowUpdateAvailable(Window? owner, string latestVersion)
    {
        return Show(
            owner,
            "Update verfügbar",
            $"Version {latestVersion} ist bereit zum Download.\n\n" +
            "Du kannst jetzt aktualisieren oder später über die Sidebar bzw. Einstellungen → Software-Update.",
            MessageBoxButton.OKCancel,
            primaryText: "Jetzt aktualisieren",
            secondaryText: "Später");
    }

    private void BuildButtons(MessageBoxButton buttons, string? primaryText, string? secondaryText)
    {
        ButtonPanel.Children.Clear();

        void AddSecondary(string text, MessageBoxResult result)
        {
            var button = new Button
            {
                Content = text,
                Style = (Style)FindResource("SecondaryButton"),
                MinWidth = 100,
                Margin = new Thickness(0, 0, 10, 0),
                IsCancel = true
            };
            button.Click += (_, _) =>
            {
                _result = result;
                DialogResult = false;
            };
            ButtonPanel.Children.Add(button);
        }

        void AddPrimary(string text, MessageBoxResult result)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 120,
                IsDefault = true
            };
            button.Click += (_, _) =>
            {
                _result = result;
                DialogResult = true;
            };
            ButtonPanel.Children.Add(button);
        }

        switch (buttons)
        {
            case MessageBoxButton.OKCancel:
                AddSecondary(secondaryText ?? "Abbrechen", MessageBoxResult.Cancel);
                AddPrimary(primaryText ?? "OK", MessageBoxResult.OK);
                break;
            case MessageBoxButton.YesNo:
                AddSecondary(secondaryText ?? "Nein", MessageBoxResult.No);
                AddPrimary(primaryText ?? "Ja", MessageBoxResult.Yes);
                break;
            default:
                AddPrimary(primaryText ?? "OK", MessageBoxResult.OK);
                break;
        }
    }
}
