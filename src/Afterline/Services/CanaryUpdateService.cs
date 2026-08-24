using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Afterline.Services;

public sealed class CanaryUpdateService
{
    public const string CanaryReleasePageUrl = "https://github.com/infamiera/afterline/releases/tag/canary";
    private const string CanaryManifestUrl = "https://github.com/infamiera/afterline/releases/download/canary/afterline-canary-update.json";
    private const string CanaryAssetBaseUrl = "https://github.com/infamiera/afterline/releases/download/canary/";
    private const string CanaryReleaseApi = "https://api.github.com/repos/infamiera/afterline/releases/tags/canary";

    private static readonly Regex CanaryExeRegex = new(
        @"^Afterline-v(?<version>\d+\.\d+\.\d+)-Canary-(?<run>\d+)-(?<build>[0-9a-f]{7,40})-Windows-x64\.exe$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LegacyCanaryExeRegex = new(
        @"^Afterline-v(?<version>\d+\.\d+\.\d+)-Canary-(?<build>[0-9a-f]{40})-Windows-x64\.exe$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly SemaphoreSlim CheckGate = new(1, 1);
    private static readonly TimeSpan SuccessfulCacheLifetime = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan FailureCacheLifetime = TimeSpan.FromSeconds(15);
    private static CanaryUpdateCheckResult? _cachedResult;
    private static DateTimeOffset _cachedUntilUtc;

    public async Task<CanaryUpdateCheckResult> CheckAsync(
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        if (!forceRefresh && TryGetCached(out CanaryUpdateCheckResult? cached))
            return cached;

        await CheckGate.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && TryGetCached(out cached))
                return cached;

            CanaryUpdateCheckResult result;
            try
            {
                // Canary builds are intentionally discovered through a normal release
                // asset. Unlike api.github.com, this endpoint is not restricted to 60
                // unauthenticated requests per hour for every running copy of Afterline.
                result = await CheckManifestAsync(cancellationToken)
                    ?? await CheckLegacyApiAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = ErrorResult("The Canary update check timed out.");
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Unable to query the latest Afterline Canary release.", ex);
                result = ErrorResult("Unable to contact the Canary update service.");
            }

            Cache(result);
            return result;
        }
        finally
        {
            CheckGate.Release();
        }
    }

    public static CanaryUpdateCheckResult ParseManifestForSmokeTest(string json)
        => ParseManifest(json);

    public static bool IsNewerBuild(
        int? candidateBuildNumber,
        string? candidateBuildId,
        int? currentBuildNumber,
        string? currentBuildId)
    {
        if (candidateBuildNumber is int candidate && currentBuildNumber is int current)
            return candidate > current;

        if (string.IsNullOrWhiteSpace(candidateBuildId))
            return false;
        if (string.IsNullOrWhiteSpace(currentBuildId))
            return true;

        return !string.Equals(candidateBuildId, currentBuildId, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<CanaryUpdateCheckResult?> CheckManifestAsync(CancellationToken cancellationToken)
    {
        using HttpClient http = CreateClient(TimeSpan.FromSeconds(12));
        string cacheKey = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30).ToString();
        string requestUrl = CanaryManifestUrl + "?afterline-check=" + cacheKey;

        using HttpResponseMessage response = await http.GetAsync(requestUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
        {
            DiagnosticLogger.Info(
                $"Canary release manifest returned {(int)response.StatusCode}; trying the compatibility API lookup.");
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            return ParseManifest(json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or FormatException)
        {
            DiagnosticLogger.Error("The Canary release manifest was invalid; trying the compatibility API lookup.", ex);
            return null;
        }
    }

    private static CanaryUpdateCheckResult ParseManifest(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        int schemaVersion = ReadInt(root, "schemaVersion") ?? 0;
        string version = ReadString(root, "version");
        int? buildNumber = ReadInt(root, "buildNumber");
        string? commitSha = NormalizeCommitSha(ReadString(root, "commitSha"));
        string executable = ReadString(root, "executable");
        string checksum = ReadString(root, "checksum");
        string notes = ReadString(root, "releaseNotes");

        if (schemaVersion != 1)
            throw new InvalidDataException("The Canary manifest schema is unsupported.");
        if (string.IsNullOrWhiteSpace(version) || buildNumber is null || string.IsNullOrWhiteSpace(commitSha))
            throw new InvalidDataException("The Canary manifest does not contain a complete build identity.");

        Match executableMatch = CanaryExeRegex.Match(executable);
        if (!executableMatch.Success ||
            !string.Equals(executableMatch.Groups["version"].Value, version, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(executableMatch.Groups["run"].Value, out int assetBuildNumber) ||
            assetBuildNumber != buildNumber ||
            !commitSha.StartsWith(executableMatch.Groups["build"].Value, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The Canary executable does not match the manifest build identity.");
        }

        if (!string.Equals(checksum, executable + ".sha256", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The Canary checksum asset does not match the executable.");

        string buildId = $"{buildNumber}.{commitSha}";
        string encodedExecutable = Uri.EscapeDataString(executable);
        string encodedChecksum = Uri.EscapeDataString(checksum);
        string displayLabel = $"{version} Canary #{buildNumber} · {ShortSha(commitSha)}";

        return new CanaryUpdateCheckResult(
            new UpdateCheckResult(
                version,
                CanaryReleasePageUrl,
                notes,
                CanaryAssetBaseUrl + encodedExecutable,
                CanaryAssetBaseUrl + encodedChecksum,
                null,
                buildId),
            buildId,
            buildNumber,
            commitSha,
            displayLabel);
    }

    private static async Task<CanaryUpdateCheckResult> CheckLegacyApiAsync(CancellationToken cancellationToken)
    {
        using HttpClient http = CreateClient(TimeSpan.FromSeconds(8));
        using HttpResponseMessage response = await http.GetAsync(CanaryReleaseApi, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string responseError;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                responseError = "No public Afterline Canary build is available yet.";
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden &&
                     response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? remaining) &&
                     remaining.FirstOrDefault() == "0")
            {
                responseError = "GitHub temporarily rate-limited the compatibility update check. Select Retry in a moment.";
            }
            else
            {
                responseError = $"GitHub Releases returned {(int)response.StatusCode} while checking Canary.";
            }

            return ErrorResult(responseError);
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        string releasePage = root.TryGetProperty("html_url", out JsonElement page)
            ? page.GetString() ?? CanaryReleasePageUrl
            : CanaryReleasePageUrl;
        string notes = root.TryGetProperty("body", out JsonElement body)
            ? body.GetString() ?? string.Empty
            : string.Empty;
        string? releaseCommit = root.TryGetProperty("target_commitish", out JsonElement target)
            ? NormalizeCommitSha(target.GetString())
            : null;

        var assetUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url))
                    assetUrls[name] = url;
            }
        }

        string? latestVersion = null;
        string? buildId = null;
        int? buildNumber = null;
        string? commitSha = null;
        string? exeName = null;

        foreach (string name in assetUrls.Keys)
        {
            Match match = CanaryExeRegex.Match(name);
            if (match.Success)
            {
                latestVersion = match.Groups["version"].Value;
                string assetSha = match.Groups["build"].Value.ToLowerInvariant();
                commitSha = releaseCommit ?? assetSha;
                if (int.TryParse(match.Groups["run"].Value, out int parsedRun))
                    buildNumber = parsedRun;
                buildId = buildNumber is int identityRun
                    ? $"{identityRun}.{commitSha}"
                    : commitSha;
                exeName = name;
                break;
            }

            match = LegacyCanaryExeRegex.Match(name);
            if (!match.Success) continue;
            latestVersion = match.Groups["version"].Value;
            commitSha = releaseCommit ?? match.Groups["build"].Value.ToLowerInvariant();
            buildId = commitSha;
            exeName = name;
            break;
        }

        string? downloadUrl = exeName is not null && assetUrls.TryGetValue(exeName, out string? exeUrl)
            ? exeUrl
            : null;
        string checksumName = (exeName ?? string.Empty) + ".sha256";
        string? checksumUrl = exeName is not null && assetUrls.TryGetValue(checksumName, out string? shaUrl)
            ? shaUrl
            : null;

        string? error = null;
        if (string.IsNullOrWhiteSpace(latestVersion))
            error = "The Canary release exists, but its versioned Windows executable could not be found.";
        else if (string.IsNullOrWhiteSpace(downloadUrl) || string.IsNullOrWhiteSpace(checksumUrl))
            error = "The Canary release exists, but its Windows executable or SHA-256 checksum asset is missing.";
        else if (string.IsNullOrWhiteSpace(buildId))
            error = "The Canary release does not identify the source build it was created from.";

        string? displayLabel = latestVersion is null
            ? null
            : buildNumber is int displayRun
                ? $"{latestVersion} Canary #{displayRun} · {ShortSha(commitSha)}"
                : $"{latestVersion} Canary · {ShortSha(commitSha)}";

        return new CanaryUpdateCheckResult(
            new UpdateCheckResult(
                latestVersion,
                releasePage,
                notes,
                downloadUrl,
                checksumUrl,
                error,
                buildId),
            buildId,
            buildNumber,
            commitSha,
            displayLabel);
    }

    private static string ReadString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static int? ReadInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : null;

    private static string? NormalizeCommitSha(string? value)
    {
        string candidate = (value ?? string.Empty).Trim().ToLowerInvariant();
        return candidate.Length == 40 && candidate.All(Uri.IsHexDigit)
            ? candidate
            : null;
    }

    private static string ShortSha(string? sha)
        => string.IsNullOrWhiteSpace(sha)
            ? "unknown"
            : sha[..Math.Min(7, sha.Length)];

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var handler = new HttpClientHandler { UseProxy = true };
        var http = new HttpClient(handler) { Timeout = timeout };
        Version? version = typeof(CanaryUpdateService).Assembly.GetName().Version;
        string versionText = version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Afterline/{versionText}");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return http;
    }

    private static bool TryGetCached(out CanaryUpdateCheckResult? result)
    {
        result = _cachedResult;
        return result is not null && DateTimeOffset.UtcNow < _cachedUntilUtc;
    }

    private static void Cache(CanaryUpdateCheckResult result)
    {
        _cachedResult = result;
        _cachedUntilUtc = DateTimeOffset.UtcNow +
                          (string.IsNullOrWhiteSpace(result.Release.Error)
                              ? SuccessfulCacheLifetime
                              : FailureCacheLifetime);
    }

    private static CanaryUpdateCheckResult ErrorResult(string message)
        => new(
            new UpdateCheckResult(null, CanaryReleasePageUrl, null, null, null, message),
            null);
}

public sealed record CanaryUpdateCheckResult(
    UpdateCheckResult Release,
    string? BuildId,
    int? BuildNumber = null,
    string? CommitSha = null,
    string? DisplayLabel = null);
