using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

internal sealed class PotentialDuplicateReviewWindow : Window
{
    private readonly IReadOnlyList<PotentialDuplicateCandidate> _candidates;
    private readonly ObservableCollection<ChatEntry> _earlierLines = new();
    private readonly ObservableCollection<ChatEntry> _suspectedLines = new();
    private readonly TextBlock _positionText;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly ListBox _suspectedList;
    private int _candidateIndex;

    public bool RemoveRequested { get; private set; }
    public IReadOnlyDictionary<Guid, IReadOnlyList<int>> SelectedReplayLineIndexes { get; private set; } =
        new Dictionary<Guid, IReadOnlyList<int>>();

    public PotentialDuplicateReviewWindow(
        Window owner,
        IReadOnlyList<PotentialDuplicateCandidate> candidates,
        bool cleanupAvailable)
    {
        Owner = owner;
        _candidates = candidates;
        Title = "Investigate potential duplicates";
        Width = 1_180;
        Height = 720;
        MinWidth = 760;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new Grid();
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingText = new StackPanel();
        headingText.Children.Add(new TextBlock
        {
            Text = "Potential duplicate comparison",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        headingText.Children.Add(new TextBlock
        {
            Text = "The earlier original is on the left. The suspected replay is on the right and is the only content eligible for removal.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)owner.FindResource("MutedText"),
            Margin = new Thickness(0, 5, 0, 0)
        });
        heading.Children.Add(headingText);

        var navigator = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        _previousButton = new Button { Content = "←", Width = 34, Padding = new Thickness(6, 5, 6, 5) };
        _previousButton.Click += (_, _) => ShowCandidate(_candidateIndex - 1);
        _positionText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0, 9, 0),
            FontWeight = FontWeights.SemiBold
        };
        _nextButton = new Button { Content = "→", Width = 34, Padding = new Thickness(6, 5, 6, 5) };
        _nextButton.Click += (_, _) => ShowCandidate(_candidateIndex + 1);
        navigator.Children.Add(_previousButton);
        navigator.Children.Add(_positionText);
        navigator.Children.Add(_nextButton);
        Grid.SetColumn(navigator, 1);
        heading.Children.Add(navigator);
        root.Children.Add(heading);

        var comparison = new Grid();
        comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        comparison.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        comparison.Children.Add(BuildScenePanel(owner, "EARLIER MATCH · retained", "This scene remains unchanged.", _earlierLines, false, out _));
        UIElement suspected = BuildScenePanel(owner, "SUSPECTED REPLAY · highlighted", "Right-click selected rows to remove only those rows.", _suspectedLines, true, out _suspectedList);
        var removeSelected = new MenuItem
        {
            Header = "Remove selected highlighted line(s)…",
            Padding = new Thickness(12, 7, 12, 7)
        };
        removeSelected.Click += (_, _) => RequestRemoval(allCurrentLines: false);
        _suspectedList.ContextMenu = new ContextMenu { Items = { removeSelected } };
        Grid.SetColumn(suspected, 2);
        comparison.Children.Add(suspected);
        Grid.SetRow(comparison, 2);
        root.Children.Add(comparison);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var keep = new Button { Content = "Keep all flagged lines", Padding = new Thickness(14, 7, 14, 7) };
        keep.Click += (_, _) => { DialogResult = false; Close(); };
        var remove = new Button
        {
            Content = "Remove this highlighted replay…",
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(8, 0, 0, 0),
            IsEnabled = cleanupAvailable,
            ToolTip = cleanupAvailable
                ? "Creates a backup, then asks once more before removing only the suspected replay shown on the right."
                : "End the active FiveM session before changing its chatlog."
        };
        remove.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        remove.Click += (_, _) => RequestRemoval(allCurrentLines: true);
        actions.Children.Add(keep);
        actions.Children.Add(remove);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        Content = root;
        ThemeService.ApplyWindow(this);
        ShowCandidate(0);
    }

