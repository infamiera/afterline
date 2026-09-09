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
    private bool _potentialDuplicatePromptOpen;

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

        await PresentPotentialDuplicatePromptAsync(journalPath, candidates);
    }

    private async void Capture_PotentialDuplicateDetected(object? sender, PotentialDuplicateCandidate candidate)
    {
        if (_potentialDuplicatePromptOpen) return;
        _potentialDuplicatePromptOpen = true;
        try
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                IReadOnlyList<PotentialDuplicateCandidate> candidates =
                    await _capture.ReadPotentialDuplicatesAsync(candidate.JournalPath, CancellationToken.None);
                await PresentPotentialDuplicatePromptAsync(candidate.JournalPath, candidates);
            }, System.Windows.Threading.DispatcherPriority.Send).Task.Unwrap();
        }
        finally
        {
            _potentialDuplicatePromptOpen = false;
        }
    }

    private async Task PresentPotentialDuplicatePromptAsync(
        string journalPath,
        IReadOnlyList<PotentialDuplicateCandidate> candidates)
    {
        if (candidates.Count == 0) return;
        int lineCount = candidates.Sum(candidate => candidate.Lines.Count);
        var prompt = new PotentialDuplicatePromptWindow(this, lineCount);
        if (prompt.ShowDialog() == true)
        {
            bool cleanupAvailable = !_journal.HasActiveSession || !string.Equals(
                _journal.ActiveFile,
                journalPath,
                StringComparison.OrdinalIgnoreCase);
            var investigation = new PotentialDuplicateReviewWindow(this, candidates, cleanupAvailable);
            bool? decision = investigation.ShowDialog();
            if (decision == true && investigation.RemoveRequested)
            {
                IReadOnlyList<PotentialDuplicateCandidate> removalRanges =
                    BuildPotentialDuplicateRemovalRanges(candidates, investigation.SelectedReplayLineIndexes);
                await RemovePotentialDuplicatesAsync(journalPath, removalRanges);
                return;
            }

            await MarkPotentialDuplicatesKeptAsync(candidates);
            return;
        }

        await MarkPotentialDuplicatesKeptAsync(candidates);
    }

    private async Task MarkPotentialDuplicatesKeptAsync(
        IReadOnlyList<PotentialDuplicateCandidate> candidates)
    {
        HashSet<Guid> candidateIds = candidates.Select(candidate => candidate.Id).ToHashSet();
        await _capture.MarkPotentialDuplicatesReviewedAsync(
            candidateIds,
            removed: false,
            CancellationToken.None);
        ClearPotentialDuplicateFlags(candidateIds);

        // The capture notification can arrive before the final message-added dispatcher work.
        // Run once more after that queue has drained so "Ignore" cannot leave a stale highlight.
        await Dispatcher.InvokeAsync(
            () => ClearPotentialDuplicateFlags(candidateIds),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ClearPotentialDuplicateFlags(IReadOnlySet<Guid> candidateIds)
    {
        foreach (ChatEntry entry in LiveMessages.Where(
                     entry => entry.PotentialDuplicateGroupId is Guid id && candidateIds.Contains(id)))
        {
            entry.ClearPotentialDuplicateFlag();
        }

        _liveChatView?.Refresh();
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
        if (await RemovePotentialDuplicatesAsync(
                _activePotentialDuplicateReviewPath,
                _activePotentialDuplicateReview))
        {
            FinishPotentialDuplicateReview();
        }
    }

    private async Task<bool> RemovePotentialDuplicatesAsync(
        string journalPath,
        IReadOnlyList<PotentialDuplicateCandidate> candidates)
    {
        if (_journal.HasActiveSession && string.Equals(
                _journal.ActiveFile,
                journalPath,
                StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(
                this,
                "End the active FiveM session before changing its chatlog.",
                "Chatlog is still active",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        int lineCount = candidates.Sum(candidate => candidate.Lines.Count);
        MessageBoxResult confirm = System.Windows.MessageBox.Show(
            this,
            $"Remove exactly {lineCount:N0} highlighted line{(lineCount == 1 ? string.Empty : "s")} from this chatlog?\n\n" +
            "Afterline will create a complete backup first. No unhighlighted line will be changed.",
            "Confirm duplicate removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return false;

        PotentialDuplicateCleanupResult? cleanup = null;
        try
        {
            cleanup = await PotentialDuplicateCleanupService.RemoveAsync(
                journalPath,
                candidates,
                CancellationToken.None);
            await _capture.MarkPotentialDuplicatesReviewedAsync(
                candidates.Select(candidate => candidate.Id),
                removed: true,
                CancellationToken.None);
            await _archiveService.EnsureFileIndexedAsync(
                _settings.ArchiveRoot,
                journalPath,
                CancellationToken.None);
            ClearPotentialDuplicateFlags(candidates.Select(candidate => candidate.Id).ToHashSet());
            System.Windows.MessageBox.Show(
                this,
                $"Removed {cleanup.RemovedLineCount:N0} highlighted line{(cleanup.RemovedLineCount == 1 ? string.Empty : "s")}.\n\n" +
                $"Backup: {StreamerModePresentationService.PathForDisplay(cleanup.BackupPath)}",
                "Chatlog updated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
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
            return cleanup is not null;
        }
    }

    private static IReadOnlyList<PotentialDuplicateCandidate> BuildPotentialDuplicateRemovalRanges(
        IReadOnlyList<PotentialDuplicateCandidate> candidates,
        IReadOnlyDictionary<Guid, IReadOnlyList<int>> selectedIndexes)
    {
        var ranges = new List<PotentialDuplicateCandidate>();
        foreach (PotentialDuplicateCandidate candidate in candidates)
        {
            if (!selectedIndexes.TryGetValue(candidate.Id, out IReadOnlyList<int>? indexes))
                continue;
            int[] ordered = indexes
                .Where(index => index >= 0 && index < candidate.Lines.Count)
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            for (int offset = 0; offset < ordered.Length;)
            {
                int start = ordered[offset];
                int end = start;
                while (offset + 1 < ordered.Length && ordered[offset + 1] == end + 1)
                {
                    end = ordered[++offset];
                }

                ranges.Add(new PotentialDuplicateCandidate
                {
                    Id = candidate.Id,
                    JournalPath = candidate.JournalPath,
                    CandidateStartLine = candidate.CandidateStartLine < 0
                        ? -1
                        : candidate.CandidateStartLine + start,
                    HistoricalStartLine = candidate.HistoricalStartLine,
                    Lines = candidate.Lines.Skip(start).Take(end - start + 1).ToList()
                });
                offset++;
            }
        }

        return ranges;
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
