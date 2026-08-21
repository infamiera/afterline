using System.Windows.Media;

namespace Afterline.Models;

public sealed class ChatEntry
{
    public DateTime CapturedAt { get; }
    public string Text { get; }
    public string Display => $"{CapturedAt:HH:mm:ss}   {Text}";
    public Brush Foreground { get; }

    public ChatEntry(DateTime capturedAt, string text, bool colorize = false)
    {
        CapturedAt = capturedAt;
        Text = text;
        Foreground = colorize ? ChatColorizer.GetBrush(text) : ChatColorizer.DefaultBrush;
    }

    public ChatEntry WithColorization(bool enabled) => new(CapturedAt, Text, enabled);
}
