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
    private readonly FiveMDevToolsChatReader _reader = new();
    private readonly SessionJournal _journal;
    private readonly Func<AppSettings> _settings;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private Task? _worker;
    private List<string> _previousVisible = new();
    private DateTime? _disconnectedAt;
    private bool _needsNewLoginMarker;

    public CaptureState State { get; private set; } = CaptureState.Stopped;
    public DateTime? LastCaptureAt { get; private set; }
    public string? LastError { get; private set; }

    public event EventHandler<ChatEntry>? MessageCaptured;
    public event EventHandler<CaptureState>? StateChanged;
    public event EventHandler<string>? SessionFinalized;

    public CaptureCoordinator(SessionJournal journal, Func<AppSettings> settings)
    {
        _journal = journal;
        _settings = settings;
    }

    public async Task StartAsync()
    {
        if (_worker is not null) return;
        _previousVisible = (await _journal.RecoverAsync(_settings().ArchiveRoot, _cts.Token)).ToList();
        _worker = Task.Run(WorkerAsync);
    }

    public async Task<int> ParseCurrentChatAsync()
    {
        if (!FiveMProcessService.IsRunning())
            throw new InvalidOperationException("FiveM is not currently running.");

        await _captureGate.WaitAsync(_cts.Token);
        try
        {
            AppSettings settings = _settings();
            IReadOnlyList<string> current = await _reader.ReadVisibleLinesAsync(_cts.Token);
            SetState(CaptureState.Capturing);
            LastError = null;
            return await CaptureAvailableLinesAsync(current, settings, _cts.Token);
        }
        finally
        {
            _captureGate.Release();
        }
    }

    public async Task FinishSessionAsync()
    {
        string? path = await _journal.FinalizeAsync(_settings().ArchiveRoot, _cts.Token);
        if (path is not null) SessionFinalized?.Invoke(this, path);
    }

    private async Task WorkerAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                AppSettings settings = _settings();
                bool fiveMRunning = settings.AutoDetectFiveM && FiveMProcessService.IsRunning();

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
                    IReadOnlyList<string> current = await _reader.ReadVisibleLinesAsync(_cts.Token);
                    SetState(CaptureState.Capturing);
                    LastError = null;
                    await CaptureAvailableLinesAsync(current, settings, _cts.Token);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    SetState(CaptureState.WaitingForNui);
                    await _reader.ResetAsync();
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
            bool hadActiveSession = _journal.HasActiveSession;

            ChatEntry? startMarker = await _journal.EnsureStartedAsync(
                settings.ArchiveRoot,
                entry.CapturedAt,
                cancellationToken);

            if (startMarker is not null)
                MessageCaptured?.Invoke(this, startMarker);
            else if (_needsNewLoginMarker && hadActiveSession)
            {
                ChatEntry? resumeMarker = await _journal.AppendLoginMarkerAsync(entry.CapturedAt, cancellationToken);
                if (resumeMarker is not null)
                    MessageCaptured?.Invoke(this, resumeMarker);
            }

            _needsNewLoginMarker = false;
            _disconnectedAt = null;

            await _journal.AppendAsync(entry, cancellationToken);
            LastCaptureAt = DateTime.Now;
            MessageCaptured?.Invoke(this, entry);
            captured++;
        }

        _previousVisible = current.ToList();
        if (_journal.HasActiveSession)
            await _journal.UpdateVisibleSnapshotAsync(_previousVisible, cancellationToken);

        return captured;
    }

    private async Task HandleFiveMAbsentAsync(AppSettings settings)
    {
        await _captureGate.WaitAsync(_cts.Token);
        try
        {
            await _reader.ResetAsync();

            if (!_journal.HasActiveSession)
            {
                SetState(CaptureState.WaitingForFiveM);
                return;
            }

            if (_disconnectedAt is null)
            {
                _disconnectedAt = DateTime.Now;
                _needsNewLoginMarker = true;

                ChatEntry? disconnectMarker = await _journal.MarkDisconnectedAsync(
                    _disconnectedAt.Value,
                    _cts.Token);
                if (disconnectMarker is not null)
                    MessageCaptured?.Invoke(this, disconnectMarker);
            }

            TimeSpan grace = TimeSpan.FromMinutes(Math.Max(0, settings.ReconnectGraceMinutes));
            if (DateTime.Now - _disconnectedAt.Value < grace)
            {
                SetState(CaptureState.ReconnectGrace);
                return;
            }

            string? path = await _journal.FinalizeAsync(settings.ArchiveRoot, _cts.Token);
            if (path is not null) SessionFinalized?.Invoke(this, path);
            _disconnectedAt = null;
            SetState(CaptureState.WaitingForFiveM);
        }
        finally
        {
            _captureGate.Release();
        }
    }

    private void SetState(CaptureState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static int FindOverlap(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        int max = Math.Min(oldLines.Count, newLines.Count);
        for (int length = max; length > 0; length--)
        {
            bool same = true;
            for (int i = 0; i < length; i++)
            {
                if (!string.Equals(oldLines[oldLines.Count - length + i], newLines[i], StringComparison.Ordinal))
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
            try { await _worker; } catch { }
        }

        try
        {
            await _journal.FinalizeAsync(_settings().ArchiveRoot, CancellationToken.None);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to finalize the active chatlog during shutdown. The recovery copy was kept.", ex);
        }

        await _reader.DisposeAsync();
        _captureGate.Dispose();
        _cts.Dispose();
    }
}
