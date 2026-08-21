using System.Windows;

namespace Afterline;

public partial class MainWindow
{
    private bool _logReaderPresentationSyncInitialized;

    private void EnsureLogReaderPresentationSync()
    {
        if (_logReaderPresentationSyncInitialized) return;
        _logReaderPresentationSyncInitialized = true;

        if (_showOocChatCheck is not null)
        {
            _showOocChatCheck.Checked += LivePresentationChangedForReader;
            _showOocChatCheck.Unchecked += LivePresentationChangedForReader;
        }

        if (_roleplayColorsCheck is not null)
        {
            _roleplayColorsCheck.Checked += LivePresentationChangedForReader;
            _roleplayColorsCheck.Unchecked += LivePresentationChangedForReader;
        }

        if (_showLiveTimestampsCheck is not null)
        {
            _showLiveTimestampsCheck.Checked += LivePresentationChangedForReader;
            _showLiveTimestampsCheck.Unchecked += LivePresentationChangedForReader;
        }

        SyncLogReaderPresentationFromSettings();
    }

    private void LivePresentationChangedForReader(object sender, RoutedEventArgs e)
        => Dispatcher.BeginInvoke(new Action(SyncLogReaderPresentationFromSettings));

    private void SyncLogReaderPresentationFromSettings()
    {
        if (_logReaderOocCheck is not null && _logReaderOocCheck.IsChecked != _settings.ShowOocChat)
            _logReaderOocCheck.IsChecked = _settings.ShowOocChat;
        if (_logReaderRpCheck is not null && _logReaderRpCheck.IsChecked != _settings.ColorizeRoleplayLines)
            _logReaderRpCheck.IsChecked = _settings.ColorizeRoleplayLines;
        if (_logReaderTimestampCheck is not null && _logReaderTimestampCheck.IsChecked != _settings.ShowLiveTimestamps)
            _logReaderTimestampCheck.IsChecked = _settings.ShowLiveTimestamps;

        _logReaderView?.Refresh();
        _logReaderList?.Items.Refresh();
        UpdateLogReaderStatus();
    }
}
