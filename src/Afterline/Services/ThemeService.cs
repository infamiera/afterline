using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline.Services;

public static class ThemeService
{
    private static ThemePreferences _previousApplied = CreateDefault();

    public static ThemePreferences CreateDefault() => new();

    public static ThemePreferences Current => Clone(_previousApplied);

    public static ThemePreferences Clone(ThemePreferences? source)
    {
        ThemePreferences normalized = Normalize(source);
        return new ThemePreferences
        {
            Background = normalized.Background,
            Sidebar = normalized.Sidebar,
            Panel = normalized.Panel,
            Raised = normalized.Raised,
            Inset = normalized.Inset,
            Border = normalized.Border,
            Accent = normalized.Accent,
            AccentHover = normalized.AccentHover,
            ControlHover = normalized.ControlHover,
            PrimaryText = normalized.PrimaryText,
            SecondaryText = normalized.SecondaryText
        };
    }

    public static ThemePreferences Normalize(ThemePreferences? source)
    {
        ThemePreferences defaults = CreateDefault();
        source ??= defaults;

        return new ThemePreferences
        {
            Background = NormalizeColor(source.Background, defaults.Background),
            Sidebar = NormalizeColor(source.Sidebar, defaults.Sidebar),
            Panel = NormalizeColor(source.Panel, defaults.Panel),
            Raised = NormalizeColor(source.Raised, defaults.Raised),
            Inset = NormalizeColor(source.Inset, defaults.Inset),
            Border = NormalizeColor(source.Border, defaults.Border),
            Accent = NormalizeColor(source.Accent, defaults.Accent),
            AccentHover = NormalizeColor(source.AccentHover, defaults.AccentHover),
            ControlHover = NormalizeColor(source.ControlHover, defaults.ControlHover),
            PrimaryText = NormalizeColor(source.PrimaryText, defaults.PrimaryText),
            SecondaryText = NormalizeColor(source.SecondaryText, defaults.SecondaryText)
        };
    }

    public static Color ParseColor(string? value, Color fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value) && ColorConverter.ConvertFromString(value) is Color color)
                return color;
        }
        catch
        {
        }

        return fallback;
    }

    public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static void Apply(ThemePreferences? preferences)
    {
        ThemeUiStyles.Ensure();

        ThemePreferences current = Normalize(preferences);
        ThemePreferences previous = Clone(_previousApplied);

        SetApplicationBrush("Bg", current.Background);
        SetApplicationBrush("Panel", current.Panel);
        SetApplicationBrush("Raised", current.Raised);
        SetApplicationBrush("Border", current.Border);
        SetApplicationBrush("Accent", current.Accent);
        SetApplicationBrush("AccentHover", current.AccentHover);
        SetApplicationBrush("Text", current.PrimaryText);
        SetApplicationBrush("MutedText", current.SecondaryText);
        SetApplicationBrush("AfterlineSidebar", current.Sidebar);
        SetApplicationBrush("AfterlineInset", current.Inset);
        SetApplicationBrush("AfterlineControlHover", current.ControlHover);

        if (System.Windows.Application.Current is not null)
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                ApplyToTree(window, current, previous);
                WindowThemeService.Apply(window, current);
            }
        }

        _previousApplied = Clone(current);
    }

    public static void ApplyWindow(Window window)
        => WindowThemeService.Apply(window, Current);

    private static string NormalizeColor(string? value, string fallback)
    {
        Color fallbackColor = ParseColor(fallback, Colors.Black);
        Color parsed = ParseColor(value, fallbackColor);
        return ToHex(parsed);
    }

    private static void SetApplicationBrush(string key, string hex)
    {
        if (System.Windows.Application.Current is null) return;

        Color color = ParseColor(hex, Colors.Transparent);
        ResourceDictionary resources = System.Windows.Application.Current.Resources;
        if (resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        resources[key] = new SolidColorBrush(color);
    }

    private static void ApplyToTree(DependencyObject root, ThemePreferences current, ThemePreferences previous)
    {
        switch (root)
        {
            case Border border:
                border.Background = MapSurface(border.Background, current, previous);
                border.BorderBrush = MapBorder(border.BorderBrush, current, previous);
                break;
            case Panel panel:
                panel.Background = MapSurface(panel.Background, current, previous);
                break;
            case Control control:
                control.Background = MapSurface(control.Background, current, previous);
                control.BorderBrush = MapBorder(control.BorderBrush, current, previous);
                control.Foreground = MapText(control.Foreground, current, previous);
                break;
            case TextBlock textBlock:
                textBlock.Foreground = MapText(textBlock.Foreground, current, previous);
                break;
        }

        int childCount;
        try
        {
            childCount = VisualTreeHelper.GetChildrenCount(root);
        }
        catch
        {
            childCount = 0;
        }

        for (int i = 0; i < childCount; i++)
            ApplyToTree(VisualTreeHelper.GetChild(root, i), current, previous);
    }

    private static Brush? MapSurface(Brush? brush, ThemePreferences current, ThemePreferences previous)
    {
        if (brush is not SolidColorBrush solid) return brush;
        string hex = ToHex(solid.Color);
        ThemePreferences defaults = CreateDefault();

        if (Matches(hex, defaults.Background, previous.Background)) return NewBrush(current.Background);
        if (Matches(hex, defaults.Sidebar, previous.Sidebar)) return NewBrush(current.Sidebar);
        if (Matches(hex, defaults.Panel, previous.Panel)) return NewBrush(current.Panel);
        if (Matches(hex, defaults.Raised, previous.Raised)) return NewBrush(current.Raised);
        if (Matches(hex, defaults.Inset, previous.Inset)) return NewBrush(current.Inset);
        if (Matches(hex, defaults.Accent, previous.Accent)) return NewBrush(current.Accent);
        if (Matches(hex, defaults.AccentHover, previous.AccentHover)) return NewBrush(current.AccentHover);
        if (Matches(hex, defaults.ControlHover, previous.ControlHover) ||
            Matches(hex, "#242E3A", previous.ControlHover) ||
            Matches(hex, "#293B50", previous.ControlHover) ||
            Matches(hex, "#293544", previous.ControlHover))
            return NewBrush(current.ControlHover);

        return brush;
    }

    private static Brush? MapBorder(Brush? brush, ThemePreferences current, ThemePreferences previous)
    {
        if (brush is not SolidColorBrush solid) return brush;
        string hex = ToHex(solid.Color);
        ThemePreferences defaults = CreateDefault();
        return Matches(hex, defaults.Border, previous.Border) ? NewBrush(current.Border) : brush;
    }

    private static Brush? MapText(Brush? brush, ThemePreferences current, ThemePreferences previous)
    {
        if (brush is not SolidColorBrush solid) return brush;
        string hex = ToHex(solid.Color);
        ThemePreferences defaults = CreateDefault();

        if (Matches(hex, defaults.PrimaryText, previous.PrimaryText)) return NewBrush(current.PrimaryText);
        if (Matches(hex, defaults.SecondaryText, previous.SecondaryText)) return NewBrush(current.SecondaryText);
        return brush;
    }

    private static bool Matches(string actual, string defaultValue, string previousValue)
        => string.Equals(actual, defaultValue, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(actual, previousValue, StringComparison.OrdinalIgnoreCase);

    private static SolidColorBrush NewBrush(string hex)
        => new(ParseColor(hex, Colors.Transparent));
}