    private void ShowCandidate(int index)
    {
        if (index < 0 || index >= _candidates.Count) return;
        _candidateIndex = index;
        PotentialDuplicateCandidate candidate = _candidates[index];
        _earlierLines.Clear();
        _suspectedLines.Clear();
        foreach (string line in candidate.HistoricalLines)
            _earlierLines.Add(new ChatEntry(candidate.DetectedAt, line, isPotentialDuplicateReviewClone: true));
        foreach (string line in candidate.Lines)
            _suspectedLines.Add(new ChatEntry(
                candidate.DetectedAt,
                line,
                potentialDuplicateGroupId: candidate.Id,
                isPotentialDuplicateReviewClone: true));

        _positionText.Text = _candidates.Count == 1
            ? $"{candidate.Lines.Count:N0} lines"
            : $"Duplicate {index + 1} of {_candidates.Count}";
        _previousButton.IsEnabled = index > 0;
        _nextButton.IsEnabled = index + 1 < _candidates.Count;
        _suspectedList.SelectedItems.Clear();
    }

    private void RequestRemoval(bool allCurrentLines)
    {
        PotentialDuplicateCandidate candidate = _candidates[_candidateIndex];
        IEnumerable<int> indexes = allCurrentLines
            ? Enumerable.Range(0, candidate.Lines.Count)
            : _suspectedList.SelectedItems
                .OfType<ChatEntry>()
                .Select(entry => _suspectedLines.IndexOf(entry))
                .Where(index => index >= 0)
                .Distinct()
                .OrderBy(index => index);
        int[] selected = indexes.ToArray();
        if (selected.Length == 0) return;

        SelectedReplayLineIndexes = new Dictionary<Guid, IReadOnlyList<int>>
        {
            [candidate.Id] = selected
        };
        RemoveRequested = true;
        DialogResult = true;
        Close();
    }

    private static UIElement BuildScenePanel(
        Window owner,
        string title,
        string description,
        ObservableCollection<ChatEntry> lines,
        bool suspectedReplay,
        out ListBox list)
    {
        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(7) });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var label = new StackPanel();
        label.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = suspectedReplay ? (Brush)owner.FindResource("Warning") : (Brush)owner.FindResource("Text")
        });
        label.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = (Brush)owner.FindResource("MutedText"),
            Margin = new Thickness(0, 3, 0, 0)
        });
        panel.Children.Add(label);

        var sceneList = new ListBox
        {
            ItemsSource = lines,
            SelectionMode = suspectedReplay ? SelectionMode.Extended : SelectionMode.Single,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent
        };
        sceneList.SetValue(ScrollViewer.CanContentScrollProperty, true);
        sceneList.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
        sceneList.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(nameof(ChatEntry.Display)));
        text.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(ChatEntry.Foreground)));
        text.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        text.SetValue(FrameworkElement.MarginProperty, new Thickness(3, 2, 3, 2));
        sceneList.ItemTemplate = new DataTemplate(typeof(ChatEntry)) { VisualTree = text };
        if (suspectedReplay)
        {
            sceneList.ItemContainerStyle = CreateSuspectedReplayItemStyle(owner);
            sceneList.PreviewMouseRightButtonDown += (_, args) =>
            {
                if (args.OriginalSource is DependencyObject source &&
                    ItemsControl.ContainerFromElement(sceneList, source) is ListBoxItem item &&
                    !item.IsSelected)
                {
                    sceneList.SelectedItem = item.DataContext;
                }
            };
        }
        list = sceneList;
        var frame = new Border { Child = sceneList, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };
        frame.SetResourceReference(Border.BackgroundProperty, "AfterlineInset");
        frame.SetResourceReference(Border.BorderBrushProperty, suspectedReplay ? "Warning" : "Border");
        RoundedPanelClip.Attach(frame);
        Grid.SetRow(frame, 2);
        panel.Children.Add(frame);
        return panel;
    }

    private static Style CreateSuspectedReplayItemStyle(Window owner)
    {
        Color warning = ((SolidColorBrush)owner.FindResource("Warning")).Color;
        var background = new SolidColorBrush(Color.FromArgb(34, warning.R, warning.G, warning.B));
        background.Freeze();
        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.BackgroundProperty, background));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, (Brush)owner.FindResource("Warning")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2, 0, 0, 0)));
        return style;
    }
}
