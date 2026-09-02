using System.Windows;
using System.Windows.Controls;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private async Task<string> ExportVisibleLiveChatHtmlAsync(CancellationToken cancellationToken)
    {
        ChatEntry[] visibleEntries = LiveMessages
            .Where(ShouldShowLiveChatEntryV076)
            .ToArray();
        if (visibleEntries.Length == 0)
            throw new InvalidOperationException("There are no visible Live Chat lines to export yet.");

        DateTime now = DateTime.Now;
        string serverName = GetCurrentServerDisplayName();
        string destination = GetUniqueLiveExportPath(GetDownloadsFolder(), now, ".html");
        ChatHtmlExportItem[] lines = visibleEntries
            .Select(entry => new ChatHtmlExportItem(entry, GetDisplayedChatText(entry)))
            .ToArray();

        await ChatHtmlExportService.ExportAsync(
            destination,
            $"Afterline Live Chat — {serverName}",
            "Current visible view · IC/OOC and timestamp settings applied",
            lines,
            _settings.ColorizeRoleplayLines,
            now,
            cancellationToken);
        return destination;
    }

    private async Task<string> ExportCompleteLiveChatHtmlAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> capturedLines = await _journal.ReadCurrentSessionLinesAsync(
            _settings.ArchiveRoot,
            cancellationToken);
        if (capturedLines.Count == 0)
            throw new InvalidOperationException("The current captured session contains no chat lines.");

        DateTime now = DateTime.Now;
        string serverName = GetCurrentServerDisplayName();
        string destination = GetUniqueLiveExportPath(GetDownloadsFolder(), now, ".html");
        ChatHtmlExportItem[] lines = capturedLines
            .Select((line, index) =>
            {
                var entry = new ChatEntry(now, line);
                return new ChatHtmlExportItem(entry, line, index + 1);
            })
            .ToArray();

        await ChatHtmlExportService.ExportAsync(
            destination,
            $"Afterline Live Chat — {serverName}",
            "Complete captured session · Live Chat display filters ignored",
            lines,
            _settings.ColorizeRoleplayLines,
            now,
            cancellationToken);
        return destination;
    }

    private async void ExportLogReaderHtml_Click(object sender, RoutedEventArgs e)
    {
        Button? actionButton = sender as Button;
        if (actionButton is not null) actionButton.IsEnabled = false;

        try
        {
            if (_logReaderList is null || string.IsNullOrWhiteSpace(_logReaderCurrentPath))
                throw new InvalidOperationException("Open a chatlog in Log Reader before exporting it.");

            LogReaderLineItem[] visibleLines = _logReaderList.Items
                .OfType<LogReaderLineItem>()
                .ToArray();
            if (visibleLines.Length == 0)
                throw new InvalidOperationException("There are no visible Log Reader lines to export.");

            DateTime now = DateTime.Now;
            string downloads = GetDownloadsFolder();
            string destination = GetUniqueHtmlExportPath(
                downloads,
                _logReaderCurrentServer,
                now);
            ChatHtmlExportItem[] lines = visibleLines
                .Select(item => new ChatHtmlExportItem(
                    item.Entry,
                    item.Display,
                    item.LineNumber))
                .ToArray();

            await ChatHtmlExportService.ExportAsync(
                destination,
                $"Afterline Log Reader — {Path.GetFileName(_logReaderCurrentPath)}",
                $"{_logReaderCurrentServer} · Current filtered view",
                lines,
                _settings.ColorizeRoleplayLines,
                now,
                CancellationToken.None);

            if (_logReaderStatusText is not null)
                _logReaderStatusText.Text = $"Saved {Path.GetFileName(destination)} to Downloads.";
            ShowExportSuccessNotification(destination);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to export Log Reader as HTML.", ex);
            if (_logReaderStatusText is not null)
                _logReaderStatusText.Text = "Unable to export HTML: " + ex.Message;
        }
        finally
        {
            if (actionButton is not null) actionButton.IsEnabled = true;
        }
    }

    private string GetDisplayedChatText(ChatEntry entry)
        => entry.IsSystemMessage
            ? entry.Text
            : _settings.ShowLiveTimestamps
                ? $"[{entry.CapturedAt:HH:mm:ss}] {entry.ContentWithoutTimestamp}"
                : entry.ContentWithoutTimestamp;

    private static string GetUniqueHtmlExportPath(
        string folder,
        string serverName,
        DateTime timestamp)
    {
        string safeServer = SanitizeExportFileComponent(serverName);
        string baseName = $"Chatlog [{safeServer}] [{timestamp:dd-MMMM-yyyy}]";
        string path = Path.Combine(folder, baseName + ".html");
        if (!File.Exists(path)) return path;

        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(folder, $"{baseName} ({i}).html");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
