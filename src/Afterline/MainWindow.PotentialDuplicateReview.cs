using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private Border? _potentialDuplicateReviewBanner;
    private TextBlock? _potentialDuplicateReviewText;
    private Button? _potentialDuplicateRemoveButton;
    private IReadOnlyList<PotentialDuplicateCandidate> _activePotentialDuplicateReview =
        Array.Empty<PotentialDuplicateCandidate>();
    private string? _activePotentialDuplicateReviewPath;

    private void EnsurePotentialDuplicateReviewUi()
    {
        if (_potentialDuplicateReviewBanner is not null) return;

        Style? baseItemStyle = LiveChatList.ItemContainerStyle;
        var itemStyle = baseItemStyle is null
            ? new Style(typeof(ListBoxItem))
            : new Style(typeof(ListBoxItem), baseItemStyle);
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        var trigger = new DataTrigger
        {
            Binding = new Binding(nameof(ChatEntry.IsPotentialDuplicate)),
            Value = true
        };
        Color warningColor = ((SolidColorBrush)FindResource("Warning")).Color;
        var highlight = new SolidColorBrush(Color.FromArgb(46, warningColor.R, warningColor.G, warningColor.B));
        highlight.Freeze();
        trigger.Setters.Add(new Setter(Control.BackgroundProperty, highlight));
        trigger.Setters.Add(new Setter(Control.BorderBrushProperty, FindResource("Warning")));
        trigger.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2, 0, 0, 0)));
        itemStyle.Triggers.Add(trigger);
        LiveChatList.ItemContainerStyle = itemStyle;

        if (LiveChatList.Parent is not Grid chatGrid || chatGrid.RowDefinitions.Count < 2)
            return;

        chatGrid.RowDefinitions.Insert(1, new RowDefinition { Height = GridLength.Auto });
        foreach (UIElement child in chatGrid.Children)
        {
            int row = Grid.GetRow(child);
            if (row >= 1) Grid.SetRow(child, row + 1);
        }

        _potentialDuplicateReviewText = new TextBlock
        {
            Foreground = (Brush)FindResource("Text"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var keepButton = new Button
        {
            Content = "Keep all lines",
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Confirm that the highlighted lines are legitimate and leave the chatlog unchanged."
        };
        keepButton.Click += KeepPotentialDuplicates_Click;
        _potentialDuplicateRemoveButton = new Button
        {
            Content = "Remove highlighted duplicates",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Create a backup, then remove only the exact highlighted ranges from the chatlog."
        };
        _potentialDuplicateRemoveButton.Click += RemovePotentialDuplicates_Click;

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        actions.Children.Add(keepButton);
        actions.Children.Add(_potentialDuplicateRemoveButton);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(_potentialDuplicateReviewText);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        _potentialDuplicateReviewBanner = new Border
        {
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Warning"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(12, 9, 12, 9),
            Margin = new Thickness(12, 0, 12, 8),
            Child = grid,
            Visibility = Visibility.Collapsed
        };
        Grid.SetRow(_potentialDuplicateReviewBanner, 1);
        chatGrid.Children.Add(_potentialDuplicateReviewBanner);
    }

    private async Task OfferPotentialDuplicateReviewAsync(string? journalPath)
    {
        if (string.IsNullOrWhiteSpace(journalPath)) return;
        IReadOnlyList<PotentialDuplicateCandidate> candidates =
            await _capture.ReadPotentialDuplicatesAsync(journalPath, CancellationToken.None);
        if (candidates.Count == 0) return;

        int lineCount = candidates.Sum(candidate => candidate.Lines.Count);
        MessageBoxResult result = System.Windows.MessageBox.Show(
            this,
            $"Afterline found {lineCount:N0} line{(lineCount == 1 ? string.Empty : "s")} that may be a replayed duplicate.\n\n" +
            "Nothing has been removed. Would you like to review the highlighted lines in Live Chat?",
            "Potential duplicate chat lines",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        ShowPotentialDuplicateReview(journalPath, candidates);
    }

    private void RefreshPotentialDuplicateReviewAvailability(string journalPath)
    {
        if (_potentialDuplicateRemoveButton is null ||
            !string.Equals(
                _activePotentialDuplicateReviewPath,
                journalPath,
                StringComparison.OrdinalIgnoreCase))
            return;

        _potentialDuplicateRemoveButton.IsEnabled = true;
        _potentialDuplicateRemoveButton.ToolTip =
            "Create a backup, then remove only the exact highlighted ranges from the chatlog.";
    }

    private void ShowPotentialDuplicateReview(
        string journalPath,
        IReadOnlyList<PotentialDuplicateCandidate> candidates)
    {
        if (!ConfirmStreamerSensitiveViewV076("Live Chat duplicate review")) return;
        EnsurePotentialDuplicateReviewUi();
        if (_potentialDuplicateReviewBanner is null || _potentialDuplicateReviewText is null)
            return;

        HashSet<Guid> candidateIds = candidates.Select(candidate => candidate.Id).ToHashSet();
        for (int index = LiveMessages.Count - 1; index >= 0; index--)
        {
            if (LiveMessages[index].PotentialDuplicateGroupId is Guid id && candidateIds.Contains(id))
                LiveMessages.RemoveAt(index);
        }

        foreach (PotentialDuplicateCandidate candidate in candidates)
        {
            LiveMessages.Add(new ChatEntry(
                candidate.DetectedAt,
                "==================== [EARLIER MATCH · comparison only] ====================",
                isSystemMessage: true,
                isPotentialDuplicateReviewClone: true));
            foreach (string historicalLine in candidate.HistoricalLines)
            {
                LiveMessages.Add(new ChatEntry(
                    candidate.DetectedAt,
                    historicalLine,
                    isPotentialDuplicateReviewClone: true));
            }
            LiveMessages.Add(new ChatEntry(
                candidate.DetectedAt,
                "==================== [POTENTIAL DUPLICATE · highlighted] ====================",
                isSystemMessage: true,
                isPotentialDuplicateReviewClone: true));
            foreach (string line in candidate.Lines)
            {
                LiveMessages.Add(new ChatEntry(
                    candidate.DetectedAt,
                    line,
                    potentialDuplicateGroupId: candidate.Id,
                    isPotentialDuplicateReviewClone: true));
            }
        }

        _activePotentialDuplicateReview = candidates;
        _activePotentialDuplicateReviewPath = journalPath;
        int lineCount = candidates.Sum(candidate => candidate.Lines.Count);
        _potentialDuplicateReviewText.Text =
            $"Review {lineCount:N0} highlighted potential duplicate line{(lineCount == 1 ? string.Empty : "s")}. " +
            "Their displayed FiveM timestamps are preserved exactly. All lines remain in the local .txt until you explicitly remove them.";
        bool activeJournal = _journal.HasActiveSession && string.Equals(
            _journal.ActiveFile,
            journalPath,
            StringComparison.OrdinalIgnoreCase);
        _potentialDuplicateRemoveButton!.IsEnabled = !activeJournal;
        _potentialDuplicateRemoveButton.ToolTip = activeJournal
            ? "End the active FiveM session before rewriting its chatlog."
            : "Create a backup, then remove only the exact highlighted ranges from the chatlog.";
        if (activeJournal)
            _potentialDuplicateReviewText.Text += " End the active session before removal becomes available.";
        _potentialDuplicateReviewBanner.Visibility = Visibility.Visible;

        LiveChatList.Visibility = Visibility.Visible;
        _liveChatView?.Refresh();
        ShowPage(LivePage, "Live Chat", "Review potential duplicate capture ranges");
        ChatEntry? first = LiveMessages.FirstOrDefault(entry =>
            entry.PotentialDuplicateGroupId is Guid id && candidateIds.Contains(id));
        if (first is not null) LiveChatList.ScrollIntoView(first);
    }

    private async void KeepPotentialDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (_activePotentialDuplicateReview.Count == 0) return;
        await _capture.MarkPotentialDuplicatesReviewedAsync(
            _activePotentialDuplicateReview.Select(candidate => candidate.Id),
            removed: false,
            CancellationToken.None);
        FinishPotentialDuplicateReview();
        if (_liveActionStatus is not null)
            _liveActionStatus.Text = "Potential duplicates reviewed; every line was kept.";
    }

    private async void RemovePotentialDuplicates_Click(object sender, RoutedEventArgs e)
    {
        if (_activePotentialDuplicateReview.Count == 0 ||
            string.IsNullOrWhiteSpace(_activePotentialDuplicateReviewPath))
            return;
        if (_journal.HasActiveSession && string.Equals(
                _journal.ActiveFile,
                _activePotentialDuplicateReviewPath,
                StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(
                this,
                "End the active FiveM session before changing its chatlog.",
                "Chatlog is still active",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        int lineCount = _activePotentialDuplicateReview.Sum(candidate => candidate.Lines.Count);
        MessageBoxResult confirm = System.Windows.MessageBox.Show(
            this,
            $"Remove exactly {lineCount:N0} highlighted line{(lineCount == 1 ? string.Empty : "s")} from this chatlog?\n\n" +
            "Afterline will create a complete backup first. No unhighlighted line will be changed.",
            "Confirm duplicate removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        PotentialDuplicateCleanupResult? cleanup = null;
        try
        {
            cleanup = await PotentialDuplicateCleanupService.RemoveAsync(
                _activePotentialDuplicateReviewPath,
                _activePotentialDuplicateReview,
                CancellationToken.None);
            await _capture.MarkPotentialDuplicatesReviewedAsync(
                _activePotentialDuplicateReview.Select(candidate => candidate.Id),
                removed: true,
                CancellationToken.None);
            await _archiveService.EnsureFileIndexedAsync(
                _settings.ArchiveRoot,
                _activePotentialDuplicateReviewPath,
                CancellationToken.None);
            FinishPotentialDuplicateReview();
            System.Windows.MessageBox.Show(
                this,
                $"Removed {cleanup.RemovedLineCount:N0} highlighted line{(cleanup.RemovedLineCount == 1 ? string.Empty : "s")}.\n\n" +
                $"Backup: {StreamerModePresentationService.PathForDisplay(cleanup.BackupPath)}",
                "Chatlog updated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("User-confirmed potential duplicate cleanup failed.", ex);
            System.Windows.MessageBox.Show(
                this,
                cleanup is null
                    ? ex.Message
                    : "The highlighted lines were removed and the backup was created, but Afterline could not finish updating its review/index metadata.\n\n" + ex.Message,
                cleanup is null ? "Chatlog was not changed" : "Chatlog updated with a warning",
                MessageBoxButton.OK,
                cleanup is null ? MessageBoxImage.Error : MessageBoxImage.Warning);
        }
    }

    private void FinishPotentialDuplicateReview()
    {
        HashSet<Guid> ids = _activePotentialDuplicateReview.Select(candidate => candidate.Id).ToHashSet();
        for (int index = LiveMessages.Count - 1; index >= 0; index--)
        {
            ChatEntry entry = LiveMessages[index];
            if (entry.IsPotentialDuplicateReviewClone)
            {
                LiveMessages.RemoveAt(index);
                continue;
            }
            if (entry.PotentialDuplicateGroupId is not Guid id || !ids.Contains(id))
                continue;
            entry.ClearPotentialDuplicateFlag();
        }
        _liveChatView?.Refresh();
        if (_potentialDuplicateReviewBanner is not null)
            _potentialDuplicateReviewBanner.Visibility = Visibility.Collapsed;
        _activePotentialDuplicateReview = Array.Empty<PotentialDuplicateCandidate>();
        _activePotentialDuplicateReviewPath = null;
    }
}
