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

        if (!IsVisible || WindowState == System.Windows.WindowState.Minimized)
        {
            _pendingArchiveNotificationPath = Path.GetFullPath(path);
            return;
        }

        _pendingArchiveNotificationPath = null;
        ShowArchiveSuccessNotification(path);
    }

    private void ShowPendingArchiveNotification()
    {
        string? path = _pendingArchiveNotificationPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        _pendingArchiveNotificationPath = null;
        if (File.Exists(path)) ShowArchiveSuccessNotification(path);
    }
}
