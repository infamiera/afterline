using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Afterline.Models;

public sealed class ChatEntry
{
    private static readonly Regex TimestampPrefix = new(
        @"^\[(?<hour>\d{1,2}):(?<minute>\d{2}):(?<second>\d{2})\]\s*",
        RegexOptions.Compiled);

    private static readonly Regex PrivateMessagePrefix = new(
        @"^(?:\(\(\s*PM\s+(?:to|from)\b|/pm\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OocCommandPrefix = new(
        @"^/b(?:\s|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OocStatusPrefix = new(
        @"^(?:\[(?:INFO|MAPPING|SUCCESS|ERROR|PM|ANTI-FALL|FRIEND|PAYPHONE|AFK\s+CHECK)\](?:\s|$)|INFO:\s*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex OocStandaloneStatus = new(
        @"^(?:WARNING:|Welcome to GTA World\.?|Weather forecast:|Temperature:|Wind:|Online faction members:|Faction Members (?:Online|On-Duty):|Number:\s*\d|Costs:\s*Initialize Call:|Commands:\s*/payphonecall|You (?:unlocked|locked) the property door\.?|Stats for\s+|Wallet:\s*|Health\s*\||Organization:\s*|Business\s+\d+:|Bank Account Routing:|Current job:|Time\s*\||Properties:\s*Owned:|Custom Number:|Monthly remaining|Premium:|World Points:|Panda Points:|Current Time:|Time spent online|Time remaining on vacation:|You can only tackle one person every\s+\d+\s+seconds!?|You were missclicked, your health \(and/or armour\) has been restored\.?|The door is locked\.?|Vehicle has been flipped!?|Vehicle parked\.?|We've placed a blip on your map to help you locate your vehicle\.?|.*:\s*Press Y to browse (?:ammunation|ammunition)\.?|.*:\s*Press Y to open store\.?|.* changed their character and quit this one\.?|Type /ar \[id\] to handle a report or /tr \[id\] to trash a report\.?|\*\s*\(A\)\s+.*|\[\(\d{1,2}:\d{2}:\d{2}\)\s+id:\s*\d+\s*,\s*by:\s*\(\d+\).*\]:|.*\bhas gone on admin duty\.?|\|\s+\S|={8,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Brush RoleplayBrush = CreateFrozenBrush(0xC2, 0xA2, 0xDA);
    private static readonly Brush PrivateMessageBrush = CreateFrozenBrush(0xFB, 0xF7, 0x24);
    private static readonly Brush OocBrush = CreateFrozenBrush(0xB8, 0xBE, 0xC7);
    private static readonly Brush DefaultBrush = CreateFrozenBrush(0xED, 0xF2, 0xF7);

    public static bool ShowTimestamps { get; set; } = true;
    public static bool ColorizeRoleplayLines { get; set; } = true;

    public DateTime CapturedAt { get; }
    public string Text { get; }
    public bool IsSystemMessage { get; }
    public IReadOnlyList<ChatColorRun> CapturedColorRuns { get; }

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

    public bool IsPrivateMessage
        => !IsSystemMessage && PrivateMessagePrefix.IsMatch(ContentWithoutTimestamp);

    public bool IsInfoLine
    {
        get
        {
            if (IsSystemMessage) return false;
            string content = ContentWithoutTimestamp.TrimStart();
            return OocStatusPrefix.IsMatch(content) ||
                   OocStandaloneStatus.IsMatch(content) ||
                   content.Contains("[NEW LOGIN]", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("[Admin Alert]", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("[Ticket]", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains("[AdmREPORT]", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains(" just toggled the mapping mode ", StringComparison.OrdinalIgnoreCase) ||
                   content.Contains(" triggered a reload of the property", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsOocChatLine
    {
        get
        {
            if (IsSystemMessage || IsPrivateMessage) return false;
            string content = ContentWithoutTimestamp.Trim();
            return content.StartsWith("((", StringComparison.Ordinal) ||
                   OocCommandPrefix.IsMatch(content);
        }
    }

    public bool IsOocLine => IsPrivateMessage || IsOocChatLine || IsInfoLine;

    public string Display
    {
        get
        {
            if (IsSystemMessage) return Text;
            string content = ContentWithoutTimestamp;
            return ShowTimestamps ? $"[{CapturedAt:HH:mm:ss}] {content}" : content;
        }
    }

    public Brush Foreground
    {
        get
        {
            if (IsPrivateMessage) return PrivateMessageBrush;
            if (IsOocChatLine) return OocBrush;
            if (ColorizeRoleplayLines && IsRoleplayLine) return RoleplayBrush;
            return DefaultBrush;
        }
    }

    public IReadOnlyList<ChatColorRun> DisplayColorRuns
        => GetColorRunsForText(Display);

    public ChatEntry(
        DateTime capturedAt,
        string text,
        bool isSystemMessage = false,
        IEnumerable<ChatColorRun>? capturedColorRuns = null)
    {
        Text = text ?? string.Empty;
        IsSystemMessage = isSystemMessage;
        CapturedAt = isSystemMessage ? capturedAt : ResolveTimestamp(capturedAt, Text);
        CapturedColorRuns = isSystemMessage
            ? Array.Empty<ChatColorRun>()
            : ChatColorReliabilityService.EnsureExpectedAccents(Text, capturedColorRuns);
    }

    public static ChatEntry System(DateTime timestamp, string text) => new(timestamp, text, true);

    public IReadOnlyList<ChatColorRun> GetColorRunsForText(string targetText)
    {
        if (IsSystemMessage || CapturedColorRuns.Count == 0 || string.IsNullOrEmpty(targetText))
            return Array.Empty<ChatColorRun>();

        if (string.Equals(Text, targetText, StringComparison.Ordinal))
            return CapturedColorRuns;

        Match sourceTimestamp = TimestampPrefix.Match(Text);
        Match targetTimestamp = TimestampPrefix.Match(targetText);
        int sourceBodyStart = sourceTimestamp.Success ? sourceTimestamp.Length : 0;
        int targetBodyStart = targetTimestamp.Success ? targetTimestamp.Length : 0;
        string sourceBody = Text[sourceBodyStart..].TrimStart();
        string targetBody = targetText[targetBodyStart..].TrimStart();
        sourceBodyStart = Text.Length - sourceBody.Length;
        targetBodyStart = targetText.Length - targetBody.Length;

        if (!string.Equals(sourceBody, targetBody, StringComparison.Ordinal))
            return Array.Empty<ChatColorRun>();

        var mapped = new List<ChatColorRun>();
        if (sourceBodyStart > 0 && targetBodyStart > 0)
        {
            IReadOnlyList<ChatColorRun> timestampRuns = ChatColorData.SliceRuns(
                Text,
                CapturedColorRuns,
                0,
                sourceBodyStart,
                0);

            if (string.Equals(
                    Text[..sourceBodyStart],
                    targetText[..targetBodyStart],
                    StringComparison.Ordinal))
            {
                mapped.AddRange(timestampRuns);
            }
            else if (timestampRuns.Count > 0)
            {
                ChatColorRun color = timestampRuns[0];
                mapped.Add(color with { Start = 0, Length = targetBodyStart });
            }
        }
        else if (sourceBodyStart == 0 && targetBodyStart > 0)
        {
            mapped.Add(new ChatColorRun(
                0,
                targetBodyStart,
                0xA8,
                0xB2,
                0xBE));
        }

        mapped.AddRange(ChatColorData.SliceRuns(
            Text,
            CapturedColorRuns,
            sourceBodyStart,
            sourceBody.Length,
            targetBodyStart));

        return ChatColorData.NormalizeRuns(targetText, mapped);
    }

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
