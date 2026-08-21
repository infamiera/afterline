using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Afterline.Services;

internal sealed record EditorChatSegment(string Text, Color Color);

internal sealed record EditorChatLine(IReadOnlyList<EditorChatSegment> Segments);

internal static class EditorChatFormatter
{
    private static readonly Regex TimestampPrefix = new(
        @"^\s*(?<timestamp>\[\d{1,2}:\d{2}:\d{2}\])\s*(?<body>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex LeadingBracketTag = new(
        @"^\[(?<tag>[^\]]+)\](?<separator>:?)\s*(?<rest>.*)$",
        RegexOptions.Compiled);

    internal static readonly Color Blue = Color.FromRgb(0x16, 0x9B, 0xFF);
    internal static readonly Color Yellow = Color.FromRgb(0xFF, 0xF3, 0x00);
    internal static readonly Color Purple = Color.FromRgb(0xC2, 0xA2, 0xDA);
    internal static readonly Color Green = Color.FromRgb(0x20, 0xE8, 0x5A);
    internal static readonly Color White = Color.FromRgb(0xF2, 0xF2, 0xF2);
    internal static readonly Color Gray = Color.FromRgb(0xB8, 0xBE, 0xC7);
    internal static readonly Color Orange = Color.FromRgb(0xFF, 0xA5, 0x1F);
    internal static readonly Color Red = Color.FromRgb(0xFF, 0x3B, 0x30);
    internal static readonly Color Radio = Color.FromRgb(0xC8, 0xB4, 0x5A);
    internal static readonly Color MutedTimestamp = Color.FromRgb(0xA8, 0xB2, 0xBE);

    public static IReadOnlyList<EditorChatLine> FormatLines(string input, bool showTimestamps)
    {
        string normalized = (input ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        var result = new List<EditorChatLine>(lines.Length);
        bool emergencyBlock = false;

        foreach (string rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                result.Add(new EditorChatLine(Array.Empty<EditorChatSegment>()));
                emergencyBlock = false;
                continue;
            }

            string body = rawLine.TrimEnd();
            string? timestamp = null;
            Match timestampMatch = TimestampPrefix.Match(body);
            if (timestampMatch.Success)
            {
                timestamp = timestampMatch.Groups["timestamp"].Value;
                body = timestampMatch.Groups["body"].Value;
            }

            IReadOnlyList<EditorChatSegment> bodySegments;
            if (IsEmergencyHeader(body))
            {
                emergencyBlock = true;
                bodySegments = Single(body, Blue);
            }
            else if (emergencyBlock && body.TrimStart().StartsWith("*", StringComparison.Ordinal) && body.Contains(':'))
            {
                bodySegments = FormatEmergencyDetail(body);
            }
            else
            {
                emergencyBlock = false;
                bodySegments = FormatBody(body);
            }

            if (!showTimestamps || string.IsNullOrWhiteSpace(timestamp))
            {
                result.Add(new EditorChatLine(bodySegments));
                continue;
            }

            var withTimestamp = new List<EditorChatSegment>(bodySegments.Count + 1)
            {
                new(timestamp + " ", MutedTimestamp)
            };
            withTimestamp.AddRange(bodySegments);
            result.Add(new EditorChatLine(withTimestamp));
        }

        return result;
    }

    private static IReadOnlyList<EditorChatSegment> FormatBody(string body)
    {
        string trimmed = body.TrimStart();
        if (trimmed.Length == 0) return Array.Empty<EditorChatSegment>();

        if (trimmed.StartsWith("((", StringComparison.Ordinal))
        {
            bool pm = ContainsAny(trimmed, " PM ", "PM to", "PM from", "Incoming PM", "Outgoing PM", "Gelen PM", "Giden PM");
            return Single(body, pm ? Yellow : Gray);
        }

        if (IsPhoneAction(trimmed) || trimmed.StartsWith("*", StringComparison.Ordinal) || trimmed.StartsWith(">", StringComparison.Ordinal))
            return Single(body, Purple);

        if (trimmed.StartsWith("**", StringComparison.Ordinal))
            return Single(body, ContainsAny(trimmed, "->", "[S:", "[CH:") ? Radio : Yellow);

        if (ContainsAny(trimmed, "[Intercom]", "[İnterkom]", "[Interkom]"))
            return Single(body, Blue);

        if (ContainsAny(trimmed, "[Microphone]", "[Mikrofon]", "[Megaphone]", "[Megafon]", "(Phone)", "(Telefon)"))
            return Single(body, Yellow);

        if (ContainsAny(trimmed, "(iFruit ", "message from", "group message"))
            return Single(body, Yellow);

        Match tagMatch = LeadingBracketTag.Match(trimmed);
        if (tagMatch.Success)
            return FormatTaggedLine(body, trimmed, tagMatch);

        if (ContainsAny(trimmed, "whispers:", "whispered:", "fısıldar:", "fısıldadı:"))
        {
            if (ContainsAny(trimmed, "(Vehicle)", "(Araç İçi)")) return Single(body, Yellow);
            return Single(body, Orange);
        }

        if (LooksLikeSuccessOrTransfer(trimmed))
            return Single(body, Green);

        if (LooksLikePlacedItem(trimmed))
            return Single(body, Orange);

        if (LooksLikeRemovedItem(trimmed))
            return Single(body, Red);

        if (LooksLikeFoundItem(trimmed))
            return Single(body, Blue);

        return Single(body, White);
    }

