using System.Text.RegularExpressions;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline.Services;

internal static class UnifiedChatFormatter
{
    private static readonly Regex TimestampPrefix = new(
        @"^\s*(?<timestamp>\[\d{1,2}:\d{2}:\d{2}\])\s*(?<body>.*)$",
        RegexOptions.Compiled);

    internal static IReadOnlyList<EditorChatLine> FormatLines(
        string input,
        bool showTimestamps,
        IReadOnlyDictionary<int, Color>? lineOverrides = null,
        IReadOnlyDictionary<int, ChatColorLineRecord>? exactColors = null)
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

            if (exactColors is not null &&
                exactColors.TryGetValue(line.SourceIndex, out ChatColorLineRecord? exact) &&
                line.SourceIndex >= 0 &&
                line.SourceIndex < sourceLines.Length &&
                TryFormatExactColors(
                    sourceLines[line.SourceIndex],
                    showTimestamps,
                    exact,
                    out IReadOnlyList<EditorChatSegment> exactSegments))
            {
                result.Add(new EditorChatLine(
                    line.SourceIndex,
                    line.PlainText,
                    "Captured from FiveM",
                    exactSegments));
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
            result.Add(new EditorChatLine(
                line.SourceIndex,
                line.PlainText,
                style,
                ChatTypographyService.ApplySlashItalics(finalSegments)));
        }

        return result;
    }

    private static bool TryFormatExactColors(
        string rawLine,
        bool showTimestamps,
        ChatColorLineRecord exact,
        out IReadOnlyList<EditorChatSegment> segments)
    {
        segments = Array.Empty<EditorChatSegment>();
        string source = rawLine.TrimEnd();
        if (!string.Equals(source, exact.Text, StringComparison.Ordinal))
            return false;

        int sourceStart = 0;
        string visibleText = source;
        if (!showTimestamps)
        {
            Match timestamp = TimestampPrefix.Match(source);
            if (timestamp.Success)
            {
                sourceStart = timestamp.Groups["body"].Index;
                visibleText = timestamp.Groups["body"].Value;
            }
        }

        IReadOnlyList<ChatColorRun> reliableRuns = ChatColorReliabilityService.EnsureExpectedAccents(
            source,
            exact.ColorRuns);
        IReadOnlyList<ChatColorRun> runs = ChatColorData.SliceRuns(
            source,
            reliableRuns,
            sourceStart,
            visibleText.Length);
        if (runs.Count == 0 || !ChatColorData.HasCompleteCoverage(visibleText, runs))
            return false;

        var formatted = new List<EditorChatSegment>();
        int cursor = 0;
        foreach (ChatColorRun run in runs)
        {
            if (run.Start > cursor)
                formatted.Add(new EditorChatSegment(visibleText[cursor..run.Start], EditorChatFormatter.White));

            formatted.Add(new EditorChatSegment(
                visibleText.Substring(run.Start, run.Length),
                Color.FromArgb(run.Alpha, run.Red, run.Green, run.Blue),
                run.Italic));
            cursor = run.End;
        }

        if (cursor < visibleText.Length)
            formatted.Add(new EditorChatSegment(visibleText[cursor..], EditorChatFormatter.White));

        segments = ChatTypographyService.ApplySlashItalics(formatted);
        return formatted.Count > 0;
    }
}
