using System.Diagnostics;

namespace Afterline.Services;

public static class CanaryUpdateInstaller
{
    private const string LegacyApplySwitch = "--afterline-apply-update";
    private const string HelperApplySwitch = "--afterline-apply-update-from";
    private static readonly TimeSpan FileRetryWindow = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RestartRetryWindow = TimeSpan.FromSeconds(12);

    public static void LaunchUpdater(UpdateDownloadResult download)
    {
        string? targetPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
            throw new InvalidOperationException("Afterline could not determine the running executable path.");
        if (!File.Exists(download.FilePath))
            throw new FileNotFoundException("The verified update executable is unavailable.", download.FilePath);

        Directory.CreateDirectory(AppPaths.UpdatesDirectory);
        string helperPath = Path.Combine(
            AppPaths.UpdatesDirectory,
            $"Afterline-update-helper-{Environment.ProcessId}-{Guid.NewGuid():N}.exe");

        RetryFileOperation(
            () => File.Copy(targetPath, helperPath, overwrite: true),
            FileRetryWindow,
            "Afterline could not prepare its detached updater helper.");

        var start = new ProcessStartInfo
        {
            FileName = helperPath,
            UseShellExecute = true,
            Arguments = $"{HelperApplySwitch} {Quote(download.FilePath)} {Quote(targetPath)} {Environment.ProcessId} {Quote(download.Version)}"
        };

        if (Process.Start(start) is null)
            throw new InvalidOperationException("The detached Afterline updater could not be started.");
    }

    public static bool TryRunUpdaterMode(string[] args)
    {
        int helperIndex = Array.FindIndex(args,
            value => string.Equals(value, HelperApplySwitch, StringComparison.OrdinalIgnoreCase));
        if (helperIndex >= 0)
        {
            if (args.Length <= helperIndex + 4)
            {
                ShowUpdaterError("The update command was incomplete.");
                return true;
            }

            string sourcePath = args[helperIndex + 1];
            string targetPath = args[helperIndex + 2];
            if (!int.TryParse(args[helperIndex + 3], out int parentPid)) parentPid = -1;
            string version = args[helperIndex + 4];
            ApplyUpdate(sourcePath, targetPath, parentPid, version);
            return true;
        }

        // Backward compatibility: Stable 0.6.4 and earlier launch the downloaded
        // update executable itself with this command. A new Canary binary can
        // therefore repair the handoff before it replaces the older Stable copy.
        int legacyIndex = Array.FindIndex(args,
            value => string.Equals(value, LegacyApplySwitch, StringComparison.OrdinalIgnoreCase));
        if (legacyIndex < 0) return false;

        if (args.Length <= legacyIndex + 3)
        {
            ShowUpdaterError("The update command was incomplete.");
            return true;
        }

        string updaterPath = Environment.ProcessPath ?? string.Empty;
        string legacyTarget = args[legacyIndex + 1];
        if (!int.TryParse(args[legacyIndex + 2], out int legacyParentPid)) legacyParentPid = -1;
        string legacyVersion = args[legacyIndex + 3];
        ApplyUpdate(updaterPath, legacyTarget, legacyParentPid, legacyVersion);
        return true;
    }

