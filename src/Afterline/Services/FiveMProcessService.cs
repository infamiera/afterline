using System.Diagnostics;

namespace Afterline.Services;

public static class FiveMProcessService
{
    private const long CacheDurationMilliseconds = 750;
    private static readonly object CacheGate = new();
    private static long _cachedAt = long.MinValue;
    private static bool _cachedResult;

    public static bool IsRunning()
    {
        long now = Environment.TickCount64;
        lock (CacheGate)
        {
            if (_cachedAt != long.MinValue && now - _cachedAt < CacheDurationMilliseconds)
                return _cachedResult;

            _cachedResult = DetectRunning();
            _cachedAt = now;
            return _cachedResult;
        }
    }

    public static bool Refresh()
    {
        lock (CacheGate)
        {
            _cachedAt = long.MinValue;
        }

        return IsRunning();
    }

    private static bool DetectRunning()
    {
        Process[] processes = Array.Empty<Process>();
        try
        {
            processes = Process.GetProcesses();
            foreach (Process process in processes)
            {
                try
                {
                    if (process.ProcessName.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // A process can exit between enumeration and inspection.
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to enumerate processes.", ex);
            return false;
        }
        finally
        {
            foreach (Process process in processes)
                process.Dispose();
        }
    }
}
