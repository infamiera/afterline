using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Afterline.Services;

public sealed class CanaryUpdateService
{
    public const string CanaryReleasePageUrl = "https://github.com/infamiera/afterline/releases/tag/canary";
    private const string CanaryReleaseApi = "https://api.github.com/repos/infamiera/afterline/releases/tags/canary";

    private static readonly Regex CanaryExeRegex = new(
        @"^Afterline-v(?<version>\d+\.\d+\.\d+)-Canary-(?<build>[0-9a-f]{40})-Windows-x64\.exe$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<CanaryUpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        using HttpClient http = CreateClient();
        try
        {
            using HttpResponseMessage response = await http.GetAsync(CanaryReleaseApi, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string responseError = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "No public Afterline Canary build is available yet."
                    : $"GitHub Releases returned {(int)response.StatusCode} while checking Canary.";
                return new CanaryUpdateCheckResult(
                    new UpdateCheckResult(null, CanaryReleasePageUrl, null, null, null, responseError),
                    null);
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
            string? exeName = null;
            foreach (string name in assetUrls.Keys)
            {
                Match match = CanaryExeRegex.Match(name);
                if (!match.Success) continue;
                latestVersion = match.Groups["version"].Value;
                buildId = match.Groups["build"].Value;
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

            return new CanaryUpdateCheckResult(
                new UpdateCheckResult(
                    latestVersion,
                    releasePage,
                    notes,
                    downloadUrl,
                    checksumUrl,
                    error),
                buildId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new CanaryUpdateCheckResult(
                new UpdateCheckResult(null, CanaryReleasePageUrl, null, null, null, "The Canary update check timed out."),
                null);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to query the latest Afterline Canary release.", ex);
            return new CanaryUpdateCheckResult(
                new UpdateCheckResult(null, CanaryReleasePageUrl, null, null, null, "Unable to contact GitHub Releases for Canary."),
                null);
        }
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { UseProxy = true };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        Version? version = typeof(CanaryUpdateService).Assembly.GetName().Version;
        string versionText = version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Afterline/{versionText}");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return http;
    }
}

public sealed record CanaryUpdateCheckResult(UpdateCheckResult Release, string? BuildId);
