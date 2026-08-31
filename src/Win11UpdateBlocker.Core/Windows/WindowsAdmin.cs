using System.Security.Principal;

namespace Win11UpdateBlocker.Core.Windows;

public static class WindowsAdmin
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsRunningAsSystem()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.IsSystem;
    }

    public static bool CanModifySystem() => IsRunningAsAdmin() || IsRunningAsSystem();

    public static void EnsureCanModifySystem()
    {
        if (!CanModifySystem())
        {
            throw new InvalidOperationException(
                "System privileges are required for registry and service operations.");
        }
    }
}
