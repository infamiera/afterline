using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline.Services;

public static class WindowThemeService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    public static void Apply(Window window, ThemePreferences preferences)
    {
        ThemePreferences theme = ThemeService.Normalize(preferences);
        window.Background = Brush(theme.Background);
        window.Foreground = Brush(theme.PrimaryText);

        if (window is not global::Afterline.MainWindow)
        {
            window.PreviewKeyDown -= PopupWindow_PreviewKeyDown;
            window.PreviewKeyDown += PopupWindow_PreviewKeyDown;
        }

        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            ApplyNativeChrome(handle, theme);
            return;
        }

        window.SourceInitialized -= Window_SourceInitialized;
        window.SourceInitialized += Window_SourceInitialized;
    }

    private static void PopupWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not Window window) return;
        e.Handled = true;
        window.Close();
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;
        window.SourceInitialized -= Window_SourceInitialized;
        ApplyNativeChrome(new WindowInteropHelper(window).Handle, ThemeService.Current);
    }

    private static void ApplyNativeChrome(IntPtr handle, ThemePreferences theme)
    {
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindows()) return;

        try
        {
            Color caption = ThemeService.ParseColor(theme.Sidebar, Colors.Black);
            Color text = ThemeService.ParseColor(theme.PrimaryText, Colors.White);
            Color border = ThemeService.ParseColor(theme.Border, Colors.Gray);

            int darkMode = IsDark(caption) ? 1 : 0;
            int captionColor = ToColorRef(caption);
            int textColor = ToColorRef(text);
            int borderColor = ToColorRef(border);

            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
            _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref captionColor, sizeof(int));
            _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref textColor, sizeof(int));
            _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref borderColor, sizeof(int));
        }
        catch
        {
            // Older Windows versions may not expose every DWM color attribute.
        }
    }

    private static bool IsDark(Color color)
    {
        double luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
        return luminance < 0.5;
    }

    private static int ToColorRef(Color color)
        => color.R | (color.G << 8) | (color.B << 16);

    private static SolidColorBrush Brush(string value)
        => new(ThemeService.ParseColor(value, Colors.Transparent));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
}
