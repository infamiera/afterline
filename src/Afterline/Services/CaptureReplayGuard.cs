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

internal sealed record ExistingLogReplayMatch(
    int CandidateStartIndex,
    int CandidateCount,
    int HistoricalStartIndex,
    string Evidence);

internal sealed class CaptureReplayGuard
{
    internal const int MinimumReplayLines = 20;
    internal const int HistoryLimit = 100;
    private const int MinimumDistinctBodies = 10;
    private const int ContextRows = 3;
    private const int MaximumHistoricalStartsPerLine = 64;
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

    // FiveM does not guarantee that every NUI row includes its displayed time.
    // For those rows, use Afterline's resolved server time (or UTC fallback) so
    // replay comparison always has one consistent time basis.
    internal static string[] WithFallbackTimestamps(
        IEnumerable<string> lines,
        DateTime observedAt)
        => lines.Select(line => WithFallbackTimestamp(line, observedAt)).ToArray();

    internal static string WithFallbackTimestamp(string line, DateTime observedAt)
    {
        string safeLine = line ?? string.Empty;
        return TimestampPrefix.IsMatch(safeLine)
            ? safeLine
            : $"[{observedAt:HH:mm:ss}] {safeLine}";
    }

    public static CaptureReplayDecision EvaluateAgainst(
        IReadOnlyList<string> history,
        IReadOnlyList<string> incoming)
    {
        if (history.Count < MinimumReplayLines || incoming.Count < MinimumReplayLines)
            return CaptureReplayDecision.None;

        string[] historyBodies = history.Select(NormalizeBody).ToArray();
        string[] incomingBodies = incoming.Select(NormalizeBody).ToArray();
        Dictionary<string, List<int>> historyStarts = BuildHistoryIndex(historyBodies);
        int bestIncomingStart = -1;
        int bestLength = 0;
        int bestStart = -1;
        string bestEvidence = string.Empty;

        for (int incomingStart = 0;
             incomingStart <= incoming.Count - MinimumReplayLines;
             incomingStart++)
        {
            if (!historyStarts.TryGetValue(incomingBodies[incomingStart], out List<int>? starts))
                continue;

            // A collapsed timestamp window is the normal corruption signature.
            // An exact timestamp-identical replay is also decisive, however it
            // has a normal timestamp span and must not be discarded by that
            // early gate.  The exact comparison below makes that path safe:
            // it needs a long, ordered, varied sequence rather than a repeated
            // message or a similar chat scene.
            bool hasCollapsedTimestampWindow = LooksLikeRestampedWindow(
                incoming,
                incomingBodies,
                incomingStart);

            int checkedStarts = 0;
            foreach (int start in starts)
            {
                if (checkedStarts++ >= MaximumHistoricalStartsPerLine)
                    break;
                if (start > history.Count - MinimumReplayLines)
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

                // The corruption signature must include timestamp collapse. An
                // identical timeline remains unsuppressed: the regular visible
                // overlap checkpoint handles it without risking a valid repeat.
                // An exact, long, varied sequence includes identical displayed
                // timestamps and is itself decisive replay evidence. This is a
                // common capture-corruption shape after an alt-tab/reconnect;
                // normal visible-buffer refreshes are already removed by the
                // overlap checkpoint before they reach this guard.
                if (!exact &&
                    (!hasCollapsedTimestampWindow ||
                     !HasRestampedReplayEvidence(
                         history,
                         start,
                         incoming,
                         incomingStart,
                         length)))
                    continue;

                if (length > bestLength)
                {
                    bestIncomingStart = incomingStart;
                    bestLength = length;
                    bestStart = start;
                    bestEvidence = BuildEvidence(
                        historyBodies,
                        start,
                        incomingBodies,
                        incomingStart,
                        length,
                        exact);
                }
            }
        }

        if (bestLength == 0)
            return CaptureReplayDecision.None;

        return new CaptureReplayDecision(
            bestIncomingStart,
            bestLength,
            bestStart,
            bestEvidence);
    }

    public static bool LooksLikeRestampedBatch(IReadOnlyList<string> lines)
    {
        if (lines.Count < MinimumReplayLines)
            return false;

        string[] bodies = lines.Select(NormalizeBody).ToArray();
        for (int start = 0; start <= lines.Count - MinimumReplayLines; start++)
        {
            if (LooksLikeRestampedWindow(lines, bodies, start))
                return true;
        }

        return false;
    }

