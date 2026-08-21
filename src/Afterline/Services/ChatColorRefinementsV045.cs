using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Afterline.Services;

internal static class ChatColorRefinementsV045
{
    private static readonly Regex PastDaysLine = new(
        @"^Time spent online the past \d+ days\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FinalHoursValue = new(
        @"\b\d+(?:\.\d+)?\s+hours?\.?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PremiumLine = new(
        @"^Premium:\s*(?<premium>[^|]+?)\s*\|\s*Furniture slots:\s*(?<furniture>[^|]+?)\s*\|\s*Wardrobe slots:\s*(?<wardrobe>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static bool TryFormat(string body, out IReadOnlyList<EditorChatSegment> segments)
    {
        segments = Array.Empty<EditorChatSegment>();
        string trimmed = body.TrimStart();
        if (trimmed.Length == 0) return false;

        if (PastDaysLine.IsMatch(trimmed))
        {
            Match value = FinalHoursValue.Match(body);
            segments = value.Success
                ? HighlightRanges(body, EditorChatFormatter.Yellow,
                    new[] { (value.Index, value.Length, EditorChatFormatter.White) })
                : Single(body, EditorChatFormatter.Yellow);
            return true;
        }

        Match premium = PremiumLine.Match(trimmed);
        if (premium.Success)
        {
            int offset = body.Length - trimmed.Length;
            var highlights = new List<(int Index, int Length, Color Color)>();
            AddTrimmedGroup(highlights, premium.Groups["premium"], offset,
                string.Equals(premium.Groups["premium"].Value.Trim(), "None", StringComparison.OrdinalIgnoreCase)
                    ? EditorChatFormatter.Red
                    : EditorChatFormatter.White);
            AddTrimmedGroup(highlights, premium.Groups["furniture"], offset, EditorChatFormatter.White);
            AddTrimmedGroup(highlights, premium.Groups["wardrobe"], offset, EditorChatFormatter.White);
            segments = HighlightRanges(body, EditorChatFormatter.Yellow, highlights);
            return true;
        }

        if (trimmed.StartsWith("Online faction members:", StringComparison.OrdinalIgnoreCase))
        {
            segments = Single(body, EditorChatFormatter.Blue);
            return true;
        }

        return false;
    }

    private static void AddTrimmedGroup(
        List<(int Index, int Length, Color Color)> highlights,
        Group group,
        int offset,
        Color color)
    {
        string raw = group.Value;
        int leading = raw.Length - raw.TrimStart().Length;
        int trailing = raw.Length - raw.TrimEnd().Length;
        int length = raw.Length - leading - trailing;
        if (length > 0)
            highlights.Add((offset + group.Index + leading, length, color));
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
        return result.Count == 0 ? Single(text, baseColor) : result;
    }

    private static IReadOnlyList<EditorChatSegment> Single(string text, Color color)
        => new[] { new EditorChatSegment(text, color) };
}
