using System.Windows;
using System.Windows.Controls;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _exportChoicesInitialized;

    private void EnsureExportChoices()
    {
        if (_exportChoicesInitialized) return;
        _exportChoicesInitialized = true;

        Button? visibleButton = FindButtonByContent(LivePage, "Save copy to Downloads") ??
                                FindButtonByContent(LivePage, "Export visible");
        if (visibleButton?.Parent is not Panel actions) return;

        visibleButton.Content = "Export visible";
        visibleButton.ToolTip = "Exports the current Live Chat view as plain TXT or self-contained colored HTML.";

        Button? completeButton = actions.Children.OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Export complete", StringComparison.Ordinal));
        if (completeButton is null)
        {
            completeButton = new Button
            {
                Content = "Export complete",
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 10, 6),
                ToolTip = "Exports the complete captured session as plain TXT or self-contained colored HTML, ignoring display filters."
            };
            completeButton.Click += ExportCompleteLiveChat_Click;

            int visibleIndex = actions.Children.IndexOf(visibleButton);
            actions.Children.Insert(Math.Min(visibleIndex + 1, actions.Children.Count), completeButton);
        }

        Button? obsoleteHtmlButton = actions.Children.OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Export HTML", StringComparison.Ordinal));
        if (obsoleteHtmlButton is not null)
            actions.Children.Remove(obsoleteHtmlButton);
    }

    private async void ExportCompleteLiveChat_Click(object sender, RoutedEventArgs e)
    {
        ChatExportFormat? format = ChooseLiveChatExportFormat("the complete captured session");
        if (format is null) return;

        Button? actionButton = sender as Button;
        if (actionButton is not null) actionButton.IsEnabled = false;

        try
        {
            string destination = format == ChatExportFormat.Html
                ? await ExportCompleteLiveChatHtmlAsync(CancellationToken.None)
                : await _journal.ExportCurrentLogAsync(
                    _settings.ArchiveRoot,
                    GetDownloadsFolder(),
                    CancellationToken.None);

            SetLiveActionStatus($"Saved {Path.GetFileName(destination)} to Downloads.");
            ShowExportSuccessNotification(destination);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to export the complete Live Chat session.", ex);
            SetLiveActionStatus("Unable to export complete session: " + ex.Message);
        }
        finally
        {
            if (actionButton is not null) actionButton.IsEnabled = true;
        }
    }

    private ChatExportFormat? ChooseLiveChatExportFormat(string scope)
    {
        var prompt = new ChatExportFormatWindow(scope) { Owner = this };
        return prompt.ShowDialog() == true ? prompt.SelectedFormat : null;
    }
}