    private static IReadOnlyList<EditorChatSegment> FormatTaggedLine(string original, string trimmed, Match match)
    {
        string tag = match.Groups["tag"].Value;
        string separator = match.Groups["separator"].Value;
        string rest = match.Groups["rest"].Value;
        int leadingWhitespace = original.Length - trimmed.Length;
        string prefixWhitespace = leadingWhitespace > 0 ? original[..leadingWhitespace] : string.Empty;
        string visibleTag = prefixWhitespace + "[" + tag + "]" + separator;
        string remainder = string.IsNullOrEmpty(rest) ? string.Empty : " " + rest;

        if (EqualsAny(tag, "CK"))
            return Segments((visibleTag, Blue), (remainder, Red));

        if (EqualsAny(tag, "ERROR", "HATA", "WARNING"))
            return Segments((visibleTag, Red), (remainder, Red));

        if (EqualsAny(tag, "INFO", "BİLGİ", "BILGI", "CASHTAP"))
            return Segments((visibleTag, Green), (remainder, White));

        if (ContainsAny(tag, "ANTI-FALL", "ANTI FALL"))
            return Segments((visibleTag, Blue), (remainder, White));

        return Segments((visibleTag, Blue), (remainder, White));
    }

    private static IReadOnlyList<EditorChatSegment> FormatEmergencyDetail(string body)
    {
        int colon = body.IndexOf(':');
        if (colon < 0) return Single(body, Blue);
        return Segments(
            (body[..(colon + 1)], Blue),
            (body[(colon + 1)..], White));
    }

    private static bool IsEmergencyHeader(string body)
        => ContainsAny(body, "EMERGENCY CALL", "ACİL ÇAĞRI", "ACIL CAGRI");

    private static bool IsPhoneAction(string body)
        => (body.StartsWith("(Phone)", StringComparison.OrdinalIgnoreCase) ||
            body.StartsWith("(Telefon)", StringComparison.OrdinalIgnoreCase)) &&
           body.Contains('*');

    private static bool LooksLikeSuccessOrTransfer(string body)
        => ContainsAny(body,
            "gave you", "you gave", "transfer completed", "transfer successful", "successfully transferred",
            "adlı kişi sana", "adlı kişiye", "kişisine $", "Transfer başarıyla", "Transfer basariyla",
            "used ", "kullandın", "kullandin");

    private static bool LooksLikePlacedItem(string body)
        => ContainsAny(body,
            "placed in property", "placed in vehicle", "placed ",
            "Mülke ", "Mulke ", "Araca ") &&
           ContainsAny(body, "placed", "yerleştirdin", "yerlestirdin");

    private static bool LooksLikeRemovedItem(string body)
        => ContainsAny(body,
            "removed from property", "took from property", "Mülkün içerisinden", "Mulkun icerisinden");

    private static bool LooksLikeFoundItem(string body)
        => ContainsAny(body,
            "found in property", "found in vehicle", "Mülkün içerisinde", "Mulkun icerisinde");

    private static bool ContainsAny(string source, params string[] values)
        => values.Any(value => source.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool EqualsAny(string source, params string[] values)
        => values.Any(value => string.Equals(source, value, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<EditorChatSegment> Single(string text, Color color)
        => new[] { new EditorChatSegment(text, color) };

    private static IReadOnlyList<EditorChatSegment> Segments(params (string Text, Color Color)[] values)
        => values.Where(value => value.Text.Length > 0)
            .Select(value => new EditorChatSegment(value.Text, value.Color))
            .ToArray();
}