    private static void ApplyUpdate(string sourcePath, string targetPath, int parentPid, string version)
    {
        string backupPath = targetPath + ".previous";
        bool replacementVerified = false;

        try
        {
            WaitForProcessExit(parentPid, TimeSpan.FromSeconds(60));

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("The verified update executable is unavailable.", sourcePath);
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new InvalidOperationException("Afterline could not determine which executable should be updated.");

            string sourceHash = RetryFileOperation(
                () => ComputeSha256(sourcePath),
                FileRetryWindow,
                "The verified update could not be reopened for installation.");

            RetryFileOperation(
                () => DeleteIfExists(backupPath),
                FileRetryWindow,
                "The previous update backup could not be cleared.");

            if (File.Exists(targetPath))
            {
                RetryFileOperation(
                    () => File.Copy(targetPath, backupPath, overwrite: true),
                    FileRetryWindow,
                    "Afterline could not back up the currently installed executable.");
            }

            RetryFileOperation(
                () => File.Copy(sourcePath, targetPath, overwrite: true),
                FileRetryWindow,
                "Windows kept the Afterline executable locked while installing the update.");

            string targetHash = RetryFileOperation(
                () => ComputeSha256(targetPath),
                FileRetryWindow,
                "Windows kept the newly installed Afterline executable locked while verifying it.");

            if (!string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The installed update did not match the verified download.");

            replacementVerified = true;

            bool restarted = TryStartWithRetry(
                targetPath,
                $"--afterline-update-complete {Quote(backupPath)} {Quote(version)}",
                RestartRetryWindow);

            if (!restarted)
            {
                ShowUpdaterWarning(
                    "The update was installed and verified successfully, but Windows would not restart Afterline automatically.\n\n" +
                    $"Please start Afterline manually from:\n{targetPath}");
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Afterline self-update failed.", ex);

            if (!replacementVerified)
            {
                bool restored = TryRestorePreviousExecutable(backupPath, targetPath);
                string restoredText = restored
                    ? "The previous executable was restored."
                    : "Afterline could not confirm that the previous executable was restored.";

                ShowUpdaterError(
                    "Afterline could not install the update. " + restoredText + "\n\n" + ex.Message);
            }
            else
            {
                // A verified replacement is already on disk. Never roll it back merely
                // because Windows, antivirus software, or Explorer delayed the restart.
                ShowUpdaterWarning(
                    "The update was installed and verified, but Afterline could not restart automatically.\n\n" +
                    $"Please start Afterline manually from:\n{targetPath}\n\n{ex.Message}");
            }
        }
    }

    private static bool TryRestorePreviousExecutable(string backupPath, string targetPath)
    {
        try
        {
            if (!File.Exists(backupPath)) return false;

            RetryFileOperation(
                () => File.Copy(backupPath, targetPath, overwrite: true),
                FileRetryWindow,
                "The previous Afterline executable could not be restored.");

            _ = TryStartWithRetry(targetPath, string.Empty, TimeSpan.FromSeconds(6));
            return true;
        }
        catch (Exception rollbackEx)
        {
            DiagnosticLogger.Error("Afterline update rollback failed.", rollbackEx);
            return false;
        }
    }

    private static T RetryFileOperation<T>(Func<T> operation, TimeSpan timeout, string failureMessage)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        Exception? last = null;

        do
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (IsTransientFileException(ex))
            {
                last = ex;
                Thread.Sleep(250);
            }
        }
        while (DateTime.UtcNow < deadline);

        throw new IOException(failureMessage, last);
    }

    private static void RetryFileOperation(Action operation, TimeSpan timeout, string failureMessage)
        => RetryFileOperation(
            () =>
            {
                operation();
                return true;
            },
            timeout,
            failureMessage);

    private static bool IsTransientFileException(Exception ex)
        => ex is IOException or UnauthorizedAccessException;

    private static bool TryStartWithRetry(string executable, string arguments, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        do
        {
            try
            {
                Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true,
                    Arguments = arguments
                });
                if (process is not null) return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                DiagnosticLogger.Info($"Afterline restart was temporarily blocked: {ex.Message}");
            }

            Thread.Sleep(300);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static void WaitForProcessExit(int pid, TimeSpan timeout)
    {
        if (pid <= 0) return;
        try
        {
            using Process process = Process.GetProcessById(pid);
            if (!process.WaitForExit((int)Math.Clamp(timeout.TotalMilliseconds, 1000, int.MaxValue)))
                throw new TimeoutException("The previous Afterline process did not close in time.");
        }
        catch (ArgumentException)
        {
            // It exited before we queried it.
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            File.Delete(path);
    }

    private static string Quote(string value)
        => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static void ShowUpdaterError(string message)
    {
        try
        {
            System.Windows.MessageBox.Show(
                message,
                "Afterline Update",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch
        {
        }
    }

    private static void ShowUpdaterWarning(string message)
    {
        try
        {
            System.Windows.MessageBox.Show(
                message,
                "Afterline Update",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch
        {
        }
    }
}
