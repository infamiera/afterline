using System.Windows;
using System.Windows.Controls;

namespace Afterline;

public partial class MainWindow
{
    private bool _cachedReplayUiInitialized;

    private void EnsureCachedReplayUi()
    {
        if (_cachedReplayUiInitialized) return;
        _cachedReplayUiInitialized = true;

        _capture.CachedSessionReplayStarting += Capture_CachedSessionReplayStarting;

        Button? parseButton = FindButtonByContent(LivePage, "Parse current chat");
        if (parseButton is not null)
        {
            parseButton.ToolTip =
                "Reads chat currently retained by FiveM. If FiveM is closed or no live chat can be read, Afterline restores the last session it captured from its persistent local cache.";
        }
    }

    private void Capture_CachedSessionReplayStarting(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Normal,
            new Action(() =>
            {
                LiveMessages.Clear();
                LiveCountText.Text = "0 messages shown";
            }));
    }
}
