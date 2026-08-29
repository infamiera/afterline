using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace Afterline.Services;

public static class ApplicationHealthMonitor
{
    private sealed class RunState
    {
        public RunState() { }
        public string Build { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime LastUiResponsiveUtc { get; set; }
        public DateTime? UiDelayStartedUtc { get; set; }
        public double LongestUiDelayMilliseconds { get; set; }
        public bool UiFreezeEscalated { get; set; }
    }

    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DelayThreshold = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FreezeThreshold = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan HeartbeatPersistenceInterval = TimeSpan.FromSeconds(15);
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static CancellationTokenSource? _watchdogCancellation;
    private static Task? _watchdog;
    private static RunState? _currentRunState;
    private static string? _runStatePathOverride;
    private static DateTime _lastHeartbeatPersistedUtc = DateTime.MinValue;
    private static int _started;
    private static string RunStatePath => _runStatePathOverride ?? AppPaths.DiagnosticRunState;

    public static void Start(Dispatcher dispatcher)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;

        string build = GetCurrentBuild();
        try
        {
            AppPaths.EnsureLocalDirectories();
            RunState? previous = ReadRunState();
            if (previous is not null &&
                previous.ProcessId != Environment.ProcessId &&
                !IsProcessRunning(previous.ProcessId))
            {
                ReportInterruptedRun(previous);
            }

            DateTime startedUtc = DateTime.UtcNow;
            var currentState = new RunState
            {
                Build = build,
                ProcessId = Environment.ProcessId,
                StartedUtc = startedUtc,
                LastUiResponsiveUtc = startedUtc
            };
            _currentRunState = currentState;
            _lastHeartbeatPersistedUtc = startedUtc;
            WriteRunState(currentState);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Warn($"Unable to initialize crash-state monitoring: {ex.Message}");
        }

        _watchdogCancellation = new CancellationTokenSource();
        _watchdog = Task.Run(() => WatchUiAsync(dispatcher, _watchdogCancellation.Token));
    }

