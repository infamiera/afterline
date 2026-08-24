using System.Text.RegularExpressions;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline.Services;

internal static class ChatColorReliabilityService
{
    // FiveM can expose a newly inserted row before all nested CSS spans have their
    // final colors. Repair only expected accent ranges that are still neutral;
    // chromatic colors captured from the game always remain authoritative.
    private static readonly Regex TimestampPrefix = new(
        @"^\s*\[\d{1,2}:\d{2}:\d{2}\]\s*",
        RegexOptions.Compiled);

    internal static IReadOnlyList<ChatColorRun> EnsureExpectedAccents(
        string text,
        IEnumerable<ChatColorRun>? colorRuns)
    {
        IReadOnlyList<ChatColorRun> normalized = ChatColorData.NormalizeRuns(text, colorRuns);
        if (string.IsNullOrEmpty(text) ||
            normalized.Count == 0 ||
            !ChatColorData.HasCompleteCoverage(text, normalized))
        {
            return normalized;
        }

        Match timestamp = TimestampPrefix.Match(text);
        int bodyStart = timestamp.Success ? timestamp.Length : 0;
        string body = text[bodyStart..];
        IReadOnlyList<EditorChatSegment> expectedSegments = ResolveExpectedSegments(body);
        if (expectedSegments.Count == 0 ||
            !string.Equals(
                string.Concat(expectedSegments.Select(segment => segment.Text)),
                body,
                StringComparison.Ordinal))
        {
            return normalized;
        }

        IReadOnlyList<ChatColorRun> reliable = normalized;
        int segmentStart = bodyStart;
        foreach (EditorChatSegment segment in expectedSegments)
        {
            int length = segment.Text.Length;
            if (length > 0 &&
                segment.Color != EditorChatFormatter.White &&
                !HasReliableAccent(reliable, segmentStart, length))
            {
                reliable = OverrideColor(
                    text,
                    reliable,
                    segmentStart,
                    length,
                    segment.Color);
            }

            segmentStart += length;
        }

        return reliable;
    }

    private static IReadOnlyList<EditorChatSegment> ResolveExpectedSegments(string body)
    {
        if (ChatColorRefinementsV045.TryFormat(
                body,
                out IReadOnlyList<EditorChatSegment> refinements))
        {
            return refinements;
        }

        if (CharacterStatsChatFormatter.TryFormat(
                body,
                out IReadOnlyList<EditorChatSegment> statsSegments))
        {
            return statsSegments;
        }

        return EditorChatFormatter
            .FormatLines(body, showTimestamps: false)
            .FirstOrDefault()?.Segments ?? Array.Empty<EditorChatSegment>();
    }

    private static bool HasReliableAccent(
        IReadOnlyList<ChatColorRun> runs,
        int start,
        int length)
    {
        int end = start + length;
        int cursor = start;
        foreach (ChatColorRun run in runs)
        {
            if (run.End <= start) continue;
            if (run.Start >= end) break;
            if (run.Start > cursor || !IsChromatic(run)) return false;
            cursor = Math.Max(cursor, Math.Min(end, run.End));
        }

        return cursor >= end;
    }

    private static bool IsChromatic(ChatColorRun run)
    {
        if (run.Alpha < 128) return false;
        int maximum = Math.Max(run.Red, Math.Max(run.Green, run.Blue));
        int minimum = Math.Min(run.Red, Math.Min(run.Green, run.Blue));
        return maximum >= 100 && maximum - minimum >= 30;
    }

    private static IReadOnlyList<ChatColorRun> OverrideColor(
        string text,
        IReadOnlyList<ChatColorRun> runs,
        int start,
        int length,
        Color color)
    {
        int end = Math.Min(text.Length, start + length);
        if (start < 0 || start >= end) return runs;

        var updated = new List<ChatColorRun>(runs.Count + 2);
        foreach (ChatColorRun run in runs)
        {
            if (run.End <= start || run.Start >= end)
            {
                updated.Add(run);
                continue;
            }

            if (run.Start < start)
                updated.Add(run with { Length = start - run.Start });
            if (run.End > end)
                updated.Add(run with { Start = end, Length = run.End - end });
        }

        updated.Add(new ChatColorRun(
            start,
            end - start,
            color.R,
            color.G,
            color.B,
            color.A));
        return ChatColorData.NormalizeRuns(text, updated);
    }
}
