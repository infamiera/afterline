using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private bool _settingsLayoutPolished;

    private void EnsureSettingsLayoutPolish()
    {
        if (_settingsLayoutPolished) return;
        _settingsLayoutPolished = true;

        StackPanel? settingsStack = FindSettingsStackPanel();
        if (settingsStack is null) return;

        Border? startupCard = FindDirectSettingsCard(settingsStack, "Startup & FiveM");
        Border? captureCard = FindDirectSettingsCard(settingsStack, "Capture & processing");
        Border? storageCard = FindDirectSettingsCard(settingsStack, "Chatlog storage");
        Border? recoveryCard = FindDirectSettingsCard(settingsStack, "Recovery Center");

        if (startupCard is not null && captureCard is not null)
            CreateCompactSettingsTopRow(settingsStack, startupCard, captureCard);

        if (recoveryCard is not null)
        {
            settingsStack.Children.Remove(recoveryCard);
            int targetIndex = Math.Min(1, settingsStack.Children.Count);
            settingsStack.Children.Insert(targetIndex, recoveryCard);
            recoveryCard.Padding = new Thickness(14);
            recoveryCard.Margin = new Thickness(0, 0, 0, 10);
            CompactRecoveryCard(recoveryCard);
        }

        if (storageCard is not null)
        {
            storageCard.Padding = new Thickness(14);
            storageCard.Margin = new Thickness(0, 0, 0, 10);
            if (storageCard.Child is StackPanel storageStack)
            {
                foreach (TextBlock text in storageStack.Children.OfType<TextBlock>())
                {
                    if (!string.Equals(text.Text, "Chatlog storage", StringComparison.Ordinal))
                        text.Margin = new Thickness(0, 3, 0, 8);
                }
            }
        }

        SettingsPage.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        SettingsPage.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        SettingsPage.PanningMode = PanningMode.VerticalOnly;

        AlignMainFooter();
    }

    private void CreateCompactSettingsTopRow(StackPanel settingsStack, Border startupCard, Border captureCard)
    {
        int startupIndex = settingsStack.Children.IndexOf(startupCard);
        int captureIndex = settingsStack.Children.IndexOf(captureCard);
        int insertAt = Math.Max(0, Math.Min(startupIndex, captureIndex));

        settingsStack.Children.Remove(startupCard);
        settingsStack.Children.Remove(captureCard);

        startupCard.Margin = new Thickness(0);
        captureCard.Margin = new Thickness(0);
        startupCard.Padding = new Thickness(14);
        captureCard.Padding = new Thickness(14);

        CompactStartupCard(startupCard);
        CompactCaptureCard(captureCard);

        var row = new Grid
        {
            Tag = "AfterlineCompactSettingsRow",
            Margin = new Thickness(0, 0, 0, 10)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(startupCard, 0);
        row.Children.Add(startupCard);
        Grid.SetColumn(captureCard, 2);
        row.Children.Add(captureCard);

        settingsStack.Children.Insert(Math.Min(insertAt, settingsStack.Children.Count), row);
    }

    private static void CompactStartupCard(Border card)
    {
        if (card.Child is not StackPanel stack) return;

        bool firstCheck = true;
        foreach (CheckBox check in stack.Children.OfType<CheckBox>())
        {
            check.Margin = new Thickness(0, firstCheck ? 8 : 2, 0, 0);
            firstCheck = false;
        }
    }

    private static void CompactCaptureCard(Border card)
    {
        if (card.Child is not StackPanel stack) return;

        bool firstCheck = true;
        foreach (UIElement child in stack.Children)
        {
            if (child is CheckBox check)
            {
                check.Margin = new Thickness(0, firstCheck ? 8 : 2, 0, 0);
                firstCheck = false;
                continue;
            }

            if (child is not Grid grid || grid.ColumnDefinitions.Count < 2) continue;

            grid.Margin = new Thickness(0, 6, 0, 0);
            grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            grid.ColumnDefinitions[1].Width = new GridLength(108);
            if (grid.ColumnDefinitions.Count > 2)
                grid.ColumnDefinitions[2].Width = GridLength.Auto;

            foreach (ComboBox combo in grid.Children.OfType<ComboBox>())
            {
                combo.MinHeight = 30;
                combo.Height = 30;
            }
        }
    }

    private void CompactRecoveryCard(Border card)
    {
        if (card.Child is not StackPanel stack) return;

        foreach (TextBlock text in stack.Children.OfType<TextBlock>())
        {
            if (string.Equals(text.Text, "Recovery Center", StringComparison.Ordinal))
            {
                text.Margin = new Thickness(0);
                continue;
            }

            if (ReferenceEquals(text, _recoveryStatusText))
                text.Margin = new Thickness(0, 0, 0, 8);
            else
                text.Margin = new Thickness(0, 3, 0, 7);
        }
    }

    private void AlignMainFooter()
    {
        if (BottomStatusText.Parent is not Grid footerGrid) return;

        footerGrid.MinHeight = 32;
        footerGrid.VerticalAlignment = VerticalAlignment.Center;
        BottomStatusText.VerticalAlignment = VerticalAlignment.Center;

        foreach (TextBlock text in footerGrid.Children.OfType<TextBlock>())
            text.VerticalAlignment = VerticalAlignment.Center;
    }

    private static Border? FindDirectSettingsCard(StackPanel settingsStack, string title)
    {
        return settingsStack.Children
            .OfType<Border>()
            .FirstOrDefault(border => ContainsSettingsText(border, title));
    }

    private static bool ContainsSettingsText(DependencyObject root, string text)
    {
        if (root is TextBlock textBlock && string.Equals(textBlock.Text, text, StringComparison.Ordinal))
            return true;

        int count;
        try
        {
            count = VisualTreeHelper.GetChildrenCount(root);
        }
        catch
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (ContainsSettingsText(VisualTreeHelper.GetChild(root, i), text))
                return true;
        }

        return false;
    }
}