    public static void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0) return;

        _watchdogCancellation?.Cancel();
        try
        {
            lock (Gate)
            {
                _currentRunState = null;
                RunState? state = ReadRunStateCore();
                if (state?.ProcessId == Environment.ProcessId && File.Exists(RunStatePath))
                    File.Delete(RunStatePath);
            }
        }
        catch
        {
            // A stale state is safe: the next startup verifies the process ID and
            // reports the interrupted run instead of risking a shutdown failure.
        }
        finally
        {
            _watchdogCancellation?.Dispose();
            _watchdogCancellation = null;
            _watchdog = null;
        }
    }

    private static async Task WatchUiAsync(Dispatcher dispatcher, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(ProbeInterval, cancellationToken).ConfigureAwait(false);

                var responsive = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                DateTime probeStartedUtc = DateTime.UtcNow;
                var probeStopwatch = Stopwatch.StartNew();
                try
                {
                    _ = dispatcher.BeginInvoke(
                        DispatcherPriority.Send,
                        new Action(() => responsive.TrySetResult(true)));
                }
                catch (InvalidOperationException) when (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    return;
                }

                bool delayReported = false;
                bool freezeReported = false;
                TimeSpan nextIncidentCheckpoint = DelayThreshold + TimeSpan.FromSeconds(5);
                while (!responsive.Task.IsCompleted)
                {
                    await Task.WhenAny(
                            responsive.Task,
                            Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken))
                        .ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    TimeSpan elapsed = probeStopwatch.Elapsed;
                    if (!delayReported && elapsed >= DelayThreshold)
                    {
                        delayReported = true;
                        RecordUiDelayStarted(probeStartedUtc, elapsed);
                        DiagnosticLogger.Error(
                            $"Afterline UI delay detected: the interface did not respond for " +
                            $"{elapsed.TotalSeconds:N1} seconds. Background capture monitoring remained active.");
                    }

                    if (!freezeReported && elapsed >= FreezeThreshold)
                    {
                        freezeReported = true;
                        RecordUiFreezeEscalated(elapsed);
                        DiagnosticLogger.Error(
                            $"Afterline UI freeze detected: the interface remained unresponsive for " +
                            $"{elapsed.TotalSeconds:N1} seconds. The incident state was saved for recovery on the next launch.");
                    }

                    if (delayReported && elapsed >= nextIncidentCheckpoint)
                    {
                        RecordUiDelayProgress(elapsed, freezeReported);
                        nextIncidentCheckpoint += TimeSpan.FromSeconds(5);
                    }
                }

                TimeSpan totalDelay = probeStopwatch.Elapsed;
                RecordUiResponsive(DateTime.UtcNow, totalDelay, delayReported);
                if (delayReported)
                {
                    DiagnosticLogger.Error(
                        $"Afterline UI responsiveness recovered after {totalDelay.TotalSeconds:N1} seconds" +
                        (freezeReported ? " following a recorded freeze." : "."));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Warn($"Application hang monitoring stopped: {ex.Message}");
        }
    }

    private static void ReportInterruptedRun(RunState previous)
        => DiagnosticLogger.Error(BuildInterruptedRunMessage(previous));

    private static string BuildInterruptedRunMessage(RunState previous)
    {
        string build = string.IsNullOrWhiteSpace(previous.Build) ? "unknown build" : previous.Build;
        string lastResponse = previous.LastUiResponsiveUtc == default
            ? "no UI heartbeat was recorded"
            : $"the last confirmed UI response was {previous.LastUiResponsiveUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

        if (previous.UiDelayStartedUtc is DateTime delayStartedUtc)
        {
            return
                $"Recovered an unfinished UI {(previous.UiFreezeEscalated ? "freeze" : "delay")} from the previous {build} session. " +
                $"It began at {delayStartedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}; {lastResponse}; " +
                $"the longest measured delay was {previous.LongestUiDelayMilliseconds / 1000:N1} seconds. " +
                "The process ended before responsiveness recovery was recorded.";
        }

        return
            $"Afterline's previous {build} run did not shut down cleanly; {lastResponse}. " +
            "The application may have crashed, been force-closed, or been terminated by Windows.";
    }

    internal static void RunPersistenceSmokeTest(string testRoot)
    {
        string statePath = Path.Combine(testRoot, "health-monitor", "run-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        lock (Gate)
        {
            if (_started != 0)
                throw new InvalidOperationException("The health-monitor persistence smoke test requires an inactive watchdog.");

            _runStatePathOverride = statePath;
            try
            {
                DateTime startedUtc = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
                var expected = new RunState
                {
                    Build = "health-smoke-build",
                    ProcessId = 424242,
                    StartedUtc = startedUtc,
                    LastUiResponsiveUtc = startedUtc.AddSeconds(4),
                    UiDelayStartedUtc = startedUtc.AddSeconds(5),
                    LongestUiDelayMilliseconds = 18_500,
                    UiFreezeEscalated = true
                };
                WriteRunStateCore(expected);
                RunState recovered = ReadRunStateCore()
                    ?? throw new InvalidOperationException("The persisted health incident could not be restored.");
                string report = BuildInterruptedRunMessage(recovered);
                if (recovered.UiDelayStartedUtc != expected.UiDelayStartedUtc ||
                    !recovered.UiFreezeEscalated ||
                    Math.Abs(recovered.LongestUiDelayMilliseconds - 18_500) > 0.1 ||
                    !report.Contains("unfinished UI freeze", StringComparison.Ordinal) ||
                    !report.Contains("longest measured delay", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The persisted health incident did not retain its delay and freeze diagnostics.");
                }
            }
            finally
            {
                try { if (File.Exists(statePath)) File.Delete(statePath); }
                catch { }
                _runStatePathOverride = null;
            }
        }
    }

    private static void RecordUiDelayStarted(DateTime startedUtc, TimeSpan elapsed)
    {
        UpdateRunState(state =>
        {
            state.UiDelayStartedUtc = startedUtc;
            state.LongestUiDelayMilliseconds = Math.Max(
                state.LongestUiDelayMilliseconds,
                elapsed.TotalMilliseconds);
        });
    }

    private static void RecordUiFreezeEscalated(TimeSpan elapsed)
    {
        UpdateRunState(state =>
        {
            state.UiFreezeEscalated = true;
            state.LongestUiDelayMilliseconds = Math.Max(
                state.LongestUiDelayMilliseconds,
                elapsed.TotalMilliseconds);
        });
    }

    private static void RecordUiDelayProgress(TimeSpan elapsed, bool freezeEscalated)
    {
        UpdateRunState(state =>
        {
            state.UiFreezeEscalated |= freezeEscalated;
            state.LongestUiDelayMilliseconds = Math.Max(
                state.LongestUiDelayMilliseconds,
                elapsed.TotalMilliseconds);
        });
    }

    private static void RecordUiResponsive(DateTime respondedUtc, TimeSpan delay, bool persistImmediately)
    {
        lock (Gate)
        {
            RunState? state = _currentRunState;
            if (state?.ProcessId != Environment.ProcessId) return;

            state.LastUiResponsiveUtc = respondedUtc;
            state.LongestUiDelayMilliseconds = Math.Max(
                state.LongestUiDelayMilliseconds,
                delay.TotalMilliseconds);
            state.UiDelayStartedUtc = null;
            state.UiFreezeEscalated = false;

            if (!persistImmediately &&
                respondedUtc - _lastHeartbeatPersistedUtc < HeartbeatPersistenceInterval)
            {
                return;
            }

            WriteRunStateCore(state);
            _lastHeartbeatPersistedUtc = respondedUtc;
        }
    }

    private static void UpdateRunState(Action<RunState> update)
    {
        lock (Gate)
        {
            RunState? state = _currentRunState;
            if (state?.ProcessId != Environment.ProcessId) return;
            update(state);
            WriteRunStateCore(state);
        }
    }

    private static RunState? ReadRunState()
    {
        lock (Gate)
            return ReadRunStateCore();
    }

    private static RunState? ReadRunStateCore()
    {
        if (!File.Exists(RunStatePath)) return null;
        try
        {
            return JsonSerializer.Deserialize<RunState>(
                File.ReadAllText(RunStatePath),
                JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteRunState(RunState state)
    {
        lock (Gate)
            WriteRunStateCore(state);
    }

    private static void WriteRunStateCore(RunState state)
    {
        string temp = RunStatePath + $".{Environment.ProcessId}.tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions), new UTF8Encoding(false));
            File.Move(temp, RunStatePath, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { }
        }
    }

    private static bool IsProcessRunning(int processId)
    {
        if (processId <= 0) return false;
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string GetCurrentBuild()
        => Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
}
