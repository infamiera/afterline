using Afterline.Models;

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

    private readonly FiveMDevToolsChatReader _reader = new();
    private readonly SessionJournal _journal;
    private readonly LastSessionCacheService _lastSessionCache = new();
    private readonly RawCaptureFailsafeService _rawCaptureFailsafe = new();
    private readonly Func<AppSettings> _settings;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);

    private Task? _worker;
    private List<string> _previousVisible = new();
    private DateTime? _disconnectedAt;
    private DateTime? _nuiUnavailableSince;
    private ServerSessionInfo? _currentServer;

    public CaptureState State { get; private set; } = CaptureState.Stopped;
    public DateTime? LastCaptureAt { get; private set; }
    public string? LastError { get; private set; }
    public ServerSessionInfo? CurrentServer => _currentServer;

    public event EventHandler<ChatEntry>? MessageCaptured;
    public event EventHandler<CaptureState>? StateChanged;
    public event EventHandler<string>? SessionFinalized;
    public event EventHandler<ServerSessionChangedEventArgs>? ServerSessionChanged;
    public event EventHandler? CachedSessionReplayStarting;

    public CaptureCoordinator(SessionJournal journal, Func<AppSettings> settings)
    {
        _journal = journal;
        _settings = settings;
    }

    public async Task StartAsync()
    {
        if (_worker is not null) return;

        await TryBeginRawCaptureRunAsync(_cts.Token);
        _previousVisible = (await _journal.RecoverAsync(
            _settings().ArchiveRoot,
            _cts.Token)).ToList();
        _worker = Task.Run(WorkerAsync);
    }

    public async Task<int> ParseCurrentChatAsync()
    {
        await _captureGate.WaitAsync(_cts.Token);
        try
        {
            Exception? liveReadError = null;

            if (FiveMProcessService.IsRunning())
            {
                try
                {
                    AppSettings settings = _settings();
                    IReadOnlyList<string> current =
                        await _reader.ReadVisibleLinesAsync(_cts.Token);
                    await TryWriteRawSnapshotAsync(
                        current,
                        _reader.CurrentServer,
                        _cts.Token);
                    await HandleObservedServerCoreAsync(
                        _reader.CurrentServer,
                        settings,
                        _cts.Token);
                    _nuiUnavailableSince = null;
                    _disconnectedAt = null;
                    SetState(CaptureState.Capturing);
                    LastError = null;

                    int captured = await CaptureAvailableLinesAsync(
                        current,
                        settings,
                        _cts.Token);
                    await TryMarkRawSnapshotProcessedAsync(_cts.Token);
                    return captured;
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    liveReadError = ex;
                    LastError = ex.Message;
                    await _reader.ResetAsync();
                }
            }

            IReadOnlyList<ChatEntry> cachedEntries =
                await _lastSessionCache.ReadAsync(_cts.Token);

            if (cachedEntries.Count == 0)
            {
                if (liveReadError is not null)
                    throw new InvalidOperationException(
                        "Unable to read the current FiveM chat and no cached previous session is available.",
                        liveReadError);

                throw new InvalidOperationException(
                    "No cached chat session is available yet. Afterline must have captured the session while it was running.");
            }

            CachedSessionReplayStarting?.Invoke(this, EventArgs.Empty);
            foreach (ChatEntry entry in cachedEntries)
                MessageCaptured?.Invoke(this, entry);

            LastError = null;
            return cachedEntries.Count;
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
                    await Task.Delay(500, _cts.Token);
                    continue;
                }

                if (!settings.AutoCapture)
                {
                    SetState(CaptureState.WaitingForFiveM);
                    await Task.Delay(1000, _cts.Token);
                    continue;
                }

                await _captureGate.WaitAsync(_cts.Token);
                try
                {
                    IReadOnlyList<string> current =
                        await _reader.ReadVisibleLinesAsync(_cts.Token);
                    await TryWriteRawSnapshotAsync(
                        current,
                        _reader.CurrentServer,
                        _cts.Token);
                    await HandleObservedServerCoreAsync(
                        _reader.CurrentServer,
                        settings,
                        _cts.Token);
                    _nuiUnavailableSince = null;
                    _disconnectedAt = null;
                    SetState(CaptureState.Capturing);
                    LastError = null;

                    await CaptureAvailableLinesAsync(
                        current,
                        settings,
                        _cts.Token);
                    await TryMarkRawSnapshotProcessedAsync(_cts.Token);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    await _reader.ResetAsync();
                    await HandleNuiUnavailableCoreAsync(
                        settings,
                        _cts.Token);
                }
                finally
                {
                    _captureGate.Release();
                }

                await Task.Delay(500, _cts.Token);
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
        IReadOnlyList<string> current,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        int overlap = FindOverlap(_previousVisible, current);
        int captured = 0;

        foreach (string line in current.Skip(overlap))
        {
            var entry = new ChatEntry(DateTime.Now, line);

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

            await _journal.AppendAsync(entry, cancellationToken);
            await TryAppendLastSessionCacheAsync(entry, cancellationToken);
            LastCaptureAt = DateTime.Now;
            MessageCaptured?.Invoke(this, entry);
            captured++;
        }

        _previousVisible = current.ToList();
        if (_journal.HasActiveSession)
        {
            await _journal.UpdateVisibleSnapshotAsync(
                _previousVisible,
                cancellationToken);
        }

        return captured;
    }

    private async Task HandleObservedServerCoreAsync(
        ServerSessionInfo observed,
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        if (_currentServer is null)
        {
            _currentServer = observed;
            NotifyServerChanged();
            return;
        }

        if (_currentServer.HasDifferentKnownAddress(observed))
        {
            await FinalizeCurrentServerCoreAsync(
                settings,
                DateTime.Now,
                false,
                cancellationToken);
            _currentServer = observed;
            NotifyServerChanged();
            return;
        }

        bool metadataImproved =
            (string.IsNullOrWhiteSpace(_currentServer.Address) &&
             !string.IsNullOrWhiteSpace(observed.Address)) ||
            (!_currentServer.HasFriendlyName && observed.HasFriendlyName);

        if (metadataImproved)
        {
            _currentServer = new ServerSessionInfo
            {
                Address = string.IsNullOrWhiteSpace(observed.Address)
                    ? _currentServer.Address
                    : observed.Address,
                Name = observed.HasFriendlyName
                    ? observed.Name
                    : _currentServer.Name
            };
            NotifyServerChanged();
        }
    }

    private async Task HandleFiveMAbsentAsync(AppSettings settings)
    {
        await _captureGate.WaitAsync(_cts.Token);
        try
        {
            await _reader.ResetAsync();
            _nuiUnavailableSince = null;

            if (_journal.HasActiveSession || _currentServer is not null)
            {
                await FinalizeCurrentServerCoreAsync(
                    settings,
                    DateTime.Now,
                    false,
                    _cts.Token);
            }

            SetState(GetDisconnectedIdleState(
                settings,
                CaptureState.WaitingForFiveM));
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
            DateTime.Now,
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

    private async Task TryWriteRawSnapshotAsync(
        IReadOnlyList<string> current,
        ServerSessionInfo server,
        CancellationToken cancellationToken)
    {
        try
        {
            await _rawCaptureFailsafe.WriteSnapshotAsync(
                current,
                server,
                cancellationToken);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to write the pre-parse raw capture failsafe.",
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
        int max = Math.Min(oldLines.Count, newLines.Count);

        for (int length = max; length > 0; length--)
        {
            bool same = true;
            for (int i = 0; i < length; i++)
            {
                if (!string.Equals(
                        oldLines[oldLines.Count - length + i],
                        newLines[i],
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

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_worker is not null)
        {
            try { await _worker; }
            catch { }
        }

        bool shutdownFinalized = false;

        try
        {
            await _captureGate.WaitAsync();
            try
            {
                if (_journal.HasActiveSession)
                {
                    ChatEntry? disconnectMarker =
                        await _journal.MarkDisconnectedAsync(
                            DateTime.Now,
                            CancellationToken.None);

                    if (disconnectMarker is not null)
                    {
                        await TryAppendLastSessionCacheAsync(
                            disconnectMarker,
                            CancellationToken.None);
                        MessageCaptured?.Invoke(this, disconnectMarker);
                    }
                }

                await _journal.FinalizeAsync(
                    _settings().ArchiveRoot,
                    CancellationToken.None);
                shutdownFinalized = true;
            }
            finally
            {
                _captureGate.Release();
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                "Unable to finalize the active chatlog during shutdown. The recovery copy was kept.",
                ex);
        }

        if (shutdownFinalized)
        {
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
        }

        await _reader.DisposeAsync();
        _captureGate.Dispose();
        _cts.Dispose();
    }
}
