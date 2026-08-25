using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private sealed record CanaryRuntimeIdentityV065(
        string Version,
        int? BuildNumber,
        string? CommitSha,
        string? BuildId,
        string DisplayLabel);

    private static readonly Regex CanaryInformationalVersionV065 = new(
        @"^(?<version>\d+\.\d+\.\d+)-canary\.(?<run>\d+)\+(?:(?<metaRun>\d+)\.)?(?<sha>[0-9a-f]{7,40})(?:\.(?<sourceSha>[0-9a-f]{7,40}))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private bool _buildIdentityV065Initialized;
    private readonly DispatcherTimer _buildIdentityRefreshTimerV065 = new()
    {
        Interval = TimeSpan.FromMinutes(10)
    };
    private bool _buildIdentityRefreshBusyV065;
    private DateTimeOffset _lastBuildIdentityRefreshUtcV065 = DateTimeOffset.MinValue;
    private UpdateCheckResult? _availableReleaseV065;
    private string? _availableBuildIdV065;
    private string _currentBuildDisplayV065 = string.Empty;
    private string? _dismissedUpdateAttentionV076;

    private void EnsureBuildIdentityV065()
    {
        if (_buildIdentityV065Initialized || _checkUpdatesButton is null)
            return;

        _buildIdentityV065Initialized = true;

        _updateRefreshTimerCanaryV3.Stop();
        _checkUpdatesButton.Click -= InstallAvailableUpdateCanaryV4_Click;
        _checkUpdatesButton.Click -= InstallAvailableUpdateCanaryV3_Click;
        _checkUpdatesButton.Click -= ChannelAwareCheckForUpdatesV062_Click;
        _checkUpdatesButton.Click -= CheckForUpdates_Click;
        _checkUpdatesButton.Click += InstallIdentifiedUpdateV065_Click;

        _buildIdentityRefreshTimerV065.Tick += async (_, _) => await RefreshIdentifiedUpdateStateV065Async();
        _buildIdentityRefreshTimerV065.Start();

        Activated += async (_, _) =>
        {
            if (!_buildIdentityRefreshBusyV065 &&
                DateTimeOffset.UtcNow - _lastBuildIdentityRefreshUtcV065 >= TimeSpan.FromMinutes(2))
                await RefreshIdentifiedUpdateStateV065Async();
        };

        // Run one authoritative startup check after all update controls have finished
        // initializing. Legacy startup polling is disabled by the final initialization
        // path, so it cannot race this result or consume GitHub's public API allowance.
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            await RefreshIdentifiedUpdateStateV065Async();
        }), DispatcherPriority.ContextIdle);
    }

    private CanaryRuntimeIdentityV065 GetCurrentCanaryIdentityV065()
    {
        string version = GetCurrentBuildVersion();
        string informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;

        Match match = CanaryInformationalVersionV065.Match(informational.Trim());
        if (!match.Success)
        {
            string? fallback = _settings.InstalledCanaryBuild;
            return new CanaryRuntimeIdentityV065(
                version,
                null,
                null,
                fallback,
                $"{version} Canary");
        }

        int? run = int.TryParse(match.Groups["run"].Value, out int parsedRun)
            ? parsedRun
            : null;
        string sha = match.Groups["sha"].Value.ToLowerInvariant();
        bool usesNewIdentity = match.Groups["metaRun"].Success;
        string buildId = usesNewIdentity && run is int buildNumber
            ? $"{buildNumber}.{sha}"
            : sha;
        string display = run is int displayRun
            ? $"{version} Canary #{displayRun} · {ShortShaV065(sha)}"
            : $"{version} Canary · {ShortShaV065(sha)}";

        return new CanaryRuntimeIdentityV065(version, run, sha, buildId, display);
    }

    private async Task RefreshIdentifiedUpdateStateV065Async(bool forceRefresh = false)
    {
        if (_buildIdentityRefreshBusyV065 || _updateInstallInProgress)
            return;

        _buildIdentityRefreshBusyV065 = true;
        _lastBuildIdentityRefreshUtcV065 = DateTimeOffset.UtcNow;
        SetUpdateActionStateCanaryV3("Checking…", false);

        try
        {
            if (IsCanaryChannelV062())
            {
                CanaryRuntimeIdentityV065 current = GetCurrentCanaryIdentityV065();
                _currentBuildDisplayV065 = current.DisplayLabel;

                CanaryUpdateCheckResult canary = await _canaryUpdateServiceV062.CheckAsync(
                    CancellationToken.None,
                    forceRefresh);
                UpdateCheckResult release = canary.Release;

                string latestCanary = canary.DisplayLabel
                    ?? (string.IsNullOrWhiteSpace(release.LatestVersion)
                        ? "Unavailable"
                        : release.LatestVersion + " Canary");
                SetUpdateBuildLines(current.DisplayLabel, latestCanary);

                if (!string.IsNullOrWhiteSpace(release.Error) ||
                    string.IsNullOrWhiteSpace(release.LatestVersion) ||
                    string.IsNullOrWhiteSpace(canary.BuildId))
                {
                    _availableReleaseV065 = null;
                    _availableBuildIdV065 = null;
                    SetUpdateAttentionV076(null);
                    SetUpdateActionStateCanaryV3("Retry", true);
                    return;
                }

                bool available = CanaryUpdateService.IsNewerBuild(
                    canary.BuildNumber,
                    canary.BuildId,
                    current.BuildNumber,
                    current.BuildId);
                if (available)
                {
                    _availableReleaseV065 = release;
                    _availableBuildIdV065 = canary.BuildId;
                    SetUpdateAttentionV076(canary.BuildId);
                    SetUpdateActionStateCanaryV3("Update", true);
                }
                else
                {
                    _availableReleaseV065 = null;
                    _availableBuildIdV065 = null;
                    SetUpdateAttentionV076(null);
                    SetUpdateActionStateCanaryV3("Check again", true);
                }
                return;
            }

            string currentVersion = GetCurrentBuildVersion();
            _currentBuildDisplayV065 = currentVersion + " Stable";
            UpdateCheckResult stableRelease = await _updateService.CheckAsync(CancellationToken.None);
            string latestStable = string.IsNullOrWhiteSpace(stableRelease.LatestVersion)
                ? "Unavailable"
                : stableRelease.LatestVersion + " Stable";
            SetUpdateBuildLines(_currentBuildDisplayV065, latestStable);

            bool stableAvailable = string.IsNullOrWhiteSpace(stableRelease.Error) &&
                                   !string.IsNullOrWhiteSpace(stableRelease.LatestVersion) &&
                                   UpdateService.IsNewer(stableRelease.LatestVersion, currentVersion);
            _availableReleaseV065 = stableAvailable ? stableRelease : null;
            _availableBuildIdV065 = null;
            SetUpdateAttentionV076(stableAvailable ? stableRelease.LatestVersion : null);
            SetUpdateActionStateCanaryV3(stableAvailable ? "Update" : "Check again", true);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Build-identity update refresh failed.", ex);
            _availableReleaseV065 = null;
            _availableBuildIdV065 = null;
            SetUpdateAttentionV076(null);
            string current = string.IsNullOrWhiteSpace(_currentBuildDisplayV065)
                ? GetCurrentBuildVersion()
                : _currentBuildDisplayV065;
            SetUpdateBuildLines(current, "Unavailable");
            SetUpdateActionStateCanaryV3("Retry", true);
        }
        finally
        {
            _buildIdentityRefreshBusyV065 = false;
        }
    }

    private async void InstallIdentifiedUpdateV065_Click(object sender, RoutedEventArgs e)
    {
        if (_updateInstallInProgress || _buildIdentityRefreshBusyV065)
            return;

        if (_availableReleaseV065 is null)
        {
            await RefreshIdentifiedUpdateStateV065Async(forceRefresh: true);
            if (_availableReleaseV065 is null)
                return;
        }

        string? availableIdentity = _availableBuildIdV065 ?? _availableReleaseV065.LatestVersion;
        _dismissedUpdateAttentionV076 = availableIdentity;
        SetUpdateAttentionV076(availableIdentity);

        UpdateCheckResult release = _availableReleaseV065;
        string current = string.IsNullOrWhiteSpace(_currentBuildDisplayV065)
            ? GetCurrentBuildVersion()
            : _currentBuildDisplayV065;

        var window = new UpdateAvailableWindow(this, current, release);
        if (window.ShowDialog() != true || !window.InstallRequested)
            return;

        bool launched = await InstallAvailableReleaseWithResilientHandoffCanaryV4(release);
        if (!launched)
            await RefreshIdentifiedUpdateStateV065Async();
    }

    private static string ShortShaV065(string? sha)
        => string.IsNullOrWhiteSpace(sha)
            ? "unknown"
            : sha[..Math.Min(7, sha.Length)];

    private void SetUpdateAttentionV076(string? availableIdentity)
    {
        if (_checkUpdatesButton is null) return;
        bool highlight = !string.IsNullOrWhiteSpace(availableIdentity) &&
                         !string.Equals(availableIdentity, _dismissedUpdateAttentionV076, StringComparison.OrdinalIgnoreCase);
        if (!highlight)
        {
            _checkUpdatesButton.ClearValue(Control.BackgroundProperty);
            _checkUpdatesButton.ClearValue(Control.BorderBrushProperty);
            _checkUpdatesButton.ClearValue(Control.BorderThicknessProperty);
            return;
        }

        _checkUpdatesButton.SetResourceReference(Control.BackgroundProperty, "AfterlineControlHover");
        _checkUpdatesButton.SetResourceReference(Control.BorderBrushProperty, "Accent");
        _checkUpdatesButton.BorderThickness = new Thickness(2);
    }
}
