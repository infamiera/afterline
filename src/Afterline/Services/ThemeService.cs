using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline.Services;

public static class ThemeService
{
    public const int MaximumCustomThemes = 8;
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
            SecondaryText = normalized.SecondaryText,
            ScrollbarTrack = normalized.ScrollbarTrack,
            ScrollbarThumb = normalized.ScrollbarThumb,
            NavigationOverview = normalized.NavigationOverview,
            NavigationChat = normalized.NavigationChat,
            NavigationLibrary = normalized.NavigationLibrary,
            NavigationCreate = normalized.NavigationCreate,
            GradientStart = normalized.GradientStart,
            GradientMiddle = normalized.GradientMiddle,
            GradientEnd = normalized.GradientEnd,
            GradientAngle = normalized.GradientAngle,
            GradientIntensity = normalized.GradientIntensity
        };
    }

    public static ThemePreferences Normalize(ThemePreferences? source)
    {
        ThemePreferences defaults = CreateDefault();
        source ??= defaults;
        bool legacyPalette =
            string.Equals(source.GradientStart, defaults.GradientStart, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(source.GradientMiddle, defaults.GradientMiddle, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(source.GradientEnd, defaults.GradientEnd, StringComparison.OrdinalIgnoreCase) &&
            (!string.Equals(source.Background, defaults.Background, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(source.Panel, defaults.Panel, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(source.Accent, defaults.Accent, StringComparison.OrdinalIgnoreCase));

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
            SecondaryText = NormalizeColor(source.SecondaryText, defaults.SecondaryText),
            ScrollbarTrack = NormalizeColor(source.ScrollbarTrack, defaults.ScrollbarTrack),
            ScrollbarThumb = NormalizeColor(source.ScrollbarThumb, defaults.ScrollbarThumb),
            NavigationOverview = NormalizeColor(source.NavigationOverview, defaults.NavigationOverview),
            NavigationChat = NormalizeColor(source.NavigationChat, defaults.NavigationChat),
            NavigationLibrary = NormalizeColor(source.NavigationLibrary, defaults.NavigationLibrary),
            NavigationCreate = NormalizeColor(source.NavigationCreate, defaults.NavigationCreate),
            GradientStart = NormalizeColor(legacyPalette ? source.Accent : source.GradientStart, source.Panel),
            GradientMiddle = NormalizeColor(legacyPalette ? source.Panel : source.GradientMiddle, source.Background),
            GradientEnd = NormalizeColor(legacyPalette ? source.Background : source.GradientEnd, source.Background),
            GradientAngle = double.IsFinite(source.GradientAngle)
                ? NormalizeAngle(source.GradientAngle)
                : defaults.GradientAngle,
            GradientIntensity = double.IsFinite(source.GradientIntensity)
                ? Math.Clamp(source.GradientIntensity, 0, 100)
                : defaults.GradientIntensity
        };
    }

    public static ThemePreferences CreateGradientTheme(
        string startHex,
        string middleHex,
        string endHex,
        double angle = 145,
        double intensity = 32)
    {
        Color start = ParseColor(startHex, Color.FromRgb(0x22, 0x34, 0x4D));
        Color middle = ParseColor(middleHex, Color.FromRgb(0x17, 0x23, 0x31));
        Color end = ParseColor(endHex, Color.FromRgb(0x11, 0x15, 0x1B));
        Color tint = Average(start, middle, end);
        double strength = Math.Clamp(intensity, 0, 100) / 100.0;

        string Surface(string neutral, Color source, double amount)
            => ToHex(Blend(ParseColor(neutral, Colors.Black), source, strength * amount));

        Color accent = EnsureReadableAccent(Blend(middle, start, 0.35));
        return new ThemePreferences
        {
            Background = Surface("#101319", end, 0.46),
            Sidebar = Surface("#0C0F14", start, 0.42),
            Panel = Surface("#181C23", tint, 0.54),
            Raised = Surface("#202630", middle, 0.50),
            Inset = Surface("#14181F", end, 0.48),
            Border = Surface("#303844", tint, 0.62),
            Accent = ToHex(accent),
            AccentHover = ToHex(Blend(accent, Colors.White, 0.16)),
            ControlHover = Surface("#2A313C", middle, 0.58),
            PrimaryText = "#F2F4F7",
            SecondaryText = "#AFB8C4",
            ScrollbarTrack = Surface("#202630", end, 0.46),
            ScrollbarThumb = ToHex(Blend(Color.FromRgb(0x68, 0x78, 0x8A), tint, strength * 0.48)),
            NavigationOverview = ToHex(EnsureReadableAccent(start)),
            NavigationChat = ToHex(EnsureReadableAccent(middle)),
            NavigationLibrary = ToHex(EnsureReadableAccent(Blend(middle, Colors.White, 0.10))),
            NavigationCreate = ToHex(EnsureReadableAccent(end)),
            GradientStart = ToHex(start),
            GradientMiddle = ToHex(middle),
            GradientEnd = ToHex(end),
            GradientAngle = NormalizeAngle(angle),
            GradientIntensity = Math.Clamp(intensity, 0, 100)
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

    public static string NormalizeCustomThemeName(string? value)
    {
        string name = string.Join(
            " ",
            (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (name.Length > 40) name = name[..40].TrimEnd();
        return string.IsNullOrWhiteSpace(name) ? "Custom Theme" : name;
    }

    public static bool TrySaveCustomTheme(
        AppSettings settings,
        string? name,
        ThemePreferences theme,
        out bool updated,
        out string message)
    {
        settings.CustomThemes ??= new List<SavedThemePreset>();
        string normalizedName = NormalizeCustomThemeName(name);
        SavedThemePreset? existing = settings.CustomThemes.FirstOrDefault(
            preset => string.Equals(preset.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Name = normalizedName;
            existing.Theme = Clone(theme);
            existing.SavedAtUtc = DateTime.UtcNow;
            updated = true;
            message = $"Updated custom theme ‘{normalizedName}’.";
            return true;
        }

        if (settings.CustomThemes.Count >= MaximumCustomThemes)
        {
            updated = false;
            message = $"All {MaximumCustomThemes} custom theme slots are in use. Delete one before saving another.";
            return false;
        }

        settings.CustomThemes.Add(new SavedThemePreset
        {
            Name = normalizedName,
            Theme = Clone(theme),
            SavedAtUtc = DateTime.UtcNow
        });
        updated = false;
        message = $"Saved custom theme ‘{normalizedName}’.";
        return true;
    }

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
        SetApplicationBrush("AfterlineScrollbarTrack", current.ScrollbarTrack);
        SetApplicationBrush("AfterlineScrollbarThumb", current.ScrollbarThumb);
        SetApplicationBrush("AfterlineNavOverview", current.NavigationOverview);
        SetApplicationBrush("AfterlineNavChat", current.NavigationChat);
        SetApplicationBrush("AfterlineNavLibrary", current.NavigationLibrary);
        SetApplicationBrush("AfterlineNavCreate", current.NavigationCreate);
        SetGradientBrush("AfterlineAppGradient", current, 1.0);
        SetGradientBrush("AfterlineSidebarGradient", current, 0.72, current.GradientAngle + 35);
        SetGradientBrush("AfterlineHeaderGradient", current, 0.58, current.GradientAngle - 45);

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

    private static void SetGradientBrush(
        string key,
        ThemePreferences theme,
        double intensityScale,
        double? angleOverride = null)
    {
        if (System.Windows.Application.Current is null) return;

        double strength = Math.Clamp(theme.GradientIntensity / 100.0 * intensityScale, 0, 1);
        Color background = ParseColor(theme.Background, Colors.Black);
        Color start = Blend(background, ParseColor(theme.GradientStart, background), strength);
        Color middle = Blend(background, ParseColor(theme.GradientMiddle, background), strength);
        Color end = Blend(background, ParseColor(theme.GradientEnd, background), strength);
        double angle = NormalizeAngle(angleOverride ?? theme.GradientAngle);
        var brush = new LinearGradientBrush
        {
            StartPoint = GradientPoint(angle, true),
            EndPoint = GradientPoint(angle, false)
        };
        brush.GradientStops.Add(new GradientStop(start, 0));
        brush.GradientStops.Add(new GradientStop(middle, 0.52));
        brush.GradientStops.Add(new GradientStop(end, 1));
        System.Windows.Application.Current.Resources[key] = brush;
    }

    private static Point GradientPoint(double angle, bool start)
    {
        double radians = NormalizeAngle(angle) * Math.PI / 180.0;
        double x = Math.Cos(radians) * 0.5;
        double y = Math.Sin(radians) * 0.5;
        return start ? new Point(0.5 - x, 0.5 - y) : new Point(0.5 + x, 0.5 + y);
    }

    private static double NormalizeAngle(double angle)
    {
        double normalized = angle % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static Color Average(params Color[] colors)
        => Color.FromRgb(
            (byte)colors.Average(color => color.R),
            (byte)colors.Average(color => color.G),
            (byte)colors.Average(color => color.B));

    private static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Channel(byte a, byte b) => (byte)Math.Round(a + ((b - a) * amount));
        return Color.FromRgb(Channel(from.R, to.R), Channel(from.G, to.G), Channel(from.B, to.B));
    }

    private static Color EnsureReadableAccent(Color color)
    {
        double luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
        return luminance >= 0.48 ? color : Blend(color, Colors.White, Math.Min(0.58, 0.48 - luminance + 0.18));
    }

    private static void ApplyToTree(DependencyObject root, ThemePreferences current, ThemePreferences previous)
    {
        // Chat presentation owns its captured server colours. Theme previews may
        // restyle the surrounding interface, but must never rewrite a live or
        // archived chat line's foreground/fallback state.
        if (root is Afterline.RoleplayColorTextBlock)
            return;

        switch (root)
        {
            case Border border:
                RoundedPanelClip.Attach(border);
                if (System.Windows.Application.Current?.Resources["CardStyle"] is Style cardStyle &&
                    ReferenceEquals(border.Style, cardStyle))
                {
                    border.SetResourceReference(Border.BackgroundProperty, "Panel");
                    border.SetResourceReference(Border.BorderBrushProperty, "Border");
                    break;
                }
                BindThemeBrushProperty(border, Border.BackgroundProperty, ThemeBrushRole.Surface, current, previous);
                BindThemeBrushProperty(border, Border.BorderBrushProperty, ThemeBrushRole.Border, current, previous);
                break;
            case Panel panel:
                BindThemeBrushProperty(panel, Panel.BackgroundProperty, ThemeBrushRole.Surface, current, previous);
                break;
            case Control control:
                BindThemeBrushProperty(control, Control.BackgroundProperty, ThemeBrushRole.Surface, current, previous);
                BindThemeBrushProperty(control, Control.BorderBrushProperty, ThemeBrushRole.Border, current, previous);
                BindThemeBrushProperty(control, Control.ForegroundProperty, ThemeBrushRole.Text, current, previous);
                break;
            case TextBlock textBlock:
                BindThemeBrushProperty(textBlock, TextBlock.ForegroundProperty, ThemeBrushRole.Text, current, previous);
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

    private enum ThemeBrushRole { Surface, Border, Text }

    private static void BindThemeBrushProperty(
        DependencyObject target,
        DependencyProperty property,
        ThemeBrushRole role,
        ThemePreferences current,
        ThemePreferences previous)
    {
        // Preserve bindings and existing DynamicResource expressions. For plain
        // brushes, convert recognized palette colours to DynamicResource references
        // once. That keeps controls created at different points in a theme-preview
        // session from becoming stranded on an older palette.
        if (DependencyPropertyHelper.GetValueSource(target, property).IsExpression)
            return;

        if (target.GetValue(property) is not SolidColorBrush brush)
            return;

        string? resourceKey = role switch
        {
            ThemeBrushRole.Surface => FindSurfaceResource(ToHex(brush.Color), current, previous),
            ThemeBrushRole.Border => MatchesAny(ToHex(brush.Color), current.Border, previous.Border, CreateDefault().Border)
                ? "Border"
                : null,
            ThemeBrushRole.Text => FindTextResource(ToHex(brush.Color), current, previous),
            _ => null
        };

        if (resourceKey is not null)
            target.SetResourceReference(property, resourceKey);
    }

    private static string? FindSurfaceResource(string hex, ThemePreferences current, ThemePreferences previous)
    {
        ThemePreferences defaults = CreateDefault();
        if (MatchesAny(hex, defaults.Background, previous.Background, current.Background)) return "Bg";
        if (MatchesAny(hex, defaults.Sidebar, previous.Sidebar, current.Sidebar)) return "AfterlineSidebar";
        if (MatchesAny(hex, defaults.Panel, previous.Panel, current.Panel)) return "Panel";
        if (MatchesAny(hex, defaults.Raised, previous.Raised, current.Raised)) return "Raised";
        if (MatchesAny(hex, defaults.Inset, previous.Inset, current.Inset)) return "AfterlineInset";
        if (MatchesAny(hex, defaults.Border, previous.Border, current.Border)) return "Border";
        if (MatchesAny(hex, defaults.Accent, previous.Accent, current.Accent)) return "Accent";
        if (MatchesAny(hex, defaults.AccentHover, previous.AccentHover, current.AccentHover)) return "AccentHover";
        if (MatchesAny(hex, defaults.ControlHover, previous.ControlHover, current.ControlHover,
                "#242E3A", "#293B50", "#293544")) return "AfterlineControlHover";
        return null;
    }

    private static string? FindTextResource(string hex, ThemePreferences current, ThemePreferences previous)
    {
        ThemePreferences defaults = CreateDefault();
        if (MatchesAny(hex, defaults.PrimaryText, previous.PrimaryText, current.PrimaryText)) return "Text";
        if (MatchesAny(hex, defaults.SecondaryText, previous.SecondaryText, current.SecondaryText)) return "MutedText";
        if (MatchesAny(hex, defaults.Accent, previous.Accent, current.Accent)) return "Accent";
        return null;
    }

    private static bool MatchesAny(string actual, params string[] candidates)
        => candidates.Any(candidate => string.Equals(actual, candidate, StringComparison.OrdinalIgnoreCase));
}
