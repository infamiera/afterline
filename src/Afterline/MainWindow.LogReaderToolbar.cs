using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _logReaderToolbarInitialized;

    private void EnsureLogReaderToolbar()
    {
        if (_logReaderToolbarInitialized || _logReaderPage is null) return;

        Border? headerCard = _logReaderPage.Children
            .OfType<Border>()
            .FirstOrDefault(border => Grid.GetRow(border) == 0);
        if (headerCard?.Child is not Grid headerGrid) return;

        WrapPanel? options = headerGrid.Children.OfType<WrapPanel>().FirstOrDefault();
        if (options is null) return;

        _logReaderToolbarInitialized = true;

        var archiveButton = new Button
        {
            Content = "Archive",
            Height = 34,
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Return to the Archive tab."
        };
        archiveButton.Click += LogReaderArchive_Click;

        var openFolderButton = new Button
        {
            Content = "Open Folder",
            Height = 34,
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Choose a .txt chatlog from your archive and open it in Log Reader."
        };
        openFolderButton.Click += LogReaderOpenFolder_Click;

        var exportHtmlButton = new Button
        {
            Content = "Export HTML",
            Height = 34,
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Exports the currently opened and filtered log as a self-contained HTML file with the displayed chat colors."
        };
        exportHtmlButton.Click += ExportLogReaderHtml_Click;

        options.Children.Insert(0, openFolderButton);
        options.Children.Insert(0, archiveButton);
        options.Children.Insert(Math.Min(2, options.Children.Count), exportHtmlButton);
    }

    private async void LogReaderArchive_Click(object sender, RoutedEventArgs e)
    {
        if (_logReaderPage is not null) _logReaderPage.Visibility = Visibility.Collapsed;
        ShowPage(ArchivePage, "Archive", "Browse plain-text sessions organized by year and month");
        await RefreshArchiveAsync();
    }

    private async void LogReaderOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_settings.ArchiveRoot);
            var dialog = new OpenFileDialog
            {
                Title = "Open chatlog in Afterline",
                InitialDirectory = _settings.ArchiveRoot,
                Filter = "Chatlog text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
                await OpenLogInReaderAsync(dialog.FileName, null);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to choose a chatlog for Log Reader.", ex);
            System.Windows.MessageBox.Show(this, ex.Message, "Unable to open chatlog", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
