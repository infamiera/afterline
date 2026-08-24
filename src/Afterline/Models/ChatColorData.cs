using System.Text.Json.Serialization;

namespace Afterline.Models;

public sealed record ChatColorRun(
    int Start,
    int Length,
    byte Red,
    byte Green,
    byte Blue,
    byte Alpha = 255,
    bool Italic = false)
{
    [JsonIgnore]
    public int End => Start + Length;
}

public sealed class CapturedChatLine
{
    public string Text { get; set; } = string.Empty;
    public List<ChatColorRun> ColorRuns { get; set; } = new();

    public CapturedChatLine()
    {
    }

    public CapturedChatLine(string text, IEnumerable<ChatColorRun>? colorRuns = null)
    {
        Text = text ?? string.Empty;
        ColorRuns = ChatColorData.NormalizeRuns(Text, colorRuns).ToList();
    }
}

public sealed class ChatColorLineRecord
{
    public string Text { get; set; } = string.Empty;
    public List<ChatColorRun> ColorRuns { get; set; } = new();
}

public static class ChatColorData
{
    public static IReadOnlyList<ChatColorRun> NormalizeRuns(
        string text,
        IEnumerable<ChatColorRun>? colorRuns)
    {
        if (string.IsNullOrEmpty(text) || colorRuns is null)
            return Array.Empty<ChatColorRun>();

        var normalized = new List<ChatColorRun>();
        int cursor = 0;

        foreach (ChatColorRun run in colorRuns
                     .Where(run => run.Length > 0 && run.Start < text.Length)
                     .OrderBy(run => run.Start))
        {
            int start = Math.Max(0, run.Start);
            if (start < cursor)
                start = cursor;

            long rawEnd = (long)run.Start + run.Length;
            int end = (int)Math.Min(text.Length, Math.Max(start, rawEnd));
            if (end <= start)
                continue;

            var safe = run with { Start = start, Length = end - start };
            if (normalized.Count > 0)
            {
                ChatColorRun previous = normalized[^1];
                if (previous.End == safe.Start && SameColor(previous, safe))
                {
                    normalized[^1] = previous with { Length = previous.Length + safe.Length };
                    cursor = safe.End;
                    continue;
                }
            }

            normalized.Add(safe);
            cursor = safe.End;
        }

        return normalized;
    }

    public static IReadOnlyList<ChatColorRun> SliceRuns(
        string sourceText,
        IEnumerable<ChatColorRun>? colorRuns,
        int sourceStart,
        int length,
        int targetStart = 0)
    {
        if (length <= 0 || sourceStart >= sourceText.Length)
            return Array.Empty<ChatColorRun>();

        int safeStart = Math.Max(0, sourceStart);
        int safeEnd = (int)Math.Min(sourceText.Length, (long)safeStart + length);
        var sliced = new List<ChatColorRun>();

        foreach (ChatColorRun run in NormalizeRuns(sourceText, colorRuns))
        {
            int overlapStart = Math.Max(run.Start, safeStart);
            int overlapEnd = Math.Min(run.End, safeEnd);
            if (overlapEnd <= overlapStart)
                continue;

            sliced.Add(run with
            {
                Start = targetStart + overlapStart - safeStart,
                Length = overlapEnd - overlapStart
            });
        }

        return sliced;
    }

    public static bool HasCompleteCoverage(
        string text,
        IEnumerable<ChatColorRun>? colorRuns)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        IReadOnlyList<ChatColorRun> runs = NormalizeRuns(text, colorRuns);
        int cursor = 0;
        foreach (ChatColorRun run in runs)
        {
            if (run.Start != cursor)
                return false;
            cursor = run.End;
        }

        return cursor == text.Length;
    }

    private static bool SameColor(ChatColorRun left, ChatColorRun right)
        => left.Red == right.Red &&
           left.Green == right.Green &&
           left.Blue == right.Blue &&
           left.Alpha == right.Alpha &&
           left.Italic == right.Italic;
}
