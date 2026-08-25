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
    }

    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static CancellationTokenSource? _watchdogCancellation;
    private static Task? _watchdog;
    private static int _started;

    public static void Start(Dispatcher dispatcher)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;

        string build = GetCurrentBuild();
        try
        {
            AppPaths.EnsureLocalDirectories();
            RunState? previous = ReadRunState();
            if (previous is not null &&
                string.Equals(previous.Build, build, StringComparison.Ordinal) &&
                previous.ProcessId != Environment.ProcessId &&
                !IsProcessRunning(previous.ProcessId))
            {
                DiagnosticLogger.Error(
                    $"Afterline's previous {build} run did not shut down cleanly. " +
                    "The application may have crashed, been force-closed, or been terminated by Windows.");
            }

            WriteRunState(new RunState
            {
                Build = build,
                ProcessId = Environment.ProcessId,
                StartedUtc = DateTime.UtcNow
            });
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
                RunState? state = ReadRunStateCore();
                if (state?.ProcessId == Environment.ProcessId && File.Exists(AppPaths.DiagnosticRunState))
                    File.Delete(AppPaths.DiagnosticRunState);
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
        bool hangReported = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

                var responsive = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

                Task timeout = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                Task completed = await Task.WhenAny(responsive.Task, timeout).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (completed == responsive.Task)
                {
                    hangReported = false;
                    continue;
                }

                if (!hangReported)
                {
                    DiagnosticLogger.Error(
                        "Afterline's interface was unresponsive for at least 15 seconds. " +
                        "This may indicate a blocked operation or application hang.");
                    hangReported = true;
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

    private static RunState? ReadRunState()
    {
        lock (Gate)
            return ReadRunStateCore();
    }

    private static RunState? ReadRunStateCore()
    {
        if (!File.Exists(AppPaths.DiagnosticRunState)) return null;
        try
        {
            return JsonSerializer.Deserialize<RunState>(
                File.ReadAllText(AppPaths.DiagnosticRunState),
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
        {
            string temp = AppPaths.DiagnosticRunState + $".{Environment.ProcessId}.tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions), new UTF8Encoding(false));
                File.Move(temp, AppPaths.DiagnosticRunState, true);
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { }
            }
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
