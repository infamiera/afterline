using System.Windows;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private async void RemoveLogReaderLine_Click(object sender, RoutedEventArgs e)
    {
        if (_logReaderList?.SelectedItem is not LogReaderLineItem selected ||
            string.IsNullOrWhiteSpace(_logReaderCurrentPath))
        {
            return;
        }

        if (selected.Entry.IsSystemMessage)
        {
            MessageBox.Show(
                this,
                "Session and server boundary rows are protected and cannot be removed here.",
                "Remove line",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_journal.HasActiveSession && string.Equals(
                _journal.ActiveFile,
                _logReaderCurrentPath,
                StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                this,
                "This chatlog is currently receiving live chat. End the session before permanently removing a line so capture remains uninterrupted.",
                "Live chatlog protected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            $"Remove line {selected.LineNumber:N0} from this chatlog?\n\n" +
            "Afterline creates a full backup first. Notes and bookmarks below this row may need checking because their line numbers can shift.",
            "Confirm line removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            ManualChatLineRemovalResult result = await ManualChatLineRemovalService.RemoveAtAsync(
                _logReaderCurrentPath,
                selected.LineNumber - 1,
                selected.RawLine,
                CancellationToken.None);

            await _archiveService.EnsureFileIndexedAsync(
                _settings.ArchiveRoot,
                _logReaderCurrentPath,
                CancellationToken.None);
            await OpenLogInReaderAsync(_logReaderCurrentPath, Math.Max(1, selected.LineNumber - 1));

            MessageBox.Show(
                this,
                $"Removed exactly line {selected.LineNumber:N0}.\n\nBackup: {StreamerModePresentationService.PathForDisplay(result.BackupPath)}",
                "Chatlog updated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to remove the selected Log Reader row.", ex);
            MessageBox.Show(
                this,
                "The chatlog was left unchanged.\n\n" + ex.Message,
                "Chatlog was not changed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RemoveSelectedLiveLine_Click(object sender, RoutedEventArgs e)
    {
        ChatEntry? selected = _liveRightClickTargetV053 ?? LiveChatList.SelectedItem as ChatEntry;
        if (selected is null) return;

        if (selected.IsSystemMessage || selected.IsPotentialDuplicateReviewClone)
        {
            MessageBox.Show(
                this,
                "This row is protected. Use the duplicate-review controls for comparison rows or keep session boundary rows intact.",
                "Remove from Live Chat",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            this,
            "Remove this line from the Live Chat display?\n\n" +
            "The active .txt chatlog will not be rewritten while capture is running. This keeps live logging uninterrupted; use Log Reader after the session for permanent removal.",
            "Remove from Live Chat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        if (!LiveMessages.Remove(selected)) return;

        _liveChatView?.Refresh();
        UpdateVisibleLiveCount();
        if (_liveActionStatus is not null)
        {
            _liveActionStatus.Text = "Line removed from the Live Chat display. The saved chatlog was not changed.";
        }
    }
}
