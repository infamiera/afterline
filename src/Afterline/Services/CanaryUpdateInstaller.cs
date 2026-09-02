using System.Diagnostics;
using System.Text.Json;

namespace Afterline.Services;

public static class CanaryUpdateInstaller
{
    private const string LegacyApplySwitch = "--afterline-apply-update";
    private const string HelperApplySwitch = "--afterline-apply-update-from";
    private const string TransactionSmokeSwitch = "--afterline-smoke-update-transaction";
    private static readonly TimeSpan FileRetryWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RestartRetryWindow = TimeSpan.FromSeconds(12);
    private static string? _pendingHealthyBackupPath;
    private static string? _pendingHealthyStagePath;

    private sealed record UpdateTransactionJournal(
        string TargetPath,
        string BackupPath,
        string StagePath,
        string SourceHash,
        string PreviousHash,
        string Version,
        string State,
        DateTimeOffset UpdatedUtc);

    private sealed record ReplacementResult(
        string BackupPath,
        string StagePath,
        string SourceHash,
        string PreviousHash);

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
        int smokeIndex = Array.FindIndex(args,
            value => string.Equals(value, TransactionSmokeSwitch, StringComparison.OrdinalIgnoreCase));
        if (smokeIndex >= 0)
        {
            try
            {
                string root = args.Length > smokeIndex + 1
                    ? args[smokeIndex + 1]
                    : throw new ArgumentException("The update-transaction smoke folder is missing.");
                RunTransactionSmokeTest(root);
                DiagnosticLogger.Info("Canary updater transaction smoke test passed.");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Canary updater transaction smoke test failed.", ex);
                Environment.ExitCode = 1;
            }
            return true;
        }

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

        // Backward compatibility for older Stable builds that launch the downloaded
        // executable itself as the update helper.
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

