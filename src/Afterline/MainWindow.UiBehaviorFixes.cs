using System.ComponentModel;
using System.Windows.Data;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _uiBehaviorFixesInitialized;
    private ICollectionView? _liveChatView;

    private void EnsureUiBehaviorFixes()
    {
        if (_uiBehaviorFixesInitialized) return;
        _uiBehaviorFixesInitialized = true;

        _liveChatView = CollectionViewSource.GetDefaultView(LiveMessages);
        _liveChatView.Filter = ShouldShowLiveChatEntry;
        LiveChatList.ItemsSource = _liveChatView;

        if (_showOocChatCheck is not null)
        {
            _showOocChatCheck.Checked += OocDisplayFilter_Changed;
            _showOocChatCheck.Unchecked += OocDisplayFilter_Changed;
        }

        _uiTimer.Tick += CompactFiveMDetectedStatus_Tick;

        _liveChatView.Refresh();
        UpdateVisibleLiveCount();
        ApplyCompactFiveMDetectedStatus();
    }

    private bool ShouldShowLiveChatEntry(object item)
        => item is not ChatEntry entry || _settings.ShowOocChat || !entry.IsOocLine;

    private void OocDisplayFilter_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        _liveChatView?.Refresh();
        UpdateVisibleLiveCount();
    }

    private void CompactFiveMDetectedStatus_Tick(object? sender, EventArgs e)
        => ApplyCompactFiveMDetectedStatus();

    private void ApplyCompactFiveMDetectedStatus()
    {
        if (_capture.State != CaptureState.WaitingForNui) return;

        TopStatusText.Text = "FiveM Detected";
        TrayStateText.Text = "FiveM Detected";

        if (_trayIcon is not null)
            _trayIcon.Text = "Afterline — FiveM Detected";
    }
}
