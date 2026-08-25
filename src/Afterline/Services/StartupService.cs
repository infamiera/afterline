using Microsoft.Win32;

namespace Afterline.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Afterline";

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        string? command = key?.GetValue(ValueName) as string;
        string? executable = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(executable) &&
               CommandTargetsExecutable(command, executable);
    }

    public static void Reconcile(bool enabled)
    {
        if (enabled != IsEnabled())
            SetEnabled(enabled);
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
        key.SetValue(ValueName, BuildCommand(exe), RegistryValueKind.String);
    }

    internal static string BuildCommand(string executable)
        => $"\"{Path.GetFullPath(executable)}\" --minimized";

    internal static bool CommandTargetsExecutable(string? command, string executable)
        => !string.IsNullOrWhiteSpace(command) &&
           string.Equals(
               command.Trim(),
               BuildCommand(executable),
               StringComparison.OrdinalIgnoreCase);
}