    public static void ReconcilePendingTransactionOnStartup()
    {
        UpdateTransactionJournal? journal = TryReadJournal();
        if (journal is null) return;

        string? currentPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentPath) || !PathsEqual(currentPath, journal.TargetPath))
        {
            DiagnosticLogger.Info("A pending update transaction belongs to a different executable path; it was left untouched.");
            return;
        }

        if (FileHashMatches(journal.TargetPath, journal.SourceHash))
        {
            _pendingHealthyBackupPath = journal.BackupPath;
            _pendingHealthyStagePath = journal.StagePath;
            DiagnosticLogger.Info("A verified interrupted update was recovered and is awaiting the healthy-startup check.");
            return;
        }

        if (FileHashMatches(journal.TargetPath, journal.PreviousHash))
        {
            CleanupTransactionFiles(journal, deleteBackup: true);
            DiagnosticLogger.Info("A failed update transaction left the previous executable intact and was cleared safely.");
            return;
        }

        DiagnosticLogger.Error(
            "A pending update transaction could not match the installed executable to either verified hash.",
            new InvalidDataException($"Update transaction state: {journal.State}."));
    }

    public static bool HasPendingHealthyCleanup
        => !string.IsNullOrWhiteSpace(_pendingHealthyBackupPath);

    public static void CompletePendingHealthyTransaction()
    {
        UpdateTransactionJournal? journal = TryReadJournal();
        DeleteIfExists(_pendingHealthyBackupPath ?? journal?.BackupPath);
        DeleteIfExists(_pendingHealthyStagePath ?? journal?.StagePath);
        DeleteIfExists(AppPaths.UpdateTransactionFile);
        DeleteIfExists(AppPaths.UpdateTransactionTemporaryFile);
        _pendingHealthyBackupPath = null;
        _pendingHealthyStagePath = null;
    }

    private static void ApplyUpdate(string sourcePath, string targetPath, int parentPid, string version)
    {
        bool replacementVerified = false;
        bool ownsUpdateMutex = false;
        using var updateMutex = new Mutex(initiallyOwned: false, name: "Local\\Afterline.Update.Transaction");

        try
        {
            try { ownsUpdateMutex = updateMutex.WaitOne(TimeSpan.FromSeconds(45)); }
            catch (AbandonedMutexException) { ownsUpdateMutex = true; }
            if (!ownsUpdateMutex)
                throw new IOException("Another Afterline update is already in progress. Wait a moment and select Update again.");

            WaitForProcessExit(parentPid, TimeSpan.FromSeconds(60));

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("The verified update executable is unavailable.", sourcePath);
            if (string.IsNullOrWhiteSpace(targetPath))
                throw new InvalidOperationException("Afterline could not determine which executable should be updated.");

            WaitForOtherTargetProcesses(targetPath, FileRetryWindow);
            ReplacementResult replacement = ReplaceExecutableTransactionally(
                sourcePath,
                targetPath,
                version,
                FileRetryWindow);
            replacementVerified = true;

            string restartArguments = string.IsNullOrWhiteSpace(replacement.BackupPath)
                ? string.Empty
                : $"--afterline-update-complete {Quote(replacement.BackupPath)} {Quote(version)}";
            bool restarted = TryStartWithRetry(targetPath, restartArguments, RestartRetryWindow);

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
            UpdateTransactionJournal? journal = TryReadJournal();

            if (!replacementVerified && journal is not null &&
                FileHashMatches(journal.TargetPath, journal.SourceHash))
            {
                replacementVerified = true;
                _pendingHealthyBackupPath = journal.BackupPath;
                _pendingHealthyStagePath = journal.StagePath;
            }

            if (replacementVerified)
            {
                _ = TryStartWithRetry(targetPath, string.Empty, TimeSpan.FromSeconds(6));
                ShowUpdaterWarning(
                    "The update is installed and its hash is valid, but Afterline could not restart automatically.\n\n" +
                    $"Please start Afterline manually from:\n{targetPath}\n\n{ex.Message}");
                return;
            }

            // Replacement cannot begin until the journal is written successfully.
            // With no journal, staging failed early and the installed copy is intact.
            bool unchanged = journal is null || FileHashMatches(journal.TargetPath, journal.PreviousHash);
            bool restored = unchanged || (journal is not null && TryRestorePreviousExecutable(journal));

            if (restored && journal is not null)
                CleanupTransactionFiles(journal, deleteBackup: true);

            string recoveryText = unchanged
                ? "The installed executable was never replaced and remains intact."
                : restored
                    ? "The previous executable was restored and verified."
                    : "Afterline could not verify either the installed executable or its backup.";

            if (restored)
                _ = TryStartWithRetry(targetPath, string.Empty, TimeSpan.FromSeconds(6));

            ShowUpdaterError("Afterline could not install the update. " + recoveryText + "\n\n" + ex.Message);
        }
        finally
        {
            if (ownsUpdateMutex) updateMutex.ReleaseMutex();
        }
    }

    private static ReplacementResult ReplaceExecutableTransactionally(
        string sourcePath,
        string targetPath,
        string version,
        TimeSpan retryWindow)
    {
        string directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("Afterline could not determine its installation folder.");
        Directory.CreateDirectory(directory);

        string identity = Guid.NewGuid().ToString("N");
        string stagePath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.update-{identity}.tmp");
        string backupPath = targetPath + $".previous-{identity}";
        string sourceHash = RetryFileOperation(
            () => ComputeSha256(sourcePath),
            retryWindow,
            "The verified update could not be reopened for installation.");
        string previousHash = RetryFileOperation(
            () => ComputeSha256(targetPath),
            retryWindow,
            "The installed Afterline executable could not be read before updating.");
        if (string.Equals(sourceHash, previousHash, StringComparison.OrdinalIgnoreCase))
        {
            DiagnosticLogger.Info("The requested update was already installed; replacement was skipped.");
            return new ReplacementResult(string.Empty, string.Empty, sourceHash, previousHash);
        }

        RetryFileOperation(
            () => File.Copy(sourcePath, stagePath, overwrite: false),
            retryWindow,
            "The verified update could not be staged beside Afterline.exe.");

        string stageHash = RetryFileOperation(
            () => ComputeSha256(stagePath),
            retryWindow,
            "The staged update could not be verified.");
        if (!string.Equals(sourceHash, stageHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The staged update did not match the verified download.");

        var journal = new UpdateTransactionJournal(
            targetPath,
            backupPath,
            stagePath,
            sourceHash,
            previousHash,
            version,
            "Prepared",
            DateTimeOffset.UtcNow);
        WriteJournal(journal);
        DiagnosticLogger.Info("Updater transaction prepared and both executable hashes were verified.");

        RetryFileOperation(
            () => File.Replace(stagePath, targetPath, backupPath, ignoreMetadataErrors: true),
            retryWindow,
            "Windows kept Afterline.exe locked. The existing executable was left unchanged.");

        journal = journal with { State = "Replaced", UpdatedUtc = DateTimeOffset.UtcNow };
        TryWriteJournal(journal);

        string targetHash = RetryFileOperation(
            () => ComputeSha256(targetPath),
            retryWindow,
            "The installed update could not be reopened for verification.");
        if (!string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The installed update did not match the verified staged file.");

        journal = journal with { State = "Verified", UpdatedUtc = DateTimeOffset.UtcNow };
        TryWriteJournal(journal);
        DiagnosticLogger.Info("Updater transaction replaced Afterline.exe atomically and verified the installed hash.");
        return new ReplacementResult(backupPath, stagePath, sourceHash, previousHash);
    }

    private static bool TryRestorePreviousExecutable(UpdateTransactionJournal journal)
    {
        try
        {
            if (FileHashMatches(journal.TargetPath, journal.PreviousHash)) return true;
            if (!File.Exists(journal.BackupPath)) return false;

            string displacedPath = journal.StagePath + ".failed";
            DeleteIfExists(displacedPath);
            if (File.Exists(journal.TargetPath))
            {
                RetryFileOperation(
                    () => File.Replace(journal.BackupPath, journal.TargetPath, displacedPath, ignoreMetadataErrors: true),
                    FileRetryWindow,
                    "The previous Afterline executable could not be restored atomically.");
                DeleteIfExists(displacedPath);
            }
            else
            {
                RetryFileOperation(
                    () => File.Move(journal.BackupPath, journal.TargetPath),
                    FileRetryWindow,
                    "The previous Afterline executable could not be restored.");
            }

            return FileHashMatches(journal.TargetPath, journal.PreviousHash);
        }
        catch (Exception rollbackEx)
        {
            DiagnosticLogger.Error("Afterline update rollback failed.", rollbackEx);
            return false;
        }
    }

    private static void WaitForOtherTargetProcesses(string targetPath, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        IReadOnlyList<int> blockers;
        do
        {
            blockers = FindTargetProcessIds(targetPath);
            if (blockers.Count == 0) return;
            Thread.Sleep(300);
        }
        while (DateTime.UtcNow < deadline);

        throw new IOException(
            $"Another Afterline process is still using this executable (PID {string.Join(", ", blockers)}). " +
            "Close every Afterline window and select Update again.");
    }

    private static IReadOnlyList<int> FindTargetProcessIds(string targetPath)
    {
        var blockers = new List<int>();
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                try
                {
                    string? candidate = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(candidate) && PathsEqual(candidate, targetPath))
                        blockers.Add(process.Id);
                }
                catch
                {
                    // Never classify an inaccessible unrelated process by name alone.
                }
            }
        }
        return blockers;
    }

    private static void RunTransactionSmokeTest(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);
        string targetPath = Path.Combine(root, "Afterline-smoke-target.exe");
        string sourcePath = Path.Combine(root, "Afterline-smoke-source.exe");
        byte[] previous = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
        byte[] update = Enumerable.Range(0, 4096).Select(i => (byte)((i * 7 + 19) % 253)).ToArray();
        File.WriteAllBytes(targetPath, previous);
        File.WriteAllBytes(sourcePath, update);
        string previousHash = ComputeSha256(targetPath);
        string updateHash = ComputeSha256(sourcePath);

        bool lockRejected = false;
        using (new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try
            {
                _ = ReplaceExecutableTransactionally(
                    sourcePath,
                    targetPath,
                    "smoke-locked",
                    TimeSpan.FromSeconds(1));
            }
            catch (IOException)
            {
                lockRejected = true;
            }
        }

        UpdateTransactionJournal lockedJournal = TryReadJournal()
            ?? throw new InvalidOperationException("The locked update did not leave a recoverable transaction journal.");
        if (!lockRejected || !FileHashMatches(targetPath, previousHash))
            throw new InvalidOperationException("A locked update modified the existing executable.");
        CleanupTransactionFiles(lockedJournal, deleteBackup: true);

        ReplacementResult installed = ReplaceExecutableTransactionally(
            sourcePath,
            targetPath,
            "smoke-success",
            TimeSpan.FromSeconds(5));
        if (!FileHashMatches(targetPath, updateHash) || !FileHashMatches(installed.BackupPath, previousHash))
            throw new InvalidOperationException("The atomic update did not preserve verified new and previous executables.");

        UpdateTransactionJournal installedJournal = TryReadJournal()
            ?? throw new InvalidOperationException("The installed update did not retain its transaction journal.");
        if (!TryRestorePreviousExecutable(installedJournal) || !FileHashMatches(targetPath, previousHash))
            throw new InvalidOperationException("The updater rollback did not restore the verified previous executable.");
        CleanupTransactionFiles(installedJournal, deleteBackup: true);
        DeleteIfExists(targetPath);
        DeleteIfExists(sourcePath);
        Directory.Delete(root, recursive: true);
    }

    private static T RetryFileOperation<T>(Func<T> operation, TimeSpan timeout, string failureMessage)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        do
        {
            try { return operation(); }
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
        => RetryFileOperation(() => { operation(); return true; }, timeout, failureMessage);

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
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static bool FileHashMatches(string path, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expectedHash) || !File.Exists(path))
            return false;
        try
        {
            string actual = RetryFileOperation(
                () => ComputeSha256(path),
                TimeSpan.FromSeconds(3),
                "The executable hash could not be read.");
            return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
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

    private static void WriteJournal(UpdateTransactionJournal journal)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.UpdateTransactionFile)!);
        string json = JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.UpdateTransactionTemporaryFile, json);
        File.Move(AppPaths.UpdateTransactionTemporaryFile, AppPaths.UpdateTransactionFile, overwrite: true);
    }

    private static void TryWriteJournal(UpdateTransactionJournal journal)
    {
        try { WriteJournal(journal); }
        catch (Exception ex) { DiagnosticLogger.Error("Unable to advance the update transaction journal.", ex); }
    }

    private static UpdateTransactionJournal? TryReadJournal()
    {
        try
        {
            if (!File.Exists(AppPaths.UpdateTransactionFile)) return null;
            return JsonSerializer.Deserialize<UpdateTransactionJournal>(File.ReadAllText(AppPaths.UpdateTransactionFile));
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to read the pending update transaction journal.", ex);
            return null;
        }
    }

    private static void CleanupTransactionFiles(UpdateTransactionJournal journal, bool deleteBackup)
    {
        DeleteIfExists(journal.StagePath);
        DeleteIfExists(journal.StagePath + ".failed");
        if (deleteBackup) DeleteIfExists(journal.BackupPath);
        DeleteIfExists(AppPaths.UpdateTransactionFile);
        DeleteIfExists(AppPaths.UpdateTransactionTemporaryFile);
    }

    private static bool PathsEqual(string first, string second)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return string.Equals(first, second, StringComparison.OrdinalIgnoreCase); }
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

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
        catch { }
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
        catch { }
    }
}
