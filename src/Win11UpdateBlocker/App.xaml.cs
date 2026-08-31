using System.Windows;
using Win11UpdateBlocker.Core;
using Win11UpdateBlocker.Core.Logging;

namespace Win11UpdateBlocker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterExceptionHandlers();

        if (e.Args.Contains("--restore", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                new BlockerEngine().RestoreAll();
                Shutdown(0);
            }
            catch
            {
                Shutdown(1);
            }

            return;
        }

        base.OnStartup(e);

        StartupSettingsBootstrap.Initialize();

        try
        {
            new MainWindow().Show();
        }
        catch (Exception ex)
        {
            FileLogger.Log($"Startup failed: {ex}");
            ShowFatalError(
                "Die Anwendung konnte nicht gestartet werden.\n\n" +
                $"{ex.Message}");
            Shutdown(1);
        }
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            FileLogger.Log($"Unhandled UI exception: {args.Exception}");
            ShowFatalError($"Ein unerwarteter Fehler ist aufgetreten:\n\n{args.Exception.Message}");
            args.Handled = true;
            Current.Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                FileLogger.Log($"Unhandled domain exception: {ex}");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            FileLogger.Log($"Unobserved task exception: {args.Exception}");
            args.SetObserved();
        };
    }

    private static void ShowFatalError(string message)
    {
        MessageBox.Show(
            message,
            "Win11 Update Blocker",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
