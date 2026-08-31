using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Win11UpdateBlocker;

internal static class WindowIconHelper
{
    public static void Apply(Window window)
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (!File.Exists(iconPath))
            {
                return;
            }

            using var stream = File.OpenRead(iconPath);
            window.Icon = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch
        {
            // Icon is optional; normal users must be able to start without elevation.
        }
    }
}
