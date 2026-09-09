using Afterline.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Afterline.Services;

public enum CaptureState
{
    WaitingForFiveM,
    WaitingForNui,
    Capturing,
    ReconnectGrace,
    Stopped
}

public sealed class CaptureCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan NuiDisconnectConfirmation = TimeSpan.FromSeconds(1.25);
    private static readonly Regex VisibleTimestampPrefix = new(
        @"^\[(?<time>\d{1,2}:\d{2}:\d{2})\]",
        RegexOptions.Compiled);

    private readonly FiveMDevToolsChatReader _reader = new();
    private readonly SessionJournal _journal;
    private readonly LastSessionCacheService _lastSessionCache = new();
    private readonly RawCaptureFailsafeService _rawCaptureFailsafe = new();
    private readonly CaptureReplayGuard _replayGuard = new();
    private readonly PotentialDuplicateCandidateService _potentialDuplicates = new();
    private readonly Func<AppSettings> _settings;
    private readonly Action<AppSettings>? _persistSettings;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);

    private Task? _worker;
    private List<string> _previousVisible = new();
    private DateTime? _disconnectedAt;
    private DateTime? _nuiUnavailableSince;
    private ServerSessionInfo? _currentServer;
    private bool _resumedSessionAwaitingValidation;
    private string? _lastCaptureFailureSignature;
    private DateTime _lastCaptureFailureLoggedUtc = DateTime.MinValue;
    private long _eventSequence;
    private long _activeReaderGeneration;
    private DateTime? _lastEventObservedAt;
    private DateTime? _captureGapDetectedAt;

    public CaptureState State { get; private set; } = CaptureState.Stopped;
    public DateTime? LastCaptureAt { get; private set; }
    public DateTime? LastSuccessfulReadAt { get; private set; }
    public string? LastError { get; private set; }
    public ServerSessionInfo? CurrentServer => _currentServer;
    public long LastEventSequence => Interlocked.Read(ref _eventSequence);
    public DateTime? LastEventObservedAt => _lastEventObservedAt;
    public DateTime? CaptureGapDetectedAt => _captureGapDetectedAt;

    public event EventHandler<ChatEntry>? MessageCaptured;
    public event EventHandler<CaptureState>? StateChanged;
    public event EventHandler<string>? SessionFinalized;
    public event EventHandler<ServerSessionChangedEventArgs>? ServerSessionChanged;
    public event EventHandler? CachedSessionReplayStarting;
    public event EventHandler? ServerClockChanged;

    public CaptureCoordinator(
        SessionJournal journal,
        Func<AppSettings> settings,
        Action<AppSettings>? persistSettings = null)
    {
        _journal = journal;
        _settings = settings;
        _persistSettings = persistSettings;
    }

    public async Task StartAsync()
    {
        if (_worker is not null) return;

        await TryBeginRawCaptureRunAsync(_cts.Token);
        _previousVisible = (await _journal.RecoverAsync(
            _settings().ArchiveRoot,
            _cts.Token)).ToList();
        _replayGuard.Reset(_previousVisible);
        if (_journal.ResumedServer is not null)
        {
            _currentServer = _journal.ResumedServer;
            _resumedSessionAwaitingValidation = true;
            NotifyServerChanged();
        }
        await TryRecoverInterruptedRawSnapshotAsync(_cts.Token);
        _worker = Task.Run(WorkerAsync);
    }

    public async Task<int> ParseCurrentChatAsync()
    {
        await _captureGate.WaitAsync(_cts.Token);
        try
        {
            Exception? liveReadError = null;
            bool liveReadSucceeded = false;
            int captured = 0;

            if (FiveMProcessService.IsRunning())
            {
                try
                {
                    AppSettings settings = _settings();
                    IReadOnlyList<CapturedChatLine> current =
                        await _reader.ReadVisibleLinesAsync(_cts.Token);
                    LastSuccessfulReadAt = DateTime.Now;
                    string[] currentText = current.Select(line => line.Text).ToArray();
                    await TryWriteRawSnapshotAsync(
                        current,
                        _reader.CurrentServer,
                        _cts.Token);
                    await ValidateResumedSessionAsync(
                        currentText,
                        _reader.CurrentServer,
                        settings,
                        _cts.Token);
                    await HandleObservedServerCoreAsync(
                        _reader.CurrentServer,
                        settings,
                        _cts.Token);
                    _nuiUnavailableSince = null;
                    _disconnectedAt = null;
                    SetState(CaptureState.Capturing);
                    LastError = null;

                    captured = await CaptureAvailableLinesAsync(
                        current,
                        settings,
                        _cts.Token);
                    await TryMarkRawSnapshotProcessedAsync(_cts.Token);
                    liveReadSucceeded = true;
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    liveReadError = ex;
                    LastError = ex.Message;
                    LogCaptureFailure("Manual current-chat read failed.", ex);
                    await _reader.ResetAsync();
                }
            }

            // A successful DevTools read is authoritative. Replaying the
            // persistent session cache afterwards clears Live Chat and fills it
            // with an old snapshot, which can look like a huge duplicate block.
            // The cache is strictly an offline/unavailable-FiveM fallback.
            if (liveReadSucceeded)
            {
                LastError = null;
                return captured;
            }

            IReadOnlyList<ChatEntry> cachedEntries =
                await _lastSessionCache.ReadAsync(_cts.Token);

            if (cachedEntries.Count > 0)
            {
                ReplayCachedEntries(cachedEntries);
                LastError = null;
                return cachedEntries.Count;
            }

            if (!liveReadSucceeded)
            {
                if (liveReadError is not null)
                    throw new InvalidOperationException(
                        "Unable to read the current FiveM chat and no cached previous session is available.",
                        liveReadError);

                throw new InvalidOperationException(
                    "No cached chat session is available yet. Afterline must have captured the session while it was running.");
            }

            LastError = null;
            return captured;
        }
        finally
        {
            _captureGate.Release();
        }
    }

    public async Task<int> RefreshConnectionAsync()
    {
        AppSettings settings = _settings();
        if (!FiveMProcessService.Refresh())
        {
            await HandleFiveMAbsentAsync(settings);
            LastError = "FiveM is not running.";
            throw new InvalidOperationException(LastError);
        }

        await _captureGate.WaitAsync(_cts.Token);
        try
        {
            // A manual refresh intentionally drops the current DevTools socket.
            // This clears stale targets left behind by a FiveM reconnect without
            // disturbing the on-disk session journal or its overlap checkpoint.
            await _reader.ResetAsync();

            IReadOnlyList<CapturedChatLine> current =
                await _reader.ReadVisibleLinesAsync(_cts.Token);
            LastSuccessfulReadAt = DateTime.Now;
            string[] currentText = current.Select(line => line.Text).ToArray();
            bool visibleChatChanged = !_previousVisible.SequenceEqual(
                currentText,
                StringComparer.Ordinal);
            if (visibleChatChanged)
            {
                await TryWriteRawSnapshotAsync(
                    current,
                    _reader.CurrentServer,
                    _cts.Token);
            }

            await ValidateResumedSessionAsync(
                currentText,
                _reader.CurrentServer,
                settings,
                _cts.Token);
            await HandleObservedServerCoreAsync(
                _reader.CurrentServer,
                settings,
                _cts.Token);

            int captured = await CaptureAvailableLinesAsync(
                current,
                settings,
                _cts.Token);
            if (visibleChatChanged)
                await TryMarkRawSnapshotProcessedAsync(_cts.Token);

            _nuiUnavailableSince = null;
            _disconnectedAt = null;
            LastError = null;
            SetState(CaptureState.Capturing);
            return captured;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            LogCaptureFailure("Manual FiveM reconnection failed.", ex);
            await _reader.ResetAsync();
            SetState(CaptureState.WaitingForNui);
            throw new InvalidOperationException(
                "FiveM was detected, but Afterline could not reconnect to its active chat.",
                ex);
        }
        finally
        {
            _captureGate.Release();
        }
    }

    public async Task FinishSessionAsync()
    {
        string? path = await _journal.FinalizeAsync(
            _settings().ArchiveRoot,
            _cts.Token);
        if (path is not null)
            SessionFinalized?.Invoke(this, path);
    }

    private async Task WorkerAsync()
    {
        DateTime lastReconciliationUtc = DateTime.MinValue;
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                AppSettings settings = _settings();
                bool fiveMRunning =
                    settings.AutoDetectFiveM &&
                    FiveMProcessService.IsRunning();

                if (!fiveMRunning)
                {
                    await HandleFiveMAbsentAsync(settings);
                    await Task.Delay(TimeSpan.FromSeconds(3), _cts.Token);
                    continue;
                }

                if (!settings.AutoCapture)
                {
                    SetState(CaptureState.WaitingForFiveM);
                    await Task.Delay(TimeSpan.FromSeconds(3), _cts.Token);
                    continue;
                }

                await _captureGate.WaitAsync(_cts.Token);
                try
                {
                    TimeSpan reconciliationRemaining = TimeSpan.FromSeconds(2) -
                        (DateTime.UtcNow - lastReconciliationUtc);
                    ObservedChatSnapshot? observedSnapshot = reconciliationRemaining > TimeSpan.Zero
                        ? await _reader.WaitForVisibleLinesChangedAsync(
                            reconciliationRemaining,
                            _cts.Token)
                        : null;
                    if (observedSnapshot?.IsChatSurfaceUnavailable == true)
                    {
                        // A HUD toggle that only hides the chat leaves the DOM
                        // observer active. If the chat surface is removed,
                        // however, no safe source exists for rows during that
                        // interval. Flag the boundary immediately and wait for
                        // a new baseline rather than importing a later viewport.
                        MarkCaptureGapDetected();
                        LastSuccessfulReadAt = DateTime.Now;
                        LastError = null;
                        SetState(CaptureState.Capturing);
                        continue;
                    }
                    bool isDirectEvent = observedSnapshot is { IsBaseline: false, AddedLines: { Count: > 0 } };
                    long eventSequence = isDirectEvent
                        ? Interlocked.Increment(ref _eventSequence)
                        : 0;
                    IReadOnlyList<CapturedChatLine> current = observedSnapshot is { IsBaseline: false, AddedLines: { Count: > 0 } }
                        ? observedSnapshot.AddedLines
                        : observedSnapshot?.Lines ??
                            await _reader.ReadVisibleLinesAsync(_cts.Token);
                    if (observedSnapshot is null)
                        lastReconciliationUtc = DateTime.UtcNow;
                    DateTimeOffset observedAtUtc = observedSnapshot?.ObservedAtUtc ??
                        DateTimeOffset.UtcNow;
                    LastSuccessfulReadAt = DateTime.Now;
                    string[] currentText = current.Select(line => line.Text).ToArray();
                    string? eventCheckpointId = null;
                    if (isDirectEvent)
                    {
                        eventCheckpointId = await TryWriteRawSnapshotAsync(
                            current,
                            _reader.CurrentServer,
                            _cts.Token,
                            true,
                            eventSequence,
                            observedAtUtc,
                            observedSnapshot?.ReaderGeneration ?? 0);
                    }
                    // The first observer payload is a viewport baseline. It
                    // may represent any point the player has scrolled to, so
                    // it must never be used to infer a disconnect boundary.
                    if (observedSnapshot?.IsBaseline == true)
                        _resumedSessionAwaitingValidation = false;
                    else if (observedSnapshot is not null)
                    {
                        await ValidateResumedSessionAsync(
                            currentText,
                            _reader.CurrentServer,
                            settings,
                            _cts.Token);
                    }
                    await HandleObservedServerCoreAsync(
                        _reader.CurrentServer,
                        settings,
                        _cts.Token);
                    _nuiUnavailableSince = null;
                    _disconnectedAt = null;
                    SetState(CaptureState.Capturing);
                    LastError = null;

                    if (observedSnapshot?.IsBaseline == true)
                    {
                        // Baselines are for continuity and crash recovery only;
                        // chat can be scrolled, so writing it would turn old
                        // rows into a false new scene.
                        _previousVisible = currentText.ToList();
                        ObserveReaderGeneration(observedSnapshot.ReaderGeneration, true);
                        if (_lastEventObservedAt is not null)
                            MarkCaptureGapDetected();
                    }
                    else if (observedSnapshot?.AddedLines is { Count: > 0 } addedLines)
                    {
                        await CaptureAvailableLinesAsync(
                            current,
                            settings,
                            _cts.Token,
                            observedAtUtc,
                            addedLines,
                            eventSequence,
                            eventCheckpointId);
                        ObserveReaderGeneration(observedSnapshot.ReaderGeneration, false);
                    }
                    else if (observedSnapshot is null)
                    {
                        // Event capture can be temporarily unavailable while
                        // FiveM reloads its NUI. Reconciliation must remain a
                        // real capture path, not merely a health probe. The
                        // overlap and replay safeguards still apply, and rows
                        // without a source timestamp are visibly marked as
                        // capture-time estimates.
                        await CaptureAvailableLinesAsync(
                            current,
                            settings,
                            _cts.Token,
                            observedAtUtc);
                    }
                    if (isDirectEvent)
                    {
                        await TryMarkEventCheckpointProcessedAsync(
                            eventCheckpointId,
                            _cts.Token);
                    }
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    LogCaptureFailure("Automatic FiveM chat capture failed; capture will retry.", ex);
                    await _reader.ResetAsync();
                    await HandleNuiUnavailableCoreAsync(
                        settings,
                        _cts.Token);
                }
                finally
                {
                    _captureGate.Release();
                }

            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                DiagnosticLogger.Error("Capture loop error.", ex);
                await Task.Delay(1000, _cts.Token);
            }
        }

        SetState(CaptureState.Stopped);
    }

    private async Task<int> CaptureAvailableLinesAsync(
        IReadOnlyList<CapturedChatLine> current,
        AppSettings settings,
        CancellationToken cancellationToken,
        DateTimeOffset? observedAtUtc = null,
        IReadOnlyList<CapturedChatLine>? confirmedAddedLines = null,
        long eventSequence = 0,
        string? captureEventId = null)
    {
        string[] currentText = current.Select(line => line.Text).ToArray();
        bool isEventStream = confirmedAddedLines is not null;
        if (!isEventStream &&
            _previousVisible.SequenceEqual(currentText, StringComparer.Ordinal))
            return 0;

        int overlap = isEventStream ? 0 : FindOverlap(_previousVisible, currentText);
        if (!isEventStream &&
            !_journal.HasActiveSession &&
            _previousVisible.Count == 0 &&
            _currentServer is not null)
        {
            DateTime baselineTime = ServerTimeService.Resolve(
                settings,
                _currentServer,
                observedAtUtc ?? DateTimeOffset.UtcNow).ServerTime;
            IReadOnlyList<string> existingTail = await _journal.ReadExistingArchiveTailAsync(
                settings.ArchiveRoot,
                _currentServer,
                baselineTime,
                250,
                cancellationToken);
            if (existingTail.Count > 0)
            {
                int sameDayOverlap = FindOverlap(existingTail, currentText);
                if (sameDayOverlap > 0)
                {
                    overlap = Math.Max(overlap, sameDayOverlap);
                }
                else
                {
                    // The visible buffer predates this Afterline run but does
                    // not belong to the tail of the same-day archive. Display
                    // history remains available separately; only future FiveM
                    // rows are eligible for this new journal segment.
                    _previousVisible = currentText.ToList();
                    _replayGuard.Reset(_previousVisible);
                    return 0;
                }
            }
        }
        CapturedChatLine[] pending = isEventStream
            ? confirmedAddedLines!.ToArray()
            : current.Skip(overlap).ToArray();
        string[] pendingText = pending.Select(line => line.Text).ToArray();
        DateTime replayObservedAt = ServerTimeService.Resolve(
            settings,
            _currentServer,
            observedAtUtc ?? DateTimeOffset.UtcNow).ServerTime;
        string[] pendingReplayText = CaptureReplayGuard.WithFallbackTimestamps(
            pendingText,
            replayObservedAt);
        CaptureReplayDecision inMemoryReplay = _replayGuard.Evaluate(pendingReplayText);
        CaptureReplayDecision replay = CaptureReplayDecision.None;
        IReadOnlyList<string> committedTail = Array.Empty<string>();

        if (inMemoryReplay.IsReplay || CaptureReplayGuard.LooksLikeRestampedBatch(pendingReplayText))
        {
            try
            {
                committedTail = await _journal.ReadRecentCommittedLinesAsync(
                    CaptureReplayGuard.HistoryLimit,
                    cancellationToken);
                replay = CaptureReplayGuard.EvaluateAgainst(committedTail, pendingReplayText);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A journal read is required before marking a candidate. If
                // confirmation fails, retain the complete batch unmarked.
                DiagnosticLogger.Error(
                    "Capture replay candidate could not be confirmed against the active chatlog; every row was retained.",
                    ex);
            }
        }

        // A confirmed replay has a long ordered body match, varied content and
        // collapsed replacement timestamps. It is safe to keep it out of the
        // journal entirely. Ambiguous batches never reach this branch and are
        // retained for the user to review instead.
        bool preventConfirmedReplay = replay.IsReplay;
        var committedPendingText = new List<string>(pending.Length);
        if (preventConfirmedReplay)
        {
            DiagnosticLogger.Warn(
                $"Prevented confirmed FiveM chat-buffer replay: {replay.CandidateCount:N0} row(s), " +
                replay.Evidence);
        }

        int captured = 0;

        if (pending.Length > 0 && _previousVisible.Count > 0)
        {
            CapturedChatLine newest = pending[^1];
            if (ServerTimeService.TryLearnFromFreshTimestamp(
                    settings,
                    _currentServer,
                    newest.Text,
                    observedAtUtc ?? DateTimeOffset.UtcNow))
            {
                PersistServerClockSettings(settings);
            }
        }

        for (int pendingIndex = 0; pendingIndex < pending.Length; pendingIndex++)
        {
            if (preventConfirmedReplay &&
                pendingIndex >= replay.CandidateStartIndex &&
                pendingIndex < replay.CandidateStartIndex + replay.CandidateCount)
            {
                continue;
            }

            CapturedChatLine line = pending[pendingIndex];
            string? lineEventId = isEventStream && !string.IsNullOrWhiteSpace(captureEventId)
                ? $"{captureEventId}:{pendingIndex}"
                : null;
            if (lineEventId is not null && await _journal.HasCommittedCaptureEventAsync(
                    lineEventId,
                    cancellationToken))
            {
                continue;
            }
            DateTime observedAt = ServerTimeService.Resolve(
                settings,
                _currentServer,
                observedAtUtc ?? DateTimeOffset.UtcNow).ServerTime;
            var entry = new ChatEntry(
                InferVisibleTimestamp(line.Text, observedAt),
                line.Text,
                capturedColorRuns: line.ColorRuns,
                // Source chat may omit its own event timestamp. Preserve the
                // event/server observation time, but label it as captured so
                // it is never mistaken for a server-provided clock.
                isEstimatedCaptureTime: !VisibleTimestampPrefix.IsMatch(line.Text));

            if (!_journal.HasActiveSession)
            {
                ServerSessionInfo server =
                    _currentServer ?? ServerSessionInfo.Unknown;
                ChatEntry? loginMarker =
                    await _journal.EnsureStartedAsync(
                        settings.ArchiveRoot,
                        entry.CapturedAt,
                        server,
                        cancellationToken);

                if (loginMarker is not null)
                {
                    await TryBeginLastSessionCacheAsync(
                        server,
                        entry.CapturedAt,
                        cancellationToken);
                    await TryAppendLastSessionCacheAsync(
                        loginMarker,
                        cancellationToken);
                    MessageCaptured?.Invoke(this, loginMarker);
                }
            }

            await _journal.AppendAsync(entry, cancellationToken, lineEventId);
            await TryAppendLastSessionCacheAsync(entry, cancellationToken);
            LastCaptureAt = DateTime.Now;
            if (isEventStream)
            {
                _lastEventObservedAt = DateTime.Now;
                if (eventSequence > 0)
                    Interlocked.Exchange(ref _eventSequence, eventSequence);
            }
            MessageCaptured?.Invoke(this, entry);
            captured++;
            committedPendingText.Add(CaptureReplayGuard.WithFallbackTimestamp(
                line.Text,
                entry.CapturedAt));
        }

        _replayGuard.RecordCommitted(committedPendingText);

        _previousVisible = currentText.ToList();
        if (_journal.HasActiveSession)
        {
            await _journal.UpdateVisibleSnapshotAsync(
                _previousVisible,
                cancellationToken);
        }

        return captured;
    }

    public Task<IReadOnlyList<PotentialDuplicateCandidate>> ReadPotentialDuplicatesAsync(
        string? journalPath,
        CancellationToken cancellationToken)
        => _potentialDuplicates.ReadPendingAsync(journalPath, cancellationToken);

    public Task MarkPotentialDuplicatesReviewedAsync(
        IEnumerable<Guid> candidateIds,
        bool removed,
        CancellationToken cancellationToken)
        => _potentialDuplicates.MarkReviewedAsync(candidateIds, removed, cancellationToken);

    public async Task<IReadOnlyList<PotentialDuplicateCandidate>> ScanExistingChatlogForPotentialDuplicatesAsync(
        string journalPath,
        string serverName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(journalPath))
            throw new ArgumentException("A chatlog path is required.", nameof(journalPath));

        string[] lines = await File.ReadAllLinesAsync(journalPath, cancellationToken);
        // The comparison can be sizeable for long chatlogs. Keep it off the UI
        // thread; the caller remains responsive while file content is checked.
        IReadOnlyList<ExistingLogReplayMatch> matches = await Task.Run(
            () => CaptureReplayGuard.FindInExistingLog(lines),
            cancellationToken);
        return await _potentialDuplicates.RecordExistingLogMatchesAsync(
            journalPath,
            serverName,
            lines,
            matches,
            cancellationToken);
    }

    private void LogCaptureFailure(string context, Exception exception)
    {
        bool expectedInterruption = IsExpectedDevToolsInterruption(exception);
        string signature = expectedInterruption
            ? "FiveMDevToolsTemporarilyUnavailable"
            : $"{exception.GetType().FullName}:{exception.Message}";
        DateTime now = DateTime.UtcNow;
        if (string.Equals(signature, _lastCaptureFailureSignature, StringComparison.Ordinal) &&
            now - _lastCaptureFailureLoggedUtc < (expectedInterruption
                ? TimeSpan.FromMinutes(5)
                : TimeSpan.FromSeconds(60)))
        {
            return;
        }

        _lastCaptureFailureSignature = signature;
        _lastCaptureFailureLoggedUtc = now;
        if (expectedInterruption)
        {
            DiagnosticLogger.Warn(
                "FiveM chat became temporarily unavailable; capture is reconnecting automatically.");
            return;
        }

        DiagnosticLogger.Error(context, exception);
    }

    private static bool IsExpectedDevToolsInterruption(Exception exception)
        => exception is TaskCanceledException or System.Net.WebSockets.WebSocketException ||
           exception is IOException io &&
           (io.Message.Contains("NUI target is not available", StringComparison.OrdinalIgnoreCase) ||
            io.Message.Contains("chat frame is not available", StringComparison.OrdinalIgnoreCase) ||
            io.Message.Contains("execution context is unavailable", StringComparison.OrdinalIgnoreCase));

    private async Task HandleObservedServerCoreAsync(
        ServerSessionInfo observed,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (_currentServer is null)
        {
            _currentServer = observed;
            ApplyServerClockHint(settings);
            NotifyServerChanged();
            return;
        }

        if (_currentServer.HasDifferentKnownAddress(observed))
        {
            await FinalizeCurrentServerCoreAsync(
                settings,
                CurrentServerTime(settings),
                false,
                cancellationToken);
            _currentServer = observed;
            ApplyServerClockHint(settings);
            NotifyServerChanged();
            return;
        }

        bool metadataImproved =
            (string.IsNullOrWhiteSpace(_currentServer.Address) &&
             !string.IsNullOrWhiteSpace(observed.Address)) ||
            (!_currentServer.HasFriendlyName && observed.HasFriendlyName);

        metadataImproved |=
            (string.IsNullOrWhiteSpace(_currentServer.TimeZoneIdHint) &&
             !string.IsNullOrWhiteSpace(observed.TimeZoneIdHint)) ||
            (_currentServer.UtcOffsetMinutesHint is null &&
             observed.UtcOffsetMinutesHint is not null);

        if (metadataImproved)
        {
            _currentServer = new ServerSessionInfo
            {
                Address = string.IsNullOrWhiteSpace(observed.Address)
                    ? _currentServer.Address
                    : observed.Address,
                Name = observed.HasFriendlyName
                    ? observed.Name
                    : _currentServer.Name,
                TimeZoneIdHint = string.IsNullOrWhiteSpace(observed.TimeZoneIdHint)
                    ? _currentServer.TimeZoneIdHint
                    : observed.TimeZoneIdHint,
                UtcOffsetMinutesHint = observed.UtcOffsetMinutesHint ??
                                       _currentServer.UtcOffsetMinutesHint
            };
            ApplyServerClockHint(settings);
            NotifyServerChanged();
        }
    }

    private async Task HandleFiveMAbsentAsync(AppSettings settings)
    {
        CaptureState idleState = GetDisconnectedIdleState(
            settings,
            CaptureState.WaitingForFiveM);
        if (!_journal.HasActiveSession &&
            _currentServer is null &&
            _nuiUnavailableSince is null &&
            State == idleState)
            return;

        await _captureGate.WaitAsync(_cts.Token);
        try
        {
            await _reader.ResetAsync();
            _nuiUnavailableSince = null;

            if (_journal.HasActiveSession || _currentServer is not null)
            {
                await FinalizeCurrentServerCoreAsync(
                    settings,
                    CurrentServerTime(settings),
                    false,
                    _cts.Token);
            }

            SetState(idleState);
        }
        finally
        {
            _captureGate.Release();
        }
    }

    private async Task HandleNuiUnavailableCoreAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (!_journal.HasActiveSession && _currentServer is null)
        {
            SetState(GetDisconnectedIdleState(
                settings,
                CaptureState.WaitingForNui));
            return;
        }

        _nuiUnavailableSince ??= DateTime.Now;
        if (DateTime.Now - _nuiUnavailableSince.Value <
            NuiDisconnectConfirmation)
        {
            SetState(CaptureState.WaitingForNui);
            return;
        }

        await FinalizeCurrentServerCoreAsync(
            settings,
            CurrentServerTime(settings),
            false,
            cancellationToken);
        SetState(GetDisconnectedIdleState(
            settings,
            CaptureState.WaitingForNui));
    }

    private async Task FinalizeCurrentServerCoreAsync(
        AppSettings settings,
        DateTime observedAt,
        bool resetReader,
        CancellationToken cancellationToken)
    {
        if (_journal.HasActiveSession)
        {
            ChatEntry? disconnectMarker =
                await _journal.MarkDisconnectedAsync(
                    observedAt,
                    cancellationToken);

            if (disconnectMarker is not null)
            {
                await TryAppendLastSessionCacheAsync(
                    disconnectMarker,
                    cancellationToken);
                MessageCaptured?.Invoke(this, disconnectMarker);
            }

            string? path = await _journal.FinalizeAsync(
                settings.ArchiveRoot,
                cancellationToken);
            if (path is not null)
                SessionFinalized?.Invoke(this, path);
        }

        if (resetReader)
            await _reader.ResetAsync();

        _previousVisible.Clear();
        _replayGuard.Reset();
        _nuiUnavailableSince = null;
        _disconnectedAt = DateTime.Now;

        if (_currentServer is not null)
        {
            _currentServer = null;
            NotifyServerChanged();
        }
    }

    private async Task TryBeginLastSessionCacheAsync(
        ServerSessionInfo server,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            await _lastSessionCache.BeginAsync(
                server,
                startedAt,
                cancellationToken);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to initialize the persistent last-session cache.",
                ex);
        }
    }

    private async Task TryAppendLastSessionCacheAsync(
        ChatEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            await _lastSessionCache.AppendAsync(
                entry,
                cancellationToken);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to update the persistent last-session cache.",
                ex);
        }
    }

    private async Task TryBeginRawCaptureRunAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _rawCaptureFailsafe.BeginRunAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to initialize the raw capture failsafe.",
                ex);
        }
    }

    private async Task<string?> TryWriteRawSnapshotAsync(
        IReadOnlyList<CapturedChatLine> current,
        ServerSessionInfo server,
        CancellationToken cancellationToken,
        bool isEventStream = false,
        long eventSequence = 0,
        DateTimeOffset? observedAtUtc = null,
        long readerGeneration = 0)
    {
        try
        {
            return await _rawCaptureFailsafe.WriteSnapshotAsync(
                current,
                server,
                cancellationToken,
                isEventStream,
                eventSequence,
                observedAtUtc,
                readerGeneration);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to write the pre-parse raw capture failsafe.",
                ex);
            return null;
        }
    }

    private async Task TryMarkEventCheckpointProcessedAsync(
        string? eventCheckpointId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _rawCaptureFailsafe.MarkEventProcessedAsync(
                eventCheckpointId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to mark the durable FiveM event checkpoint as processed.",
                ex);
        }
    }

    private async Task TryMarkRawSnapshotProcessedAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await _rawCaptureFailsafe.MarkProcessedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to mark the raw capture snapshot as processed.",
                ex);
        }
    }

    private async Task TryRecoverInterruptedRawSnapshotAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            CaptureRunManifest? manifest =
                await _rawCaptureFailsafe.ReadRunManifestAsync(cancellationToken);
            if (manifest?.PreviousRunEndedUnexpectedly != true)
                return;

            IReadOnlyList<RawCaptureSnapshot> eventCheckpoints =
                await _rawCaptureFailsafe.ReadInterruptedEventCheckpointsAsync(cancellationToken);
            if (eventCheckpoints.Count > 0)
            {
                int recovered = 0;
                foreach (RawCaptureSnapshot checkpoint in eventCheckpoints)
                {
                    IReadOnlyList<CapturedChatLine> lines = checkpoint.GetCapturedLines();
                    if (lines.Count == 0) continue;

                    var server = CreateServerFromRawSnapshot(checkpoint);
                    _resumedSessionAwaitingValidation = false;
                    await HandleObservedServerCoreAsync(server, _settings(), cancellationToken);
                    int restoredLines = await CaptureAvailableLinesAsync(
                        lines,
                        _settings(),
                        cancellationToken,
                        checkpoint.ObservedAtUtc == default ? null : checkpoint.ObservedAtUtc,
                        lines,
                        checkpoint.EventSequence,
                        checkpoint.EventId);
                    await _rawCaptureFailsafe.MarkEventProcessedAsync(
                        checkpoint.EventId,
                        cancellationToken);
                    recovered += restoredLines;
                }

                DiagnosticLogger.Info(
                    $"Recovered {recovered:N0} durable FiveM chat event line(s) from the interrupted capture journal.");
                return;
            }

            RawCaptureSnapshot? snapshot =
                await _rawCaptureFailsafe.ReadLatestRecoverableAsync(cancellationToken);
            if (snapshot is null || snapshot.ProcessedAt is not null || snapshot.Lines.Count == 0)
                return;

            if (!snapshot.IsEventStream)
            {
                MarkCaptureGapDetected();
                DiagnosticLogger.Warn(
                    "Preserved a legacy visible-chat recovery snapshot for manual review; it was not appended because a viewport is not an event stream.");
                return;
            }

            IReadOnlyList<CapturedChatLine> recoveredLines = snapshot.GetCapturedLines();

            ServerSessionInfo recoveredServer = CreateServerFromRawSnapshot(snapshot);
            // This checkpoint was captured from a confirmed appended DOM row,
            // not a viewport. It belongs to the resumed session even when its
            // single line does not overlap the prior visible-window snapshot.
            _resumedSessionAwaitingValidation = false;
            await HandleObservedServerCoreAsync(
                recoveredServer,
                _settings(),
                cancellationToken);
            await CaptureAvailableLinesAsync(
                recoveredLines,
                _settings(),
                cancellationToken,
                confirmedAddedLines: recoveredLines,
                eventSequence: snapshot.EventSequence,
                captureEventId: snapshot.EventId);
            await _rawCaptureFailsafe.MarkProcessedAsync(cancellationToken);
            DiagnosticLogger.Info(
                $"Recovered {snapshot.Lines.Count:N0} visible chat line(s) from the interrupted raw capture checkpoint.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to merge the interrupted raw capture checkpoint into the active chatlog.",
                ex);
        }
    }

    private static ServerSessionInfo CreateServerFromRawSnapshot(RawCaptureSnapshot snapshot)
        => new()
        {
            Name = string.IsNullOrWhiteSpace(snapshot.ServerName) ||
                   string.Equals(snapshot.ServerName, "Unknown Server", StringComparison.OrdinalIgnoreCase) ||
                   snapshot.ServerName.StartsWith("Unresolved Server ", StringComparison.OrdinalIgnoreCase)
                ? null
                : snapshot.ServerName,
            Address = snapshot.ServerAddress
        };

    private void ObserveReaderGeneration(long readerGeneration, bool isBaseline)
    {
        if (readerGeneration <= 0) return;
        if (_activeReaderGeneration == 0)
        {
            _activeReaderGeneration = readerGeneration;
            return;
        }

        if (_activeReaderGeneration == readerGeneration) return;

        // A changed DevTools/NUI generation cannot prove that any currently
        // visible rows are new. The baseline is retained only for continuity;
        // a direct post-reconnect event is still safe to append on its own.
        if (_lastEventObservedAt is not null || !isBaseline)
            MarkCaptureGapDetected();
        _activeReaderGeneration = readerGeneration;
    }

    private void ReplayCachedEntries(IReadOnlyList<ChatEntry> entries)
    {
        CachedSessionReplayStarting?.Invoke(this, EventArgs.Empty);
        foreach (ChatEntry entry in entries)
            MessageCaptured?.Invoke(this, entry);
    }

    private async Task ValidateResumedSessionAsync(
        IReadOnlyList<string> current,
        ServerSessionInfo observedServer,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (!_resumedSessionAwaitingValidation)
            return;

        _resumedSessionAwaitingValidation = false;
        if (!_journal.HasActiveSession ||
            _previousVisible.Count == 0 ||
            current.Count == 0 ||
            FindOverlap(_previousVisible, current) > 0)
            return;

        DateTime boundary = InferVisibleTimestamp(
            current[0],
            CurrentServerTime(settings));
        await FinalizeCurrentServerCoreAsync(
            settings,
            boundary,
            false,
            cancellationToken);
        _currentServer = observedServer;
        NotifyServerChanged();
        DiagnosticLogger.Info(
            "The resumed FiveM chat buffer had no overlap with its checkpoint; a genuine new session boundary was created.");
    }

    private static DateTime InferVisibleTimestamp(string line, DateTime observedAt)
    {
        Match match = VisibleTimestampPrefix.Match(line);
        if (!match.Success ||
            !DateTime.TryParseExact(
                match.Groups["time"].Value,
                "H:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
            return observedAt;

        DateTime timestamp = observedAt.Date.Add(parsed.TimeOfDay);
        if (timestamp > observedAt.AddHours(12))
            timestamp = timestamp.AddDays(-1);
        return timestamp;
    }

    private CaptureState GetDisconnectedIdleState(
        AppSettings settings,
        CaptureState fallback)
    {
        if (_disconnectedAt is null) return fallback;

        TimeSpan grace = TimeSpan.FromMinutes(
            Math.Max(0, settings.ReconnectGraceMinutes));
        if (grace > TimeSpan.Zero &&
            DateTime.Now - _disconnectedAt.Value < grace)
            return CaptureState.ReconnectGrace;

        return fallback;
    }

    private void NotifyServerChanged()
        => ServerSessionChanged?.Invoke(
            this,
            new ServerSessionChangedEventArgs(_currentServer));

    private void MarkCaptureGapDetected()
    {
        if (_captureGapDetectedAt is not null)
            return;

        _captureGapDetectedAt = DateTime.Now;
        DiagnosticLogger.Warn(
            "FiveM chat UI reattached after live capture had begun. Afterline kept the new viewport as a baseline and did not import it, so no scrolled or late rows can be written out of order.");
    }

    private void SetState(CaptureState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static int FindOverlap(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines)
    {
        int exact = FindOverlapCore(oldLines, newLines, static line => line);
        if (exact > 0) return exact;

        // FiveM adds/removes the timestamp prefix on every retained row when the
        // player toggles timestamps. Treat that as a presentation-only change,
        // but require multiple varied rows so a legitimate repeated message is
        // never silently swallowed.
        if (!HasConfirmedTimestampPresentationSwitch(oldLines, newLines))
            return 0;

        int normalized = FindOverlapCore(
            oldLines,
            newLines,
            static line => VisibleTimestampPrefix.Replace(line ?? string.Empty, string.Empty).TrimStart());
        if (normalized < 2)
            return 0;

        int distinct = newLines.Take(normalized)
            .Select(line => VisibleTimestampPrefix.Replace(line ?? string.Empty, string.Empty).Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .Count();
        return distinct >= 2 ? normalized : 0;
    }

    private static int FindOverlapCore(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines,
        Func<string, string> normalize)
    {
        int max = Math.Min(oldLines.Count, newLines.Count);

        for (int length = max; length > 0; length--)
        {
            bool same = true;
            for (int i = 0; i < length; i++)
            {
                if (!string.Equals(
                        normalize(oldLines[oldLines.Count - length + i]),
                        normalize(newLines[i]),
                        StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }

            if (same) return length;
        }

        return 0;
    }

    private static bool HasConfirmedTimestampPresentationSwitch(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines)
    {
        if (oldLines.Count < 2 || newLines.Count < 2)
            return false;

        bool oldStamped = VisibleTimestampPrefix.IsMatch(oldLines[^1]);
        bool newStamped = VisibleTimestampPrefix.IsMatch(newLines[0]);
        if (oldStamped == newStamped)
            return false;

        return oldLines.All(line => VisibleTimestampPrefix.IsMatch(line) == oldStamped) &&
               newLines.All(line => VisibleTimestampPrefix.IsMatch(line) == newStamped);
    }

    private DateTime CurrentServerTime(AppSettings settings)
        => ServerTimeService.Resolve(
            settings,
            _currentServer,
            DateTimeOffset.UtcNow).ServerTime;

    private void ApplyServerClockHint(AppSettings settings)
    {
        if (ServerTimeService.ApplyServerHint(settings, _currentServer))
            PersistServerClockSettings(settings);
    }

    private void PersistServerClockSettings(AppSettings settings)
    {
        try
        {
            _persistSettings?.Invoke(settings);
            ServerClockChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to persist the detected server timezone.", ex);
        }
    }

    internal static int FindOverlapForSmokeTest(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines)
        => FindOverlap(oldLines, newLines);

    // The capture loop deliberately has no dependency on Afterline's foreground
    // state. This regression check models tabbing away and back while FiveM keeps
    // exposing its complete visible buffer: retained rows must overlap and only
    // the genuinely new row may be delivered.
    internal static void RunFocusIndependentContinuitySmokeTest()
    {
        string[] beforeFocusChange =
        {
            "[20:14:01] Mina says: First.",
            "[20:14:02] Samayo says: Second.",
            "[20:14:03] Bianca says: Third."
        };
        string[] whileAfterlineIsFocused = beforeFocusChange.ToArray();
        string[] afterFocusReturnsToFiveM = beforeFocusChange
            .Append("[20:14:04] Mina says: Fourth.")
            .ToArray();

        if (FindOverlap(beforeFocusChange, whileAfterlineIsFocused) != beforeFocusChange.Length ||
            FindOverlap(whileAfterlineIsFocused, afterFocusReturnsToFiveM) != beforeFocusChange.Length)
        {
            throw new InvalidOperationException(
                "Capture continuity changed across an application focus transition.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_worker is not null)
        {
            try { await _worker; }
            catch { }
        }

        // Stopping or updating Afterline is not a FiveM disconnect. The journal
        // already checkpoints each line and visible snapshot, so leave the active
        // state resumable and close only the Afterline run manifest. A real FiveM
        // disappearance is finalized by the worker before shutdown.
        try
        {
            await _rawCaptureFailsafe.MarkRunCleanlyClosedAsync(
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to mark the capture run as cleanly closed.",
                ex);
        }

        await _reader.DisposeAsync();
        _captureGate.Dispose();
        _cts.Dispose();
    }
}
