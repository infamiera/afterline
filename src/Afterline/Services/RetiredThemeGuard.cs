using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Afterline.Models;

namespace Afterline.Services;

internal static class RetiredThemeGuard
{
    private static bool _uiFilterInstalled;
    private static readonly HashSet<ICollectionView> FilteredViews = new();

    private static readonly ThemePreferences Frost = new()
    {
        Background = "#F3F6F9",
        Sidebar = "#E7EDF3",
        Panel = "#FFFFFF",
        Raised = "#EDF2F7",
        Inset = "#F7F9FB",
        Border = "#C9D3DD",
        Accent = "#3D7FC4",
        AccentHover = "#5B99D6",
        ControlHover = "#DFE7EF",
        PrimaryText = "#18212B",
        SecondaryText = "#5F6D7A"
    };

    public static bool IsRetiredFrost(ThemePreferences? theme)
    {
        ThemePreferences candidate = ThemeService.Normalize(theme);
        ThemePreferences frost = ThemeService.Normalize(Frost);

        return Same(candidate.Background, frost.Background) &&
               Same(candidate.Sidebar, frost.Sidebar) &&
               Same(candidate.Panel, frost.Panel) &&
               Same(candidate.Raised, frost.Raised) &&
               Same(candidate.Inset, frost.Inset) &&
               Same(candidate.Border, frost.Border) &&
               Same(candidate.Accent, frost.Accent) &&
               Same(candidate.AccentHover, frost.AccentHover) &&
               Same(candidate.ControlHover, frost.ControlHover) &&
               Same(candidate.PrimaryText, frost.PrimaryText) &&
               Same(candidate.SecondaryText, frost.SecondaryText);
    }

    public static void EnsureUiFilter()
    {
        if (_uiFilterInstalled) return;
        _uiFilterInstalled = true;

        EventManager.RegisterClassHandler(
            typeof(ComboBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnComboBoxLoaded));
    }

    private static void OnComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.ItemsSource is not IEnumerable source) return;

        bool containsFrost = false;
        foreach (object? item in source)
        {
            if (!IsFrostItem(item)) continue;
            containsFrost = true;
            break;
        }

        if (!containsFrost) return;

        ICollectionView view = CollectionViewSource.GetDefaultView(combo.ItemsSource);
        if (FilteredViews.Add(view))
        {
            Predicate<object>? existing = view.Filter;
            view.Filter = item => (existing?.Invoke(item) ?? true) && !IsFrostItem(item);
            view.Refresh();
        }

        if (IsFrostItem(combo.SelectedItem) || combo.SelectedIndex < 0)
            combo.SelectedIndex = combo.Items.Count > 0 ? 0 : -1;
    }

    private static bool IsFrostItem(object? item)
        => string.Equals(item?.ToString(), "Frost", StringComparison.OrdinalIgnoreCase);

    private static bool Same(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
