namespace Afterline.Services;

internal static class GlobalHotkeySafety
{
    internal const string DefaultScreenshotHotkey = "Ctrl+Shift+F12";

    internal static bool IsSafeForGlobalRegistration(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut)) return false;

        string[] segments = shortcut.Split(
            '+',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return false;

        bool hasModifier = false;
        bool hasMouseButton = false;
        foreach (string segment in segments)
        {
            if (segment.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Control", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Shift", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                hasModifier = true;
                continue;
            }

            if (segment.Equals("Mouse 4", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Mouse4", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Mouse 5", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("Mouse5", StringComparison.OrdinalIgnoreCase))
            {
                hasMouseButton = true;
            }
        }

        // RegisterHotKey reserves an unmodified keyboard key system-wide. A
        // saved value such as "T" therefore prevents FiveM from receiving its
        // normal chat key. Mouse 4/5 use a non-consuming hook and remain safe.
        return hasMouseButton || hasModifier;
    }

    internal static void RunSmokeTest()
    {
        if (IsSafeForGlobalRegistration("T") ||
            IsSafeForGlobalRegistration("F12") ||
            !IsSafeForGlobalRegistration("Ctrl+T") ||
            !IsSafeForGlobalRegistration("Ctrl+Shift+F12") ||
            !IsSafeForGlobalRegistration("Mouse 4"))
        {
            throw new InvalidOperationException(
                "Global hotkey safety allowed an unmodified keyboard key or rejected a safe shortcut.");
        }
    }
}
