using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Afterline.Services;

public sealed class UpdateService
{
    public const string WorkflowPageUrl = "https://github.com/infamiera/afterline/actions/workflows/windows-build.yml";

    private const string LatestSuccessfulRunApi =
        "https://api.github.com/repos/infamiera/afterline/actions/workflows/windows-build.yml/runs?branch=main&status=success&per_page=1";

    private static readonly Regex VersionRegex = new(
        @"<Version>\s*(?<version>[^<]+)\s*</Version>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler { UseProxy = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Afterline/0.2.5");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        string? token = Environment.GetEnvironmentVariable("AFTERLINE_GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

        try
        {
            using HttpResponseMessage runResponse = await http.GetAsync(LatestSuccessfulRunApi, cancellationToken);
            if (!runResponse.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(
                    null,
                    WorkflowPageUrl,
                    runResponse.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Automatic update checking is unavailable while the update repository is private."
                        : $"Update service returned {(int)runResponse.StatusCode}.");
            }

            string runJson = await runResponse.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument runDoc = JsonDocument.Parse(runJson);
            if (!runDoc.RootElement.TryGetProperty("workflow_runs", out JsonElement runs) ||
                runs.ValueKind != JsonValueKind.Array ||
                runs.GetArrayLength() == 0)
                return new UpdateCheckResult(null, WorkflowPageUrl, "No successful Windows builds were found.");

            JsonElement run = runs[0];
            string? headSha = run.TryGetProperty("head_sha", out JsonElement shaElement)
                ? shaElement.GetString()
                : null;
            string buildUrl = run.TryGetProperty("html_url", out JsonElement urlElement)
                ? urlElement.GetString() ?? WorkflowPageUrl
                : WorkflowPageUrl;

            if (string.IsNullOrWhiteSpace(headSha))
                return new UpdateCheckResult(null, buildUrl, "The latest build did not expose a commit SHA.");

            string projectUrl =
                $"https://api.github.com/repos/infamiera/afterline/contents/src/Afterline/Afterline.csproj?ref={Uri.EscapeDataString(headSha)}";
            using HttpResponseMessage projectResponse = await http.GetAsync(projectUrl, cancellationToken);
            if (!projectResponse.IsSuccessStatusCode)
                return new UpdateCheckResult(null, buildUrl, "The latest build version could not be read.");

            string projectJson = await projectResponse.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument projectDoc = JsonDocument.Parse(projectJson);
            if (!projectDoc.RootElement.TryGetProperty("content", out JsonElement contentElement))
                return new UpdateCheckResult(null, buildUrl, "The latest build version metadata was missing.");

            string? encoded = contentElement.GetString()?.Replace("\n", string.Empty).Replace("\r", string.Empty);
            if (string.IsNullOrWhiteSpace(encoded))
                return new UpdateCheckResult(null, buildUrl, "The latest build version metadata was empty.");

            string projectText = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            Match match = VersionRegex.Match(projectText);
            if (!match.Success)
                return new UpdateCheckResult(null, buildUrl, "The latest build version could not be parsed.");

            string latestVersion = NormalizeVersion(match.Groups["version"].Value);
            return new UpdateCheckResult(latestVersion, buildUrl, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(null, WorkflowPageUrl, "The update check timed out.");
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to query the latest Afterline build.", ex);
            return new UpdateCheckResult(null, WorkflowPageUrl, "Unable to contact the update service.");
        }
    }

    public static bool IsNewer(string candidate, string current)
    {
        if (!Version.TryParse(NormalizeVersion(candidate), out Version? candidateVersion)) return false;
        if (!Version.TryParse(NormalizeVersion(current), out Version? currentVersion)) return false;
        return candidateVersion > currentVersion;
    }

    private static string NormalizeVersion(string value)
    {
        string trimmed = value.Trim().TrimStart('v', 'V');
        string[] parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => trimmed + ".0.0",
            2 => trimmed + ".0",
            _ => string.Join('.', parts.Take(3))
        };
    }
}

public sealed record UpdateCheckResult(string? LatestVersion, string? BuildUrl, string? Error);
