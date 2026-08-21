using System.Diagnostics;

namespace Afterline.Services;

public static class FiveMProcessService
{
    public static bool IsRunning()
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
            foreach (Process process in processes) process.Dispose();
        }
    }
}
