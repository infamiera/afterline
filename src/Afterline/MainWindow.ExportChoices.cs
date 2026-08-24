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
        visibleButton.ToolTip = "Exports exactly what is currently visible in Live Chat, including the current OOC/INFO and timestamp settings.";

        Button? completeButton = actions.Children.OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "Export complete", StringComparison.Ordinal));
        if (completeButton is null)
        {
            completeButton = new Button
            {
                Content = "Export complete",
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = "Exports the complete current captured session, ignoring Live Chat display filters."
            };
            completeButton.Click += ExportCompleteLiveChat_Click;

            int visibleIndex = actions.Children.IndexOf(visibleButton);
            actions.Children.Insert(Math.Min(visibleIndex + 1, actions.Children.Count), completeButton);
        }

        if (actions.Children.OfType<Button>().Any(button => string.Equals(button.Content?.ToString(), "Export HTML", StringComparison.Ordinal)))
            return;

        var htmlButton = new Button
        {
            Content = "Export HTML",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = "Exports the currently visible Live Chat as a self-contained HTML file with the displayed chat colors."
        };
        htmlButton.Click += ExportVisibleLiveChatHtml_Click;

        int completeIndex = actions.Children.IndexOf(completeButton);
        actions.Children.Insert(Math.Min(completeIndex + 1, actions.Children.Count), htmlButton);
    }

    private async void ExportCompleteLiveChat_Click(object sender, RoutedEventArgs e)
    {
        Button? actionButton = sender as Button;
        if (actionButton is not null) actionButton.IsEnabled = false;

        try
        {
            string downloads = GetDownloadsFolder();
            string temporary = await _journal.ExportCurrentLogAsync(_settings.ArchiveRoot, downloads, CancellationToken.None);
            string destination = GetUniqueLiveExportPath(downloads, DateTime.Now);

            if (!string.Equals(Path.GetFullPath(temporary), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                ChatColorSidecarService.DeleteForTextFile(destination);
                File.Move(temporary, destination, false);
                try
                {
                    ChatColorSidecarService.MoveForTextFile(
                        temporary,
                        destination,
                        overwrite: false);
                }
                catch (Exception ex)
                {
                    DiagnosticLogger.Error("Unable to move exact color metadata with the exported chatlog.", ex);
                }
            }
            else
                destination = temporary;

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
}
