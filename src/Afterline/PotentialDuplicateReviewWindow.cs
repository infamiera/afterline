using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

internal sealed class PotentialDuplicateReviewWindow : Window
{
    private readonly ObservableCollection<ChatEntry> _lines = new();

    public bool OpenLiveChatReview { get; private set; }

    public PotentialDuplicateReviewWindow(
        Window owner,
        IReadOnlyList<PotentialDuplicateCandidate> candidates)
    {
        Owner = owner;
        Title = "Investigate potential duplicates";
        Width = 850;
        Height = 700;
        MinWidth = 620;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "Potential duplicate comparison",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Earlier matching context is shown first. Suspected replay rows follow in the same log-style view. Nothing is removed here.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)owner.FindResource("MutedText"),
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(heading);

        foreach (PotentialDuplicateCandidate candidate in candidates)
        {
            AddDivider("EARLIER MATCH · comparison context");
            foreach (string line in candidate.HistoricalLines)
                _lines.Add(new ChatEntry(candidate.DetectedAt, line, isPotentialDuplicateReviewClone: true));
            AddDivider("POTENTIAL DUPLICATE · retained pending your decision");
            foreach (string line in candidate.Lines)
                _lines.Add(new ChatEntry(
                    candidate.DetectedAt,
                    line,
                    potentialDuplicateGroupId: candidate.Id,
                    isPotentialDuplicateReviewClone: true));
        }

        var list = new ListBox
        {
            ItemsSource = _lines,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent
        };
        list.SetValue(ScrollViewer.CanContentScrollProperty, true);
        list.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
        list.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(ChatEntry.Display)));
        text.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(ChatEntry.Foreground)));
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        list.ItemTemplate = new DataTemplate(typeof(ChatEntry)) { VisualTree = text };
        var listFrame = new Border { Child = list, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };
        listFrame.SetResourceReference(Border.BackgroundProperty, "AfterlineInset");
        listFrame.SetResourceReference(Border.BorderBrushProperty, "Border");
        RoundedPanelClip.Attach(listFrame);
        Grid.SetRow(listFrame, 2);
        root.Children.Add(listFrame);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var keep = new Button { Content = "Keep all lines", Padding = new Thickness(14, 7, 14, 7) };
        keep.Click += (_, _) => { DialogResult = false; Close(); };
        var openLive = new Button
        {
            Content = "Review in Live Chat",
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(8, 0, 0, 0)
        };
        openLive.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        openLive.Click += (_, _) => { OpenLiveChatReview = true; DialogResult = true; Close(); };
        actions.Children.Add(keep);
        actions.Children.Add(openLive);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        Content = root;
        ThemeService.ApplyWindow(this);
    }

    private void AddDivider(string text)
        => _lines.Add(new ChatEntry(DateTime.Now, $"==================== [{text}] ====================", isSystemMessage: true));
}
