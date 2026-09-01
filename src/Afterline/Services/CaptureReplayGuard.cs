using System.Globalization;
using System.Text.RegularExpressions;

namespace Afterline.Services;

internal sealed record CaptureReplayDecision(
    int CandidateStartIndex,
    int CandidateCount,
    int HistoricalStartIndex,
    string Evidence)
{
    public static CaptureReplayDecision None { get; } = new(-1, 0, -1, string.Empty);
    public bool IsReplay => CandidateStartIndex >= 0 && CandidateCount > 0;
}

internal sealed class CaptureReplayGuard
{
    internal const int MinimumReplayLines = 20;
    private const int MinimumDistinctBodies = 10;
    private const int HistoryLimit = 2500;
    private static readonly TimeSpan MaximumRestampedSpan = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MinimumHistoricalSpan = TimeSpan.FromSeconds(15);
    private static readonly Regex TimestampPrefix = new(
        @"^\[(?<time>\d{1,2}:\d{2}:\d{2})\]\s*",
        RegexOptions.Compiled);

    private readonly List<string> _committedHistory = new();

    public void Reset(IEnumerable<string>? committedLines = null)
    {
        _committedHistory.Clear();
        if (committedLines is not null)
            RecordCommitted(committedLines);
    }

    public void RecordCommitted(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                _committedHistory.Add(line);
        }

