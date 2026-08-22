using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Afterline.Services;

public sealed class UpdateService
{
    public const string ReleasesPageUrl = "https://github.com/infamiera/afterline/releases";
    private const string LatestReleaseApi = "https://api.github.com/repos/infamiera/afterline/releases/latest";

    private static readonly Regex Sha256Regex = new(
        @"(?<![0-9a-fA-F])(?<hash>[0-9a-fA-F]{64})(?![0-9a-fA-F])",
        RegexOptions.Compiled);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        using HttpClient http = CreateClient();
        try
        {
            using HttpResponseMessage response = await http.GetAsync(LatestReleaseApi, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    null,
                    ReleasesPageUrl,
                    null,
                    null,
                    null,
                    response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "No public Afterline release is available yet."
                        : $"GitHub Releases returned {(int)response.StatusCode}.");
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            string latestVersion = NormalizeVersion(root.TryGetProperty("tag_name", out JsonElement tag)
                ? tag.GetString() ?? string.Empty
                : string.Empty);
            if (string.IsNullOrWhiteSpace(latestVersion) || latestVersion == "0.0.0")
                return new UpdateCheckResult(null, ReleasesPageUrl, null, null, null, "The latest release tag could not be parsed.");

            string releasePage = root.TryGetProperty("html_url", out JsonElement page)
                ? page.GetString() ?? ReleasesPageUrl
                : ReleasesPageUrl;
            string notes = root.TryGetProperty("body", out JsonElement body)
                ? body.GetString() ?? string.Empty
                : string.Empty;

            string expectedExe = $"Afterline-v{latestVersion}-Windows-x64.exe";
            string expectedSha = expectedExe + ".sha256";
            string? downloadUrl = null;
            string? checksumUrl = null;

            if (root.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;
                    string? url = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                        ? urlElement.GetString()
                        : null;
                    if (string.Equals(name, expectedExe, StringComparison.OrdinalIgnoreCase))
                        downloadUrl = url;
                    else if (string.Equals(name, expectedSha, StringComparison.OrdinalIgnoreCase))
                        checksumUrl = url;
                }
            }

            string? error = null;
            if (string.IsNullOrWhiteSpace(downloadUrl) || string.IsNullOrWhiteSpace(checksumUrl))
                error = "The release exists, but its Windows executable or SHA-256 checksum asset is missing.";

