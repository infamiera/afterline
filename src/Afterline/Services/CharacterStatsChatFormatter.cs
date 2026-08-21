using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Afterline.Services;

internal static class CharacterStatsChatFormatter
{
    private static readonly Regex BusinessLabel = new(@"^Business\s+\d+:", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MoneyValue = new(@"\$[\d,]+", RegexOptions.Compiled);
    private static readonly Regex CurrentlyWorkedValue = new(@"\b\d+(?:\.\d+)?/\d+(?:\.\d+)?\s+hours?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CurrentDateValue = new(@"\b\d{1,2}/[A-Za-z]{3}/\d{4}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ClockValue = new(@"\b\d{1,2}:\d{2}:\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex FinalHoursValue = new(@"\b\d+(?:\.\d+)?\s+hours?\.\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FinalVacationValue = new(@"\b\d+(?:\.\d+)?\s+day(?:\(s\)|s)?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumberAfterColon = new(@":\s*(?<value>\d[\d,.]*)", RegexOptions.Compiled);
    private static readonly Regex ValueAfterColon = new(@":\s*(?<value>[^|]+)", RegexOptions.Compiled);

    internal static bool TryFormat(string body, out IReadOnlyList<EditorChatSegment> segments)
    {
        segments = Array.Empty<EditorChatSegment>();
        string trimmed = body.TrimStart();
        if (trimmed.Length == 0) return false;

        if (trimmed.StartsWith("Stats for ", StringComparison.OrdinalIgnoreCase))
        {
            segments = HighlightLiteral(body, "Stats for", EditorChatFormatter.Yellow, EditorChatFormatter.White);
            return true;
        }

        if (trimmed.StartsWith("Wallet:", StringComparison.OrdinalIgnoreCase) &&
            trimmed.Contains("Bank:", StringComparison.OrdinalIgnoreCase) &&
            trimmed.Contains("Total Assets:", StringComparison.OrdinalIgnoreCase))
        {
            var highlights = new List<(int Index, int Length, Color Color)>();
            AddLiteral(highlights, body, "Wallet:", EditorChatFormatter.Yellow);
            AddLiteral(highlights, body, "Bank:", EditorChatFormatter.Yellow);
            AddLiteral(highlights, body, "Total Assets:", EditorChatFormatter.Yellow);
            AddMatches(highlights, MoneyValue, body, EditorChatFormatter.Green);
            segments = HighlightRanges(body, EditorChatFormatter.White, highlights);
            return true;
        }

        if (trimmed.StartsWith("Health |", StringComparison.OrdinalIgnoreCase))
        {
            segments = HighlightLiteral(body, "Health", EditorChatFormatter.Yellow, EditorChatFormatter.White);
            return true;
        }

        if (trimmed.StartsWith("Organization:", StringComparison.OrdinalIgnoreCase))
        {
            segments = HighlightLiteral(body, "Organization:", EditorChatFormatter.Yellow, EditorChatFormatter.White);
            return true;
        }

        Match business = BusinessLabel.Match(trimmed);
        if (business.Success)
        {
            int offset = body.Length - trimmed.Length;
            segments = HighlightRanges(body, EditorChatFormatter.White,
                new[] { (offset + business.Index, business.Length, EditorChatFormatter.Yellow) });
            return true;
        }

        if (trimmed.StartsWith("Bank Account Routing:", StringComparison.OrdinalIgnoreCase))
        {
            segments = HighlightLiteral(body, "Bank Account Routing:", EditorChatFormatter.Yellow, EditorChatFormatter.White);
            return true;
        }

        if (trimmed.StartsWith("Current job:", StringComparison.OrdinalIgnoreCase))
        {
            segments = HighlightLiteral(body, "Current job:", EditorChatFormatter.Yellow, EditorChatFormatter.White);
            return true;
        }

        if (trimmed.StartsWith("Time |", StringComparison.OrdinalIgnoreCase))
        {
            var highlights = new List<(int Index, int Length, Color Color)>();
            AddLiteral(highlights, body, "Time", EditorChatFormatter.Yellow);
            AddMatches(highlights, CurrentlyWorkedValue, body, EditorChatFormatter.Blue);
            segments = HighlightRanges(body, EditorChatFormatter.White, highlights);
            return true;
        }

        if (trimmed.StartsWith("Properties:", StringComparison.OrdinalIgnoreCase))
        {
            var highlights = new List<(int Index, int Length, Color Color)>();
            AddLiteral(highlights, body, "Properties", EditorChatFormatter.Yellow);
            AddLiteral(highlights, body, "Owned:", EditorChatFormatter.Blue);
            AddLiteral(highlights, body, "Partner:", EditorChatFormatter.Blue);
            AddLiteral(highlights, body, "Rented:", EditorChatFormatter.Blue);
            segments = HighlightRanges(body, EditorChatFormatter.White, highlights);
            return true;
        }

        if (trimmed.StartsWith("Custom Number:", StringComparison.OrdinalIgnoreCase) &&
            trimmed.Contains("Main Phone Number:", StringComparison.OrdinalIgnoreCase))
        {
            var highlights = new List<(int Index, int Length, Color Color)>();
            AddLiteral(highlights, body, "Custom Number:", EditorChatFormatter.Yellow);
            AddLiteral(highlights, body, "Main Phone Number:", EditorChatFormatter.Yellow);
            segments = HighlightRanges(body, EditorChatFormatter.White, highlights);
            return true;
        }

        if (trimmed.StartsWith("Monthly remaining weapons:", StringComparison.OrdinalIgnoreCase))
        {
            segments = ValuesAfterColon(body, NumberAfterColon, EditorChatFormatter.Yellow, EditorChatFormatter.White);
            return true;
        }

        if (trimmed.StartsWith("Premium:", StringComparison.OrdinalIgnoreCase) &&
            trimmed.Contains("Furniture slots:", StringComparison.OrdinalIgnoreCase) &&
            trimmed.Contains("Wardrobe slots:", StringComparison.OrdinalIgnoreCase))
        {
            segments = ValuesAfterColon(body, ValueAfterColon, EditorChatFormatter.Yellow, EditorChatFormatter.White);
            return true;
        }

        if (trimmed.StartsWith("World Points:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Panda Points:", StringComparison.OrdinalIgnoreCase))
        {
            segments = ValuesAfterColon(body, NumberAfterColon, EditorChatFormatter.Yellow, EditorChatFormatter.White);
            return true;
        }

        if (trimmed.StartsWith("Current Time:", StringComparison.OrdinalIgnoreCase))
        {
            var highlights = new List<(int Index, int Length, Color Color)>();
            AddMatches(highlights, CurrentDateValue, body, EditorChatFormatter.White);
            AddMatches(highlights, ClockValue, body, EditorChatFormatter.White);
            segments = HighlightRanges(body, EditorChatFormatter.Yellow, highlights);
            return true;
        }

        if (trimmed.StartsWith("Time spent online the past 30 days", StringComparison.OrdinalIgnoreCase))
        {
            Match value = FinalHoursValue.Match(body);
            segments = value.Success
                ? HighlightRanges(body, EditorChatFormatter.Yellow, new[] { (value.Index, value.Length, EditorChatFormatter.White) })
                : new[] { new EditorChatSegment(body, EditorChatFormatter.Yellow) };
            return true;
        }

        if (trimmed.StartsWith("Time remaining on vacation:", StringComparison.OrdinalIgnoreCase))
        {
            Match value = FinalVacationValue.Match(body);
            segments = value.Success
                ? HighlightRanges(body, EditorChatFormatter.Yellow, new[] { (value.Index, value.Length, EditorChatFormatter.White) })
                : new[] { new EditorChatSegment(body, EditorChatFormatter.Yellow) };
            return true;
        }

        return false;
    }

    private static IReadOnlyList<EditorChatSegment> ValuesAfterColon(
        string body,
        Regex regex,
        Color baseColor,
        Color valueColor)
    {
        var highlights = new List<(int Index, int Length, Color Color)>();
        foreach (Match match in regex.Matches(body))
        {
            Group value = match.Groups["value"];
            string raw = value.Value;
            int leading = raw.Length - raw.TrimStart().Length;
            int trailing = raw.Length - raw.TrimEnd().Length;
            int length = raw.Length - leading - trailing;
            if (length > 0)
                highlights.Add((value.Index + leading, length, valueColor));
        }
        return HighlightRanges(body, baseColor, highlights);
    }

    private static IReadOnlyList<EditorChatSegment> HighlightLiteral(string body, string text, Color highlight, Color baseColor)
    {
        int index = body.IndexOf(text, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? new[] { new EditorChatSegment(body, baseColor) }
            : HighlightRanges(body, baseColor, new[] { (index, text.Length, highlight) });
    }

    private static void AddLiteral(List<(int Index, int Length, Color Color)> highlights, string body, string text, Color color)
    {
        int index = body.IndexOf(text, StringComparison.OrdinalIgnoreCase);
        if (index >= 0) highlights.Add((index, text.Length, color));
    }

    private static void AddMatches(List<(int Index, int Length, Color Color)> highlights, Regex regex, string body, Color color)
    {
        foreach (Match match in regex.Matches(body))
            highlights.Add((match.Index, match.Length, color));
    }

    private static IReadOnlyList<EditorChatSegment> HighlightRanges(
        string text,
        Color baseColor,
        IEnumerable<(int Index, int Length, Color Color)> highlights)
    {
        var result = new List<EditorChatSegment>();
        int cursor = 0;
        foreach ((int index, int length, Color color) in highlights
                     .Where(value => value.Index >= 0 && value.Length > 0 && value.Index < text.Length)
                     .OrderBy(value => value.Index))
        {
            if (index < cursor) continue;
            if (index > cursor)
                result.Add(new EditorChatSegment(text[cursor..index], baseColor));

            int safeLength = Math.Min(length, text.Length - index);
            result.Add(new EditorChatSegment(text.Substring(index, safeLength), color));
            cursor = index + safeLength;
        }

        if (cursor < text.Length)
            result.Add(new EditorChatSegment(text[cursor..], baseColor));
        return result.Count == 0 ? new[] { new EditorChatSegment(text, baseColor) } : result;
    }
}
