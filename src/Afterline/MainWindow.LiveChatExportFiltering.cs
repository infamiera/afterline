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
        exportButton.ToolTip = "Exports exactly what is currently visible in Live Chat. IC/OOC visibility and the timestamp toggle are respected.";
        _oocExportFilteringInitialized = true;
    }

    private async void ExportFilteredLiveLog_Click(object sender, RoutedEventArgs e)
    {
        ChatExportFormat? format = ChooseLiveChatExportFormat("the visible Live Chat lines");
        if (format is null) return;

        Button? actionButton = sender as Button;
        if (actionButton is not null) actionButton.IsEnabled = false;

        try
        {
            string path = format == ChatExportFormat.Html
                ? await ExportVisibleLiveChatHtmlAsync(CancellationToken.None)
                : await ExportVisibleLiveChatAsync(GetDownloadsFolder(), CancellationToken.None);
            if (_liveActionStatus is not null)
                _liveActionStatus.Text = $"Saved {Path.GetFileName(path)} to Downloads.";
            ShowExportSuccessNotification(path);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to export the visible Live Chat view.", ex);
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
            .Where(ShouldShowLiveChatEntryV076)
            .ToArray();

        if (visibleEntries.Length == 0)
            throw new InvalidOperationException("There are no visible Live Chat lines to export yet.");

        Directory.CreateDirectory(downloadsFolder);
        DateTime now = DateTime.Now;
        string destination = GetUniqueLiveExportPath(downloadsFolder, now);
        string serverName = GetCurrentServerDisplayName();

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
                : _settings.ShowLiveTimestamps
                    ? $"[{entry.CapturedAt:HH:mm:ss}] {entry.ContentWithoutTimestamp}"
                    : entry.ContentWithoutTimestamp;
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return destination;
    }

    private string GetUniqueLiveExportPath(string folder, DateTime timestamp, string extension = ".txt")
    {
        if (string.IsNullOrWhiteSpace(extension)) extension = ".txt";
        if (!extension.StartsWith(".", StringComparison.Ordinal)) extension = "." + extension;

        string serverName = SanitizeExportFileComponent(GetCurrentServerDisplayName());
        string baseName = $"Chatlog [{serverName}] [{timestamp:dd-MMMM-yyyy}]";
        string path = Path.Combine(folder, baseName + extension);
        if (!File.Exists(path)) return path;

        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(folder, $"{baseName} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private string GetCurrentServerDisplayName()
    {
        // DisplayName deliberately returns "Unknown Server" while discovery is still
        // resolving. Do not let that placeholder mask the friendly name already held
        // by the active journal (for example after a brief FiveM reconnect).
        ServerSessionInfo? currentServer = _capture.CurrentServer;
        if (currentServer?.HasFriendlyName == true)
            return currentServer.DisplayName;

        string? journalName = _journal.ActiveServerName;
        if (IsUsableExportServerName(journalName))
            return journalName!.Trim();

        if (_journal.ResumedServer?.HasFriendlyName == true)
            return _journal.ResumedServer.DisplayName;

        return "Unknown Server";
    }

    private static bool IsUsableExportServerName(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           !string.Equals(value.Trim(), "Unknown Server", StringComparison.OrdinalIgnoreCase) &&
           !value.Trim().StartsWith("Unresolved Server ", StringComparison.OrdinalIgnoreCase) &&
           !ServerSessionInfo.IsGenericServerName(value);

    private static string SanitizeExportFileComponent(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(c => invalid.Contains(c) || c is '[' or ']' ? ' ' : c).ToArray();
        string safe = string.Join(" ", new string(chars).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim(' ', '.');
        if (string.IsNullOrWhiteSpace(safe)) safe = "Unknown Server";
        if (safe.Length > 80) safe = safe[..80].TrimEnd();
        return safe;
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
