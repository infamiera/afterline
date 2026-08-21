namespace Afterline.Models;

public sealed class ChatEntry
{
    public DateTime CapturedAt { get; }
    public string Text { get; }
    public string Display => $"{CapturedAt:HH:mm:ss}   {Text}";

    public ChatEntry(DateTime capturedAt, string text)
    {
        CapturedAt = capturedAt;
        Text = text;
    }
}
