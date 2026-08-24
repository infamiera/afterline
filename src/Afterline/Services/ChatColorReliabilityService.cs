using System.Text.RegularExpressions;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline.Services;

internal static class ChatColorReliabilityService
{
    // FiveM can expose a newly inserted row before all nested CSS spans have their
    // final colors. Most genuine chromatic spans remain authoritative, while a
    // small set of structurally unambiguous lines receives stricter validation to
    // prevent a previous row's color from leaking into the new message.
    private static readonly Regex TimestampPrefix = new(
        @"^\s*(?<timestamp>\[\d{1,2}:\d{2}:\d{2}\])\s*",
        RegexOptions.Compiled);

    private static readonly Regex NeutralLowSpeech = new(
        @"^\s*.+?\s+says\s+\[(?:low|lower)\]:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GlobalOoc = new(
        @"^\s*\(\(\s*Global\s+OOC:\s*\(\d+\)\s*(?<name>[^:]+?)(?<colon>:\s*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        Color? timestampColor = ResolveTimestampReferenceColor(normalized, timestamp);

        if (IsNeutralLowSpeech(body))
        {
            Color baseColor = timestampColor is Color captured && IsNeutralColor(captured)
                ? captured
                : EditorChatFormatter.White;
            return NormalizeLowSpeechColors(
                text,
                normalized,
                bodyStart,
                body.Length,
                baseColor);
        }

        Match globalOoc = GlobalOoc.Match(body);
        if (globalOoc.Success)
        {
            int nameStart = bodyStart + globalOoc.Groups["name"].Index;
            int nameLength = globalOoc.Groups["name"].Length;
            Color? roleColor = ResolveGlobalOocRoleColor(normalized, nameStart, nameLength);

            IReadOnlyList<ChatColorRun> corrected = OverrideColor(
                text,
                normalized,
                bodyStart,
                body.Length,
                EditorChatFormatter.White);
            if (roleColor is Color color)
                corrected = OverrideColor(text, corrected, nameStart, nameLength, color);
            return corrected;
        }

        IReadOnlyList<EditorChatSegment> expectedSegments = ResolveExpectedSegments(body);
        if (expectedSegments.Count == 0 ||
            !string.Equals(
                string.Concat(expectedSegments.Select(segment => segment.Text)),
                body,
                StringComparison.Ordinal))
        {
            return normalized;
        }

        Color[] expectedColors = expectedSegments
            .Where(segment => segment.Text.Length > 0)
            .Select(segment => segment.Color)
            .Distinct()
            .ToArray();
        if (expectedColors.Length == 1 &&
            expectedColors[0] != EditorChatFormatter.White &&
            !body.TrimStart().StartsWith("*", StringComparison.Ordinal))
        {
            // Whole-line formats such as (phone), microphone, whisper, radio,
            // success and inventory rows must stay one color. A temporarily
            // neutral FiveM body must not override the recognized line color
            // merely because its timestamp already received the correct style.
            Color lineColor = timestampColor is Color captured &&
                              IsCompatibleTint(captured, expectedColors[0])
                ? captured
                : expectedColors[0];
            return OverrideColor(
                text,
                normalized,
                bodyStart,
                body.Length,
                lineColor);
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
                // Leading-star action lines can legally mix purple action spans with
                // white spoken text. Preserve that exact computed-style snapshot;
                // replacing the expected full-purple fallback would destroy the
                // speech/action boundary seen in FiveM.
                bool preserveMixedAction = segmentStart == bodyStart &&
                    length == body.Length &&
                    body.TrimStart().StartsWith("*", StringComparison.Ordinal) &&
                    HasActionPurpleAndNeutralCoverage(reliable, segmentStart, length);
                if (preserveMixedAction)
                {
                    segmentStart += length;
                    continue;
                }

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

    internal static bool IsNeutralLowSpeech(string body)
        => !string.IsNullOrWhiteSpace(body) && NeutralLowSpeech.IsMatch(body);

    internal static bool IsGlobalOoc(string body)
        => !string.IsNullOrWhiteSpace(body) && GlobalOoc.IsMatch(body);

    private static Color? ResolveGlobalOocRoleColor(
        IReadOnlyList<ChatColorRun> runs,
        int nameStart,
        int nameLength)
    {
        int nameEnd = nameStart + nameLength;
        (Color Color, int Coverage)? best = null;
        foreach (ChatColorRun run in runs)
        {
            int overlap = Math.Min(nameEnd, run.End) - Math.Max(nameStart, run.Start);
            if (overlap <= 0 || !TryClassifyGlobalOocRole(run, out Color roleColor))
                continue;

            if (best is null || overlap > best.Value.Coverage)
                best = (roleColor, overlap);
        }

        return best?.Color;
    }

    private static bool TryClassifyGlobalOocRole(ChatColorRun run, out Color color)
    {
        color = default;
        if (run.Alpha < 128) return false;

        // Management orange is checked first because it is red-dominant too.
        if (run.Red >= 160 && run.Green >= 90 && run.Green <= 220 &&
            run.Red - run.Green >= 20 && run.Blue < 140)
        {
            color = EditorChatFormatter.Orange;
            return true;
        }

        if (run.Red >= 150 && run.Red >= run.Green + 55 && run.Red >= run.Blue + 55)
        {
            color = EditorChatFormatter.Red;
            return true;
        }

        if (run.Blue >= 140 && run.Blue >= run.Red + 45 && run.Blue >= run.Green + 30)
        {
            color = EditorChatFormatter.Blue;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<ChatColorRun> NormalizeLowSpeechColors(
        string text,
        IReadOnlyList<ChatColorRun> runs,
        int start,
        int length,
        Color baseColor)
    {
        int end = start + length;
        IReadOnlyList<ChatColorRun> corrected = runs;
        foreach (ChatColorRun run in runs)
        {
            int overlapStart = Math.Max(start, run.Start);
            int overlapEnd = Math.Min(end, run.End);
            if (overlapEnd <= overlapStart || IsActionPurple(run)) continue;
            corrected = OverrideColor(
                text,
                corrected,
                overlapStart,
                overlapEnd - overlapStart,
                baseColor);
        }
        return corrected;
    }

    private static Color? ResolveTimestampReferenceColor(
        IReadOnlyList<ChatColorRun> runs,
        Match timestamp)
    {
        if (!timestamp.Success || !timestamp.Groups["timestamp"].Success)
            return null;

        int index = timestamp.Groups["timestamp"].Index;
        ChatColorRun? run = runs.FirstOrDefault(value =>
            value.Start <= index && value.End > index && value.Alpha > 0);
        return run is null
            ? null
            : Color.FromArgb(run.Alpha, run.Red, run.Green, run.Blue);
    }

    private static bool IsNeutralColor(Color color)
    {
        int maximum = Math.Max(color.R, Math.Max(color.G, color.B));
        int minimum = Math.Min(color.R, Math.Min(color.G, color.B));
        return maximum - minimum <= 28;
    }

    private static bool IsCompatibleTint(Color captured, Color expected)
    {
        double denominator = expected.R * expected.R +
                             expected.G * expected.G +
                             expected.B * expected.B;
        if (denominator <= 0) return false;

        double scale = (captured.R * expected.R +
                        captured.G * expected.G +
                        captured.B * expected.B) / denominator;
        scale = Math.Clamp(scale, 0.15, 1.5);
        double red = captured.R - expected.R * scale;
        double green = captured.G - expected.G * scale;
        double blue = captured.B - expected.B * scale;
        return Math.Sqrt(red * red + green * green + blue * blue) <= 52;
    }

    private static bool IsActionPurple(ChatColorRun run)
        => run.Alpha >= 128 &&
           run.Red >= 135 &&
           run.Blue >= 145 &&
           run.Red - run.Green >= 15 &&
           run.Blue - run.Green >= 20;

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

    private static bool HasActionPurpleAndNeutralCoverage(
        IReadOnlyList<ChatColorRun> runs,
        int start,
        int length)
    {
        int end = start + length;
        bool chromatic = false;
        bool neutral = false;
        foreach (ChatColorRun run in runs)
        {
            if (run.End <= start) continue;
            if (run.Start >= end) break;
            if (run.Alpha < 128) continue;
            if (IsActionPurple(run)) chromatic = true;
            else neutral = true;
            if (chromatic && neutral) return true;
        }
        return false;
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

        var updated = new List<ChatColorRun>(runs.Count + 4);
        foreach (ChatColorRun run in runs)
        {
            if (run.End <= start || run.Start >= end)
            {
                updated.Add(run);
                continue;
            }

            if (run.Start < start)
                updated.Add(run with { Length = start - run.Start });

            int overlapStart = Math.Max(start, run.Start);
            int overlapEnd = Math.Min(end, run.End);
            if (overlapEnd > overlapStart)
            {
                updated.Add(new ChatColorRun(
                    overlapStart,
                    overlapEnd - overlapStart,
                    color.R,
                    color.G,
                    color.B,
                    color.A,
                    run.Italic));
            }

            if (run.End > end)
                updated.Add(run with { Start = end, Length = run.End - end });
        }
        return ChatColorData.NormalizeRuns(text, updated);
    }
}
