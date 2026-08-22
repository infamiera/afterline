using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Models;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _rawRecoveryV054Initialized;
    private TextBlock? _rawRecoveryStatusV054;
    private Button? _recoverRawCaptureButtonV054;
    private Button? _saveRawRecoveryButtonV054;
    private readonly RawCaptureFailsafeService _rawRecoveryServiceV054 = new();

    private void EnsureRawCaptureRecoveryV054()
    {
        if (_rawRecoveryV054Initialized) return;
        _rawRecoveryV054Initialized = true;

        if (SettingsPage.Content is not StackPanel settingsStack) return;

        var card = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "Raw Capture Failsafe",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = "Keeps a pre-parse copy of FiveM's visible chat so recent lines can still be recovered after a crash, forced shutdown or interrupted write.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 10)
        });

        _rawRecoveryStatusV054 = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        content.Children.Add(_rawRecoveryStatusV054);

        var actions = new WrapPanel();

        _recoverRawCaptureButtonV054 = new Button
        {
            Content = "Recover raw capture",
            Padding = new Thickness(11, 6, 11, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        _recoverRawCaptureButtonV054.Click += RecoverRawCaptureV054_Click;
        actions.Children.Add(_recoverRawCaptureButtonV054);

        _saveRawRecoveryButtonV054 = new Button
        {
            Content = "Save recovery copy",
            Padding = new Thickness(11, 6, 11, 6)
        };
        _saveRawRecoveryButtonV054.Click += SaveRawRecoveryCopyV054_Click;
        actions.Children.Add(_saveRawRecoveryButtonV054);

        content.Children.Add(actions);
        card.Child = content;

        int insertAt = Math.Max(0, settingsStack.Children.Count - 1);
        settingsStack.Children.Insert(insertAt, card);

        _uiTimer.Tick += RawRecoveryStatusV054_Tick;
        _ = UpdateRawRecoveryStatusV054Async();
    }

    private async void RawRecoveryStatusV054_Tick(object? sender, EventArgs e)
        => await UpdateRawRecoveryStatusV054Async();

    private async Task UpdateRawRecoveryStatusV054Async()
    {
        if (_rawRecoveryStatusV054 is null) return;

        try
        {
            RawCaptureSnapshot? snapshot =
                await _rawRecoveryServiceV054.ReadLatestRecoverableAsync(CancellationToken.None);
            CaptureRunManifest? manifest =
                await _rawRecoveryServiceV054.ReadRunManifestAsync(CancellationToken.None);
            int preserved = _rawRecoveryServiceV054.CountPreservedCrashSnapshots();

            if (snapshot is null)
            {
                _rawRecoveryStatusV054.Text =
                    $"Raw capture backup: Not available · Preserved crash snapshots: {preserved:N0}";
                if (_recoverRawCaptureButtonV054 is not null) _recoverRawCaptureButtonV054.IsEnabled = false;
                if (_saveRawRecoveryButtonV054 is not null) _saveRawRecoveryButtonV054.IsEnabled = false;
                return;
            }

            string state = snapshot.ProcessedAt is null
                ? "Potentially unprocessed"
                : "Parsed safely";
            string previousRun = manifest?.PreviousRunEndedUnexpectedly == true
                ? " · Previous shutdown: Unexpected"
                : string.Empty;

            _rawRecoveryStatusV054.Text =
                $"Raw capture backup: Available · {state}{previousRun}\n" +
                $"Snapshot: {snapshot.CapturedAt.ToString("dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture)} · " +
                $"Server: {snapshot.ServerName} · Preserved crash snapshots: {preserved:N0}";

            if (_recoverRawCaptureButtonV054 is not null) _recoverRawCaptureButtonV054.IsEnabled = true;
            if (_saveRawRecoveryButtonV054 is not null) _saveRawRecoveryButtonV054.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _rawRecoveryStatusV054.Text = "Raw recovery state could not be read.";
            DiagnosticLogger.Error("Unable to update raw capture recovery status.", ex);
        }
    }

    private async void RecoverRawCaptureV054_Click(object sender, RoutedEventArgs e)
    {
        if (_recoverRawCaptureButtonV054 is not null)
            _recoverRawCaptureButtonV054.IsEnabled = false;

        try
        {
            RawCaptureSnapshot? snapshot =
                await _rawRecoveryServiceV054.ReadLatestRecoverableAsync(CancellationToken.None);

            if (snapshot is null || snapshot.Lines.Count == 0)
            {
                if (_rawRecoveryStatusV054 is not null)
                    _rawRecoveryStatusV054.Text = "No raw capture backup is currently available.";
                return;
            }

            LiveMessages.Clear();
            foreach (string line in snapshot.Lines)
                LiveMessages.Add(new ChatEntry(snapshot.CapturedAt, line));

            UpdateVisibleLiveCount();

            if (_notesBookmarksPage is not null) _notesBookmarksPage.Visibility = Visibility.Collapsed;
            if (_logReaderPage is not null) _logReaderPage.Visibility = Visibility.Collapsed;
            ShowPage(LivePage, "Live Chat", "Recovered raw capture snapshot");
            SetLiveActionStatus(
                $"Recovered {snapshot.Lines.Count:N0} raw chat line{(snapshot.Lines.Count == 1 ? string.Empty : "s")} from the failsafe cache.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to replay the raw capture failsafe.", ex);
            if (_rawRecoveryStatusV054 is not null)
                _rawRecoveryStatusV054.Text = "Unable to recover raw capture: " + ex.Message;
        }
        finally
        {
            await UpdateRawRecoveryStatusV054Async();
        }
    }

    private async void SaveRawRecoveryCopyV054_Click(object sender, RoutedEventArgs e)
    {
        if (_saveRawRecoveryButtonV054 is not null)
            _saveRawRecoveryButtonV054.IsEnabled = false;

        try
        {
            string path = await _rawRecoveryServiceV054.SaveRecoveryCopyAsync(CancellationToken.None);
            if (_rawRecoveryStatusV054 is not null)
                _rawRecoveryStatusV054.Text = $"Saved recovery copy: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save a raw recovery copy.", ex);
            if (_rawRecoveryStatusV054 is not null)
                _rawRecoveryStatusV054.Text = "Unable to save recovery copy: " + ex.Message;
        }
        finally
        {
            if (_saveRawRecoveryButtonV054 is not null)
                _saveRawRecoveryButtonV054.IsEnabled = true;
        }
    }
}
