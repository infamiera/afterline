using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Afterline.Models;

public sealed class ChatEntry
{
    private static readonly Regex TimestampPrefix = new(
        @"^\[(?<hour>\d{1,2}):(?<minute>\d{2}):(?<second>\d{2})\]\s*",
        RegexOptions.Compiled);

    private static readonly Brush RoleplayBrush = CreateFrozenBrush(0xC2, 0xA2, 0xDA);
    private static readonly Brush DefaultBrush = CreateFrozenBrush(0xED, 0xF2, 0xF7);

    public static bool ShowTimestamps { get; set; } = true;
    public static bool ColorizeRoleplayLines { get; set; } = true;

    public DateTime CapturedAt { get; }
    public string Text { get; }
    public bool IsSystemMessage { get; }

    public string ContentWithoutTimestamp => TimestampPrefix.Replace(Text, string.Empty).TrimStart();

    public bool IsRoleplayLine
    {
        get
        {
            if (IsSystemMessage) return false;
            string content = ContentWithoutTimestamp;
            return content.StartsWith("*", StringComparison.Ordinal) ||
                   content.StartsWith(">", StringComparison.Ordinal);
        }
    }

    public string Display
    {
        get
        {
            if (IsSystemMessage) return Text;
            string content = ContentWithoutTimestamp;
            return ShowTimestamps ? $"[{CapturedAt:HH:mm:ss}] {content}" : content;
        }
    }

    public Brush Foreground => ColorizeRoleplayLines && IsRoleplayLine
        ? RoleplayBrush
        : DefaultBrush;

    public ChatEntry(DateTime capturedAt, string text, bool isSystemMessage = false)
    {
        Text = text ?? string.Empty;
        IsSystemMessage = isSystemMessage;
        CapturedAt = isSystemMessage ? capturedAt : ResolveTimestamp(capturedAt, Text);
    }

    public static ChatEntry System(DateTime timestamp, string text) => new(timestamp, text, true);

    private static DateTime ResolveTimestamp(DateTime observedAt, string text)
    {
        Match match = TimestampPrefix.Match(text);
        if (!match.Success) return observedAt;

        if (!int.TryParse(match.Groups["hour"].Value, out int hour) ||
            !int.TryParse(match.Groups["minute"].Value, out int minute) ||
            !int.TryParse(match.Groups["second"].Value, out int second))
            return observedAt;

        try
        {
            DateTime candidate = observedAt.Date.AddHours(hour).AddMinutes(minute).AddSeconds(second);
            if (candidate > observedAt.AddHours(12)) candidate = candidate.AddDays(-1);
            return candidate;
        }
        catch
        {
            return observedAt;
        }
    }

    private static Brush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
