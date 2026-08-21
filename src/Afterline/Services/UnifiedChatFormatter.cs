using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Afterline.Services;

internal static class UnifiedChatFormatter
{
    private static readonly Regex TimestampPrefix = new(
        @"^\s*(?<timestamp>\[\d{1,2}:\d{2}:\d{2}\])\s*(?<body>.*)$",
        RegexOptions.Compiled);

    internal static IReadOnlyList<EditorChatLine> FormatLines(
        string input,
        bool showTimestamps,
        IReadOnlyDictionary<int, Color>? lineOverrides = null)
    {
        IReadOnlyList<EditorChatLine> baseLines = EditorChatFormatter.FormatLines(input, showTimestamps, lineOverrides);
        if (baseLines.Count == 0) return baseLines;

        string normalized = (input ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        string[] sourceLines = normalized.Split('\n');
        var result = new List<EditorChatLine>(baseLines.Count);

        foreach (EditorChatLine line in baseLines)
        {
            if (lineOverrides is not null && lineOverrides.ContainsKey(line.SourceIndex))
            {
                result.Add(line);
                continue;
            }

            IReadOnlyList<EditorChatSegment> recognizedSegments;
            if (ChatColorRefinementsV045.TryFormat(line.PlainText, out IReadOnlyList<EditorChatSegment> refinements))
            {
                recognizedSegments = refinements;
            }
            else if (CharacterStatsChatFormatter.TryFormat(line.PlainText, out IReadOnlyList<EditorChatSegment> statsSegments))
            {
                recognizedSegments = statsSegments;
            }
            else
            {
                result.Add(line);
                continue;
            }

            IReadOnlyList<EditorChatSegment> finalSegments = recognizedSegments;
            if (showTimestamps && line.SourceIndex >= 0 && line.SourceIndex < sourceLines.Length)
            {
                Match timestamp = TimestampPrefix.Match(sourceLines[line.SourceIndex]);
                if (timestamp.Success)
                {
                    var withTimestamp = new List<EditorChatSegment>(recognizedSegments.Count + 1)
                    {
                        new(timestamp.Groups["timestamp"].Value + " ", EditorChatFormatter.MutedTimestamp)
                    };
                    withTimestamp.AddRange(recognizedSegments);
                    finalSegments = withTimestamp;
                }
            }

            string style = finalSegments.Select(segment => segment.Color).Distinct().Skip(1).Any()
                ? "Mixed"
                : "Automatic";
            result.Add(new EditorChatLine(line.SourceIndex, line.PlainText, style, finalSegments));
        }

        return result;
    }
}