        int excess = _committedHistory.Count - HistoryLimit;
        if (excess > 0)
            _committedHistory.RemoveRange(0, excess);
    }

    public CaptureReplayDecision Evaluate(IReadOnlyList<string> incoming)
        => EvaluateAgainst(_committedHistory, incoming);

    public static CaptureReplayDecision EvaluateAgainst(
        IReadOnlyList<string> history,
        IReadOnlyList<string> incoming)
    {
        if (history.Count < MinimumReplayLines || incoming.Count < MinimumReplayLines)
            return CaptureReplayDecision.None;

        string[] historyBodies = history.Select(NormalizeBody).ToArray();
        string[] incomingBodies = incoming.Select(NormalizeBody).ToArray();
        int bestIncomingStart = -1;
        int bestLength = 0;
        int bestStart = -1;

        for (int incomingStart = 0;
             incomingStart <= incoming.Count - MinimumReplayLines;
             incomingStart++)
        {
            for (int start = 0; start <= history.Count - MinimumReplayLines; start++)
            {
                if (!string.Equals(
                        historyBodies[start],
                        incomingBodies[incomingStart],
                        StringComparison.Ordinal))
                    continue;

                int length = 0;
                bool exact = true;
                int maximum = Math.Min(
                    history.Count - start,
                    incoming.Count - incomingStart);
                while (length < maximum && string.Equals(
                           historyBodies[start + length],
                           incomingBodies[incomingStart + length],
                           StringComparison.Ordinal))
                {
                    exact &= string.Equals(
                        history[start + length],
                        incoming[incomingStart + length],
                        StringComparison.Ordinal);
                    length++;
                }

                if (length < MinimumReplayLines ||
                    CountDistinctBodies(incomingBodies, incomingStart, length) < MinimumDistinctBodies)
                    continue;

                // Identical text and timestamps are not sufficient proof: the game
                // may legitimately emit the same content more than once. Only the
                // observed corruption signature (an old varied timeline collapsed
                // into a new two-second window) is eligible for suppression.
                if (exact || !HasRestampedReplayEvidence(
                        history,
                        start,
                        incoming,
                        incomingStart,
                        length))
                    continue;

                if (length > bestLength)
                {
                    bestIncomingStart = incomingStart;
                    bestLength = length;
                    bestStart = start;
                }
            }
        }

        if (bestLength == 0)
            return CaptureReplayDecision.None;

        return new CaptureReplayDecision(
            bestIncomingStart,
            bestLength,
            bestStart,
            "ordered sequence with collapsed replacement timestamps");
    }

    public static bool LooksLikeRestampedBatch(IReadOnlyList<string> lines)
    {
        if (lines.Count < MinimumReplayLines)
            return false;

        string[] bodies = lines.Select(NormalizeBody).ToArray();
        for (int start = 0; start <= lines.Count - MinimumReplayLines; start++)
        {
            if (CountDistinctBodies(bodies, start, MinimumReplayLines) >= MinimumDistinctBodies &&
                TryGetTimestampSpan(
                    lines,
                    start,
                    MinimumReplayLines,
                    out TimeSpan span) &&
                span <= MaximumRestampedSpan)
                return true;
        }

        return false;
    }

    internal static void RunSmokeTest()
    {
        string[] bodies = Enumerable.Range(1, 24)
            .Select(index => index switch
            {
                1 => "You unlocked the property door.",
                2 => "(( PM from (196) Elijah Sledge: ohhh ))",
                3 => "(( PM to (196) Elijah Sledge: come come ))",
                4 => "(( PM from (196) Elijah Sledge: is it open ))",
                5 => "(( PM to (196) Elijah Sledge: ya ))",
                6 => "You locked the property door.",
                7 => "(( (160) Mina Guillebeaux: hf! ))",
                8 => "(( PM from (196) Elijah Sledge: do we have a white background ))",
                9 => "[INFO] Object placed.",
                10 => "[INFO] [FURNITURE] Rotation copied!",
                11 => "[INFO] [FURNITURE] Rotation pasted!",
                24 => "(( PM from (196) Elijah Sledge: buhbye ))",
                _ => $"Distinct historical chat line {index}."
            })
            .ToArray();
        DateTime historicalStart = DateTime.Today.AddHours(15).AddMinutes(42);
        string[] history = bodies.Select((body, index) =>
            $"[{historicalStart.AddSeconds(index * 25):HH:mm:ss}] {body}").ToArray();
        string[] restamped = bodies.Select((body, index) =>
            $"[16:33:{index / 12:00}] {body}").ToArray();

        CaptureReplayDecision replay = EvaluateAgainst(history, restamped);
        if (!replay.IsReplay || replay.CandidateStartIndex != 0 || replay.CandidateCount != bodies.Length)
            throw new InvalidOperationException("A proven restamped chat-buffer replay was not detected.");

        string[] repeatedSpam = Enumerable.Range(0, 24)
            .Select(index => $"[14:53:{index:00}] (( PM from (196) Player: hi ))")
            .ToArray();
        string[] laterSpam = Enumerable.Range(0, 24)
            .Select(index => $"[14:57:{index:00}] (( PM from (196) Player: hi ))")
            .ToArray();
        if (EvaluateAgainst(repeatedSpam, laterSpam).IsReplay)
            throw new InvalidOperationException("Legitimate repeated PMs were incorrectly treated as a replay.");

        string[] naturallyRepeatedConversation = bodies.Select((body, index) =>
            $"[{historicalStart.AddMinutes(20).AddSeconds(index * 25):HH:mm:ss}] {body}").ToArray();
        if (EvaluateAgainst(history, naturallyRepeatedConversation).IsReplay)
            throw new InvalidOperationException(
                "A legitimately repeated conversation with its own timestamp timeline was incorrectly flagged.");

        string[] oneRepeatedLine =
        {
            "[14:53:02] (( PM from (196) Player: hi ))"
        };
        if (EvaluateAgainst(oneRepeatedLine, oneRepeatedLine).IsReplay)
            throw new InvalidOperationException("A single identical line was incorrectly treated as a replay.");

        string[] exactReplay = history.ToArray();
        if (EvaluateAgainst(history, exactReplay).IsReplay)
            throw new InvalidOperationException(
                "An exact multi-line sequence without timestamp-collapse evidence was incorrectly flagged.");

        string[] partialReplay = new[] { "[16:32:59] This is genuinely new before the replay." }
            .Concat(restamped)
            .Concat(new[] { "[16:33:03] This is genuinely new after the replay." })
            .ToArray();
        CaptureReplayDecision partial = EvaluateAgainst(history, partialReplay);
        if (partial.CandidateStartIndex != 1 ||
            partial.CandidateCount != bodies.Length ||
            partial.CandidateCount == partialReplay.Length)
            throw new InvalidOperationException("An interior replay did not preserve its genuinely new neighbors.");

        string[] largeHistory = Enumerable.Range(0, 10_000)
            .Select(index => $"[{index / 3600 % 24:00}:{index / 60 % 60:00}:{index % 60:00}] Historical line {index}.")
            .ToArray();
        string[] unrelatedIncoming = Enumerable.Range(0, 100)
            .Select(index => $"[18:00:{index % 60:00}] Unrelated incoming line {index}.")
            .ToArray();
        if (EvaluateAgainst(largeHistory, unrelatedIncoming).IsReplay)
            throw new InvalidOperationException(
                "The replay guard produced a false positive against 10,000 legitimate historical lines.");
    }

    private static bool HasRestampedReplayEvidence(
        IReadOnlyList<string> history,
        int historyStart,
        IReadOnlyList<string> incoming,
        int incomingStart,
        int length)
    {
        if (!TryGetTimestampSpan(history, historyStart, length, out TimeSpan historicalSpan) ||
            !TryGetTimestampSpan(incoming, incomingStart, length, out TimeSpan incomingSpan) ||
            historicalSpan < MinimumHistoricalSpan ||
            incomingSpan > MaximumRestampedSpan)
            return false;

        int changedTimestamps = 0;
        for (int index = 0; index < length; index++)
        {
            if (!string.Equals(
                    GetTimestampText(history[historyStart + index]),
                    GetTimestampText(incoming[incomingStart + index]),
                    StringComparison.Ordinal))
                changedTimestamps++;
        }

        return changedTimestamps >= Math.Ceiling(length * 0.9);
    }

    private static int CountDistinctBodies(
        IReadOnlyList<string> bodies,
        int start,
        int length)
        => bodies.Skip(start)
            .Take(length)
            .Distinct(StringComparer.Ordinal)
            .Take(MinimumDistinctBodies)
            .Count();

    private static string NormalizeBody(string line)
        => TimestampPrefix.Replace(line ?? string.Empty, string.Empty).Trim();

    private static string GetTimestampText(string line)
    {
        Match match = TimestampPrefix.Match(line ?? string.Empty);
        return match.Success ? match.Groups["time"].Value : string.Empty;
    }

    private static bool TryGetTimestampSpan(
        IReadOnlyList<string> lines,
        int start,
        int length,
        out TimeSpan span)
    {
        span = TimeSpan.Zero;
        TimeSpan? first = null;
        TimeSpan? previous = null;
        TimeSpan elapsed = TimeSpan.Zero;

        for (int index = 0; index < length; index++)
        {
            Match match = TimestampPrefix.Match(lines[start + index] ?? string.Empty);
            if (!match.Success || !DateTime.TryParseExact(
                    match.Groups["time"].Value,
                    "H:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime timestamp))
                return false;

            TimeSpan current = timestamp.TimeOfDay;
            if (first is null)
            {
                first = current;
                previous = current;
                continue;
            }

            TimeSpan delta = current - previous!.Value;
            if (delta < TimeSpan.FromHours(-12))
                delta += TimeSpan.FromDays(1);
            else if (delta < TimeSpan.Zero)
                return false;

            elapsed += delta;
            previous = current;
        }

        span = elapsed;
        return first is not null;
    }
}
