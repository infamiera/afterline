using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _dailyLogRolloverUiV054Initialized;

    private void EnsureDailyLogRolloverUiV054()
    {
        if (_dailyLogRolloverUiV054Initialized) return;
        _dailyLogRolloverUiV054Initialized = true;

        _journal.DailyLogRolledOver += Journal_DailyLogRolledOverV054;

        if (_serverStatusText?.Parent is StackPanel panel &&
            !panel.Children.OfType<TextBlock>().Any(block => Equals(block.Tag, "AfterlineDailyRolloverNote")))
        {
            var note = new TextBlock
            {
                Tag = "AfterlineDailyRolloverNote",
                Text = "Daily logs automatically roll over at midnight without interrupting your current session.",
                Foreground = (Brush)FindResource("MutedText"),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };

            int index = panel.Children.IndexOf(_serverStatusText);
            panel.Children.Insert(Math.Min(index + 1, panel.Children.Count), note);
        }
    }

    private async void Journal_DailyLogRolledOverV054(
        object? sender,
        DailyLogRolloverEventArgs e)
    {
        string status = $"Started a new daily chatlog for {e.NewDate:dd MMMM}.";

        await Dispatcher.InvokeAsync(() =>
        {
            if (_liveActionStatus is not null)
                _liveActionStatus.Text = status;
        });

        await Task.Delay(TimeSpan.FromSeconds(6));

        await Dispatcher.InvokeAsync(() =>
        {
            if (_liveActionStatus is not null &&
                string.Equals(_liveActionStatus.Text, status, StringComparison.Ordinal))
                _liveActionStatus.Text = string.Empty;
        });
    }
}
