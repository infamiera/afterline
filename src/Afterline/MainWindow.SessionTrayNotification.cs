using System.Diagnostics;
using Forms = System.Windows.Forms;

namespace Afterline;

public partial class MainWindow
{
    private bool _sessionTrayNotificationInitialized;
    private string? _lastTraySavedPath;

    private void EnsureSessionTrayNotification()
    {
        if (_sessionTrayNotificationInitialized) return;
        _sessionTrayNotificationInitialized = true;

        _capture.SessionFinalized += Capture_SessionFinalizedTrayNotification;
        if (_trayIcon is not null)
            _trayIcon.BalloonTipClicked += TrayIcon_BalloonTipClicked;
    }

    private void Capture_SessionFinalizedTrayNotification(object? sender, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _lastTraySavedPath = Path.GetFullPath(path);
            if (_trayIcon is null) return;

            string fileName = Path.GetFileName(path);
            string message = $"{fileName}\nChatlog saved successfully. Click to open its folder.";
            if (message.Length > 240) message = message[..240];

            _trayIcon.ShowBalloonTip(
                8000,
                "Afterline — Chatlog saved",
                message,
                Forms.ToolTipIcon.Info);
        }));
    }

    private void TrayIcon_BalloonTipClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastTraySavedPath) || !File.Exists(_lastTraySavedPath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_lastTraySavedPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Services.DiagnosticLogger.Error("Unable to open the saved chatlog from the tray notification.", ex);
        }
    }
}
