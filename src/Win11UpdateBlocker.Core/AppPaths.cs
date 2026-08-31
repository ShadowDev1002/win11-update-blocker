namespace Win11UpdateBlocker.Core;



public static class AppPaths

{

    public static string GetGuiExecutablePath() =>

        Environment.ProcessPath

        ?? Path.Combine(AppContext.BaseDirectory, "Win11UpdateBlocker.exe");



    public static string? GetInstallRoot()

    {

        var directory = Path.GetDirectoryName(GetGuiExecutablePath());

        if (string.IsNullOrWhiteSpace(directory))

        {

            return null;

        }



        var directoryName = Path.GetFileName(directory);



        // Legacy installs: C:\Program Files\Win11UpdateBlocker\gui\

        if (string.Equals(directoryName, "gui", StringComparison.OrdinalIgnoreCase))

        {

            return Directory.GetParent(directory)?.FullName;

        }



        // Current installs: C:\Program Files\Win11 Update Blocker\

        if (File.Exists(Path.Combine(directory, "service", "Win11UpdateBlocker.Service.exe")))

        {

            return directory;

        }



        return directory;

    }



    public static string? GetServiceExecutablePath()

    {

        var installRoot = GetInstallRoot();

        if (string.IsNullOrWhiteSpace(installRoot))

        {

            return null;

        }



        var servicePath = Path.Combine(installRoot, "service", "Win11UpdateBlocker.Service.exe");

        return File.Exists(servicePath) ? servicePath : null;

    }

}


