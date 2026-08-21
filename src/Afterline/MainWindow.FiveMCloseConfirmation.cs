using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _fiveMCloseConfirmationInitialized;
    private bool _fiveMWasRunningForConfirmation;
    private bool _pendingFiveMCloseConfirmation;
    private string? _lastSavedChatlogPath;

    private void EnsureFiveMCloseSaveConfirmation()
    {
        if (_fiveMCloseConfirmationInitialized) return;
        _fiveMCloseConfirmationInitialized = true;

        _fiveMWasRunningForConfirmation = FiveMProcessService.IsRunning();
        _capture.SessionFinalized += Capture_RememberFinalizedChatlog;
        _uiTimer.Tick += FiveMCloseConfirmation_Tick;
    }

    private void Capture_RememberFinalizedChatlog(object? sender, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

        _lastSavedChatlogPath = Path.GetFullPath(path);

        if (_pendingFiveMCloseConfirmation && !FiveMProcessService.IsRunning())
            Dispatcher.BeginInvoke(new Action(TryShowFiveMClosedSaveConfirmation));
    }

    private void FiveMCloseConfirmation_Tick(object? sender, EventArgs e)
    {
        if (_isExiting) return;

        bool running = FiveMProcessService.IsRunning();
        if (running)
        {
            _fiveMWasRunningForConfirmation = true;
            _pendingFiveMCloseConfirmation = false;
            return;
        }

        if (!_fiveMWasRunningForConfirmation) return;

        _fiveMWasRunningForConfirmation = false;
        _pendingFiveMCloseConfirmation = true;

        // If the current session was already finalized before FiveM closed,
        // there is nothing left for the capture worker to flush. Show the
        // confirmation immediately using the most recently saved chatlog.
        if (!_journal.HasActiveSession)
            TryShowFiveMClosedSaveConfirmation();
    }

    private void TryShowFiveMClosedSaveConfirmation()
    {
        if (!_pendingFiveMCloseConfirmation || _isExiting) return;
        if (FiveMProcessService.IsRunning()) return;
        if (string.IsNullOrWhiteSpace(_lastSavedChatlogPath) || !File.Exists(_lastSavedChatlogPath)) return;

        _pendingFiveMCloseConfirmation = false;

        string message =
            "FiveM has closed.\n\n" +
            "Your chatlogs have been successfully saved to:\n\n" +
            _lastSavedChatlogPath;

        if (IsVisible && WindowState != System.Windows.WindowState.Minimized)
        {
            System.Windows.MessageBox.Show(
                this,
                message,
                "Afterline — Chatlogs saved",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        else
        {
            System.Windows.MessageBox.Show(
                message,
                "Afterline — Chatlogs saved",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }
}
