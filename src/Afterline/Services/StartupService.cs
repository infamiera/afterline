using Microsoft.Win32;

namespace Afterline.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Afterline";

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, false);
            return;
        }

        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to resolve Afterline executable path.");
        key.SetValue(ValueName, $"\"{exe}\" --minimized", RegistryValueKind.String);
    }
}