            return new UpdateCheckResult(
                latestVersion,
                releasePage,
                notes,
                downloadUrl,
                checksumUrl,
                error);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(null, ReleasesPageUrl, null, null, null, "The update check timed out.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to query the latest Afterline release.", ex);
            return new UpdateCheckResult(null, ReleasesPageUrl, null, null, null, "Unable to contact GitHub Releases.");
        }
    }

    public async Task<UpdateDownloadResult> DownloadVerifiedAsync(
        UpdateCheckResult release,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(release.LatestVersion) ||
            string.IsNullOrWhiteSpace(release.DownloadUrl) ||
            string.IsNullOrWhiteSpace(release.ChecksumUrl))
            throw new InvalidOperationException("This release does not contain a complete self-update package.");

        Directory.CreateDirectory(AppPaths.UpdatesDirectory);
        CleanupStaleUpdateFiles();

        string finalName = $"Afterline-v{release.LatestVersion}-update.exe";
        string finalPath = Path.Combine(AppPaths.UpdatesDirectory, finalName);
        string temporaryPath = finalPath + ".download";
        DeleteIfExists(temporaryPath);

        using HttpClient http = CreateClient(TimeSpan.FromMinutes(5));
        try
        {
            using (HttpResponseMessage download = await http.GetAsync(
                       release.DownloadUrl,
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken))
            {
                download.EnsureSuccessStatusCode();
                await using Stream source = await download.Content.ReadAsStreamAsync(cancellationToken);
                await using FileStream target = new(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 128,
                    useAsync: true);
                await source.CopyToAsync(target, cancellationToken);
                await target.FlushAsync(cancellationToken);
            }

            string checksumText = await http.GetStringAsync(release.ChecksumUrl, cancellationToken);
            Match checksumMatch = Sha256Regex.Match(checksumText);
            if (!checksumMatch.Success)
                throw new InvalidDataException("The release checksum file did not contain a valid SHA-256 hash.");

            string expectedHash = checksumMatch.Groups["hash"].Value.ToUpperInvariant();
            string actualHash = await ComputeSha256Async(temporaryPath, cancellationToken);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded update failed SHA-256 verification and was discarded.");

            DeleteIfExists(finalPath);
            File.Move(temporaryPath, finalPath);
            return new UpdateDownloadResult(finalPath, actualHash, release.LatestVersion);
        }
        catch
        {
            DeleteIfExists(temporaryPath);
            throw;
        }
    }

    public static bool CanSelfUpdate(out string? reason)
    {
        reason = null;
        string? current = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(current))
        {
            reason = "Afterline could not determine the path of the running executable.";
            return false;
        }

        string? directory = Path.GetDirectoryName(current);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            reason = "Afterline could not determine its installation folder.";
            return false;
        }

        string testPath = Path.Combine(directory, $".afterline-write-test-{Environment.ProcessId}.tmp");
        try
        {
            using (FileStream stream = new(testPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                stream.WriteByte(0);
            File.Delete(testPath);
            return true;
        }
        catch (Exception ex)
        {
            DeleteIfExists(testPath);
            reason = "Afterline cannot replace itself in the current folder. Move Afterline.exe to a writable folder, or install the update manually from GitHub Releases. " + ex.Message;
            return false;
        }
    }

    public static void LaunchUpdater(UpdateDownloadResult download)
    {
        string? current = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(current))
            throw new InvalidOperationException("Afterline could not determine the running executable path.");

        var start = new ProcessStartInfo
        {
            FileName = download.FilePath,
            UseShellExecute = true,
            Arguments = $"--afterline-apply-update {Quote(current)} {Environment.ProcessId} {Quote(download.Version)}"
        };
        if (Process.Start(start) is null)
            throw new InvalidOperationException("The downloaded updater could not be started.");
    }

    public static bool TryRunUpdaterMode(string[] args)
    {
        int index = Array.FindIndex(args, value => string.Equals(value, "--afterline-apply-update", StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        if (args.Length <= index + 3)
        {
            ShowUpdaterError("The update command was incomplete.");
            return true;
        }

        string targetPath = args[index + 1];
        if (!int.TryParse(args[index + 2], out int parentPid)) parentPid = -1;
        string version = args[index + 3];
        string updaterPath = Environment.ProcessPath ?? string.Empty;
        string backupPath = targetPath + ".previous";

        try
        {
            WaitForProcessExit(parentPid, TimeSpan.FromSeconds(60));
            if (string.IsNullOrWhiteSpace(updaterPath) || !File.Exists(updaterPath))
                throw new FileNotFoundException("The downloaded update executable is unavailable.", updaterPath);

            DeleteIfExists(backupPath);
            if (File.Exists(targetPath))
                File.Copy(targetPath, backupPath, overwrite: true);

            File.Copy(updaterPath, targetPath, overwrite: true);
            string sourceHash = ComputeSha256(updaterPath);
            string targetHash = ComputeSha256(targetPath);
            if (!string.Equals(sourceHash, targetHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The installed update did not match the verified download.");

            if (Process.Start(new ProcessStartInfo
            {
                FileName = targetPath,
                UseShellExecute = true,
                Arguments = $"--afterline-update-complete {Quote(backupPath)} {Quote(version)}"
            }) is null)
                throw new InvalidOperationException("The updated Afterline executable could not be restarted.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Afterline self-update failed.", ex);
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, targetPath, overwrite: true);
                    Process.Start(new ProcessStartInfo { FileName = targetPath, UseShellExecute = true });
                }
            }
            catch (Exception rollbackEx)
            {
                DiagnosticLogger.Error("Afterline update rollback failed.", rollbackEx);
            }
            ShowUpdaterError("Afterline could not install the update. The previous executable was restored when possible.\n\n" + ex.Message);
        }
        return true;
    }

    public static void CleanupCompletedUpdate(string[] args)
    {
        int index = Array.FindIndex(args, value => string.Equals(value, "--afterline-update-complete", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || args.Length <= index + 1) return;
        string backupPath = args[index + 1];
        try { DeleteIfExists(backupPath); }
        catch (Exception ex) { DiagnosticLogger.Error("Unable to remove the previous Afterline executable after updating.", ex); }
        CleanupStaleUpdateFiles();
    }

    public static bool IsNewer(string candidate, string current)
    {
        if (!Version.TryParse(NormalizeVersion(candidate), out Version? candidateVersion)) return false;
        if (!Version.TryParse(NormalizeVersion(current), out Version? currentVersion)) return false;
        return candidateVersion > currentVersion;
    }

    private static HttpClient CreateClient(TimeSpan? timeout = null)
    {
        var handler = new HttpClientHandler { UseProxy = true };
        var http = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(8) };
        string version = typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Afterline/{version}");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return http;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, useAsync: true);
        using SHA256 sha = SHA256.Create();
        byte[] hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
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
        }
    }

    private static string NormalizeVersion(string value)
    {
        string trimmed = (value ?? string.Empty).Trim().TrimStart('v', 'V');
        string[] parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "0.0.0";
        return parts.Length switch
        {
            1 => trimmed + ".0.0",
            2 => trimmed + ".0",
            _ => string.Join('.', parts.Take(3))
        };
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static void CleanupStaleUpdateFiles()
    {
        try
        {
            if (!Directory.Exists(AppPaths.UpdatesDirectory)) return;
            foreach (string file in Directory.EnumerateFiles(AppPaths.UpdatesDirectory))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-7))
                        File.Delete(file);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
    }

    private static void ShowUpdaterError(string message)
    {
        try
        {
            System.Windows.MessageBox.Show(message, "Afterline Update", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        catch
        {
        }
    }
}

public sealed record UpdateCheckResult(
    string? LatestVersion,
    string ReleasePageUrl,
    string? ReleaseNotes,
    string? DownloadUrl,
    string? ChecksumUrl,
    string? Error);

public sealed record UpdateDownloadResult(string FilePath, string Sha256, string Version);
