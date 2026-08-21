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

        if (actions.Children.OfType<Button>().Any(button => string.Equals(button.Content?.ToString(), "Export complete", StringComparison.Ordinal)))
            return;

        var completeButton = new Button
        {
            Content = "Export complete",
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = "Exports the complete current captured session, ignoring Live Chat display filters."
        };
        completeButton.Click += ExportCompleteLiveChat_Click;

        int index = actions.Children.IndexOf(visibleButton);
        actions.Children.Insert(Math.Min(index + 1, actions.Children.Count), completeButton);
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
                File.Move(temporary, destination, false);
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
