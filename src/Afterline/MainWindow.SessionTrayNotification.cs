namespace Afterline;

public partial class MainWindow
{
    private bool _sessionArchiveNotificationInitialized;
    private string? _pendingArchiveNotificationPath;

    private void EnsureSessionArchiveNotification()
    {
        if (_sessionArchiveNotificationInitialized) return;
        _sessionArchiveNotificationInitialized = true;
        EnsureNotificationAndUpdateUi();
    }

    private void ShowSessionArchivedNotification(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        if (_settings.UseWindowsArchiveNotifications && ShowWindowsArchiveNotification(path))
        {
            _pendingArchiveNotificationPath = null;
            return;
        }

        if (!IsVisible || WindowState == System.Windows.WindowState.Minimized)
        {
            _pendingArchiveNotificationPath = Path.GetFullPath(path);
            return;
        }

        _pendingArchiveNotificationPath = null;
        ShowArchiveSuccessNotification(path);
    }

    private bool ShowWindowsArchiveNotification(string path)
    {
        if (_trayIcon is null) return false;

        _lastExportPath = Path.GetFullPath(path);
        _trayIcon.BalloonTipTitle = "Chatlog safely parsed and archived";
        _trayIcon.BalloonTipText =
            $"{Path.GetFileName(path)} was verified and added to the archive. Click to open its location.";
        _trayIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
        _trayIcon.ShowBalloonTip(10_000);
        return true;
    }

    private void ShowPendingArchiveNotification()
    {
        string? path = _pendingArchiveNotificationPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        _pendingArchiveNotificationPath = null;
        if (File.Exists(path)) ShowArchiveSuccessNotification(path);
    }
}