    // Manual log review deliberately uses the same 100-line context as live
    // capture. This prevents a similar line or scene from hours earlier from
    // being treated as a duplicate just because its text happens to match.
    public static IReadOnlyList<ExistingLogReplayMatch> FindInExistingLog(
        IReadOnlyList<string> lines)
    {
        if (lines.Count < MinimumReplayLines * 2)
            return Array.Empty<ExistingLogReplayMatch>();

        var matches = new List<ExistingLogReplayMatch>();
        for (int candidateWindowStart = MinimumReplayLines;
             candidateWindowStart <= lines.Count - MinimumReplayLines;)
        {
            // A repeated timestamp is ordinary during an active conversation.
            // Inspect only compact, varied windows, but inspect every eligible
            // start. Skipping a whole 100-row window after one non-match was
            // able to hide a second replay later in the same collapsed batch.
            bool collapsed = IsCollapsedWindow(lines, candidateWindowStart);
            if (!collapsed && !MayStartExactReplay(lines, candidateWindowStart))
            {
                candidateWindowStart++;
                continue;
            }

            int candidateLength = Math.Min(HistoryLimit, lines.Count - candidateWindowStart);
            string[] candidateWindow = lines
                .Skip(candidateWindowStart)
                .Take(candidateLength)
                .ToArray();
            string[] candidateBodies = candidateWindow.Select(NormalizeBody).ToArray();
            if (!LooksLikeRestampedWindow(candidateWindow, candidateBodies, 0))
            {
                candidateWindowStart++;
                continue;
            }

            int historyStart = Math.Max(0, candidateWindowStart - HistoryLimit);
            string[] historyWindow = lines
                .Skip(historyStart)
                .Take(candidateWindowStart - historyStart)
                .ToArray();
            CaptureReplayDecision decision = EvaluateAgainst(historyWindow, candidateWindow);
            if (!decision.IsReplay)
            {
                candidateWindowStart++;
                continue;
            }

            int absoluteCandidateStart = candidateWindowStart + decision.CandidateStartIndex;
            matches.Add(new ExistingLogReplayMatch(
                absoluteCandidateStart,
                decision.CandidateCount,
                historyStart + decision.HistoricalStartIndex,
                decision.Evidence));

            // The entire confirmed candidate range is one scene. Continuing
            // inside it would only create duplicate review prompts.
            candidateWindowStart = absoluteCandidateStart + decision.CandidateCount;
        }

        return matches;
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

        // Rows without a visible FiveM timestamp use Afterline's resolved server
        // clock (UTC when no server clock is available) for the same safeguard.
        string[] fallbackTimedReplay = WithFallbackTimestamps(
            bodies,
            historicalStart.AddMinutes(51));
        if (!EvaluateAgainst(history, fallbackTimedReplay).IsReplay)
            throw new InvalidOperationException(
                "A replay using the resolved server/UTC timestamp fallback was not detected.");

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
        if (!EvaluateAgainst(history, exactReplay).IsReplay)
            throw new InvalidOperationException(
                "An exact timestamp-identical replay was not detected.");

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

        string[] fullLog = history.Concat(restamped).Concat(restamped).ToArray();
        ExistingLogReplayMatch[] existing = FindInExistingLog(fullLog).ToArray();
        if (existing.Length != 2 ||
            existing[0].CandidateStartIndex != bodies.Length ||
            existing[1].CandidateStartIndex != bodies.Length * 2 ||
            existing.Any(match => match.CandidateCount != bodies.Length))
        {
            throw new InvalidOperationException(
                "The existing-log duplicate scan did not return every proven replay range.");
        }

        string[] exactFullLog = history.Concat(history).ToArray();
        ExistingLogReplayMatch[] exactExisting = FindInExistingLog(exactFullLog).ToArray();
        if (exactExisting.Length != 1 ||
            exactExisting[0].CandidateStartIndex != bodies.Length ||
            exactExisting[0].CandidateCount != bodies.Length)
        {
            throw new InvalidOperationException(
                "The existing-log duplicate scan did not detect an exact timestamp-identical replay.");
        }
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

    private static Dictionary<string, List<int>> BuildHistoryIndex(
        IReadOnlyList<string> bodies)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int index = 0; index < bodies.Count; index++)
        {
            string body = bodies[index];
            if (!result.TryGetValue(body, out List<int>? indexes))
            {
                indexes = new List<int>();
                result[body] = indexes;
            }
            indexes.Add(index);
        }
        return result;
    }

    private static bool LooksLikeRestampedWindow(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> bodies,
        int start)
        => CountDistinctBodies(bodies, start, MinimumReplayLines) >= MinimumDistinctBodies &&
           TryGetTimestampSpan(lines, start, MinimumReplayLines, out TimeSpan span) &&
           span <= MaximumRestampedSpan;

    private static bool StartsCollapsedBatch(
        IReadOnlyList<string> lines,
        int start)
    {
        if (start > lines.Count - MinimumReplayLines)
            return false;

        if (!IsCollapsedWindow(lines, start))
            return false;

        // A normal multi-line conversation can share one timestamp. It is only
        // a candidate batch if the preceding window was not already collapsed.
        if (start == 0 || start < MinimumReplayLines)
            return true;
        return !IsCollapsedWindow(lines, start - 1);
    }

    private static bool IsCollapsedWindow(
        IReadOnlyList<string> lines,
        int start)
    {
        if (start < 0 || start > lines.Count - MinimumReplayLines)
            return false;

        if (!TryGetTimestampSpan(lines, start, MinimumReplayLines, out TimeSpan span) ||
            span > MaximumRestampedSpan)
            return false;

        // This runs at every possible start during a manual review. Avoiding a
        // short-lived array and LINQ chain per row keeps a 10,000-line scan
        // friendly to a game running alongside Afterline.
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (int offset = 0; offset < MinimumReplayLines; offset++)
        {
            distinct.Add(NormalizeBody(lines[start + offset]));
            if (distinct.Count >= MinimumDistinctBodies)
                return true;
        }

        return false;
    }

    private static bool MayStartExactReplay(
        IReadOnlyList<string> lines,
        int candidateStart)
    {
        if (candidateStart < MinimumReplayLines ||
            candidateStart > lines.Count - MinimumReplayLines)
            return false;

        int firstHistoryIndex = Math.Max(0, candidateStart - HistoryLimit);
        for (int historyStart = firstHistoryIndex; historyStart < candidateStart; historyStart++)
        {
            if (!string.Equals(lines[historyStart], lines[candidateStart], StringComparison.Ordinal))
                continue;

            int maximum = Math.Min(
                candidateStart - historyStart,
                lines.Count - candidateStart);
            if (maximum < MinimumReplayLines)
                continue;

            int length = 0;
            while (length < maximum &&
                   string.Equals(lines[historyStart + length], lines[candidateStart + length], StringComparison.Ordinal))
            {
                length++;
            }

            if (length >= MinimumReplayLines &&
                CountDistinctLineBodies(lines, candidateStart, length) >= MinimumDistinctBodies)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountDistinctLineBodies(
        IReadOnlyList<string> lines,
        int start,
        int length)
    {
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (int offset = 0; offset < length; offset++)
        {
            distinct.Add(NormalizeBody(lines[start + offset]));
            if (distinct.Count >= MinimumDistinctBodies)
                break;
        }
        return distinct.Count;
    }

    private static string BuildEvidence(
        IReadOnlyList<string> historyBodies,
        int historyStart,
        IReadOnlyList<string> incomingBodies,
        int incomingStart,
        int length,
        bool exact)
    {
        int leading = 0;
        for (int offset = 1; offset <= ContextRows && historyStart >= offset && incomingStart >= offset; offset++)
        {
            if (!string.Equals(historyBodies[historyStart - offset], incomingBodies[incomingStart - offset], StringComparison.Ordinal))
                break;
            leading++;
        }

        int trailing = 0;
        for (int offset = 0;
             offset < ContextRows && historyStart + length + offset < historyBodies.Count && incomingStart + length + offset < incomingBodies.Count;
             offset++)
        {
            if (!string.Equals(
                    historyBodies[historyStart + length + offset],
                    incomingBodies[incomingStart + length + offset],
                    StringComparison.Ordinal))
                break;
            trailing++;
        }

        string kind = exact
            ? "ordered exact replay"
            : "ordered replay with collapsed replacement timestamps";
        return $"{kind}; {leading} previous and {trailing} following scene anchor(s) matched";
    }

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
