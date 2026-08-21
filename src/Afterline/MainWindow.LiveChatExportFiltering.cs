using System.Text;
using System.Windows;
using System.Windows.Controls;
using Afterline.Models;

namespace Afterline;

public partial class MainWindow
{
    private bool _oocExportFilteringInitialized;

    private void EnsureOocExportFiltering()
    {
        if (_oocExportFilteringInitialized) return;

        Button? exportButton = FindButtonByContent(LivePage, "Save copy to Downloads");
        if (exportButton is null) return;

        exportButton.Click -= ExportCurrentLiveLog_Click;
        exportButton.Click += ExportFilteredLiveLog_Click;
        exportButton.ToolTip = "Writes an independent copy of the current Live Chat to your Downloads folder. The Show OOC chat toggle is respected in the exported copy.";
        _oocExportFilteringInitialized = true;
    }

    private async void ExportFilteredLiveLog_Click(object sender, RoutedEventArgs e)
    {
        Button? actionButton = sender as Button;
        if (actionButton is not null) actionButton.IsEnabled = false;

        try
        {
            string downloads = GetDownloadsFolder();
            string path = await ExportVisibleLiveChatAsync(downloads, CancellationToken.None);
            if (_liveActionStatus is not null)
                _liveActionStatus.Text = $"Saved {Path.GetFileName(path)} to Downloads.";
            ShowExportSuccessNotification(path);
        }
        catch (Exception ex)
        {
            if (_liveActionStatus is not null)
                _liveActionStatus.Text = "Unable to save log copy: " + ex.Message;
        }
        finally
        {
            if (actionButton is not null) actionButton.IsEnabled = true;
        }
    }

    private async Task<string> ExportVisibleLiveChatAsync(string downloadsFolder, CancellationToken cancellationToken)
    {
        ChatEntry[] visibleEntries = LiveMessages
            .Where(entry => _settings.ShowOocChat || !entry.IsOocLine)
            .ToArray();

        if (visibleEntries.Length == 0)
            throw new InvalidOperationException("There are no visible Live Chat lines to export yet.");

        Directory.CreateDirectory(downloadsFolder);
        DateTime now = DateTime.Now;
        string destination = GetUniqueLiveExportPath(downloadsFolder, now);

        string serverName = _capture.CurrentServer?.DisplayName ?? _journal.ActiveServerName ?? "Unknown Server";
        if (string.IsNullOrWhiteSpace(serverName)) serverName = "Unknown Server";

        await using FileStream stream = new(
            destination,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using StreamWriter writer = new(stream, new UTF8Encoding(false));

        await writer.WriteLineAsync(
            $"[AFTERLINE LIVE EXPORT: {serverName} · {now:yyyy-MM-dd HH:mm:ss}]".AsMemory(),
            cancellationToken);

        foreach (ChatEntry entry in visibleEntries)
        {
            string line = entry.IsSystemMessage
                ? entry.Text
                : $"[{entry.CapturedAt:HH:mm:ss}] {entry.ContentWithoutTimestamp}";
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return destination;
    }

    private static string GetUniqueLiveExportPath(string folder, DateTime timestamp)
    {
        string baseName = $"Chatlog Export [{timestamp:dd-MMMM-yyyy - HH-mm-ss}]";
        string path = Path.Combine(folder, baseName + ".txt");
        if (!File.Exists(path)) return path;

        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(folder, $"{baseName} ({i}).txt");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private static Button? FindButtonByContent(DependencyObject root, string content)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is Button button && string.Equals(button.Content?.ToString(), content, StringComparison.Ordinal))
                return button;

            if (child is DependencyObject dependencyObject)
            {
                Button? found = FindButtonByContent(dependencyObject, content);
                if (found is not null) return found;
            }
        }

        return null;
    }
}
