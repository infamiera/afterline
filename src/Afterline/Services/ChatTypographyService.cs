using System.Text.RegularExpressions;

namespace Afterline.Services;

internal static class ChatTypographyService
{
    // Slash emphasis is deliberately stricter than a generic /.../ match so
    // command-heavy lines such as "/detach ... or /detach ..." cannot be
    // mistaken for roleplay italics.
    private static readonly Regex SlashItalic = new(
        @"(?<!\S)/(?<text>\S(?:[^/\r\n]*?\S)?)/(?=$|[\s.,!?;:)\]])",
        RegexOptions.Compiled);

    internal static IReadOnlyList<EditorChatSegment> ApplySlashItalics(
        IEnumerable<EditorChatSegment> source)
    {
        EditorChatSegment[] segments = source.Where(segment => segment.Text.Length > 0).ToArray();
        if (segments.Length == 0) return segments;

        string text = string.Concat(segments.Select(segment => segment.Text));
        (int Start, int End)[] ranges = SlashItalic.Matches(text)
            .Cast<Match>()
            .Select(match => (match.Index, match.Index + match.Length))
            .ToArray();
        if (ranges.Length == 0) return segments;

        var styled = new List<EditorChatSegment>(segments.Length + ranges.Length * 2);
        int absoluteStart = 0;
        foreach (EditorChatSegment segment in segments)
        {
            int absoluteEnd = absoluteStart + segment.Text.Length;
            var boundaries = new SortedSet<int> { absoluteStart, absoluteEnd };
            foreach ((int start, int end) in ranges)
            {
                if (end <= absoluteStart || start >= absoluteEnd) continue;
                boundaries.Add(Math.Max(absoluteStart, start));
                boundaries.Add(Math.Min(absoluteEnd, end));
            }

            int[] points = boundaries.ToArray();
            for (int index = 0; index < points.Length - 1; index++)
            {
                int partStart = points[index];
                int partEnd = points[index + 1];
                bool italic = segment.IsItalic || ranges.Any(range =>
                    range.Start <= partStart && range.End >= partEnd);
                styled.Add(new EditorChatSegment(
                    segment.Text.Substring(partStart - absoluteStart, partEnd - partStart),
                    segment.Color,
                    italic));
            }

            absoluteStart = absoluteEnd;
        }

        return styled;
    }
}
