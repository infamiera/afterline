using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
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
        @"^(?<version>\d+\.\d+\.\d+)-canary\.(?<run>\d+)\+(?:(?<metaRun>\d+)\.)?(?<sha>[0-9a-f]{7,40})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private bool _buildIdentityV065Initialized;
    private readonly DispatcherTimer _buildIdentityRefreshTimerV065 = new()
    {
        Interval = TimeSpan.FromSeconds(30)
    };
    private bool _buildIdentityRefreshBusyV065;
    private UpdateCheckResult? _availableReleaseV065;
    private string? _availableBuildIdV065;
    private string _currentBuildDisplayV065 = string.Empty;

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
            if (!_buildIdentityRefreshBusyV065)
                await RefreshIdentifiedUpdateStateV065Async();
        };

        // V4 starts one legacy refresh during initialization. Wait for that task to
        // finish, then keep the V3 refresh path disabled so it cannot overwrite the
        // build-number-aware labels introduced here.
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            for (int i = 0; i < 120 && _updateRefreshBusyCanaryV3; i++)
                await Task.Delay(100);

            _updateRefreshBusyCanaryV3 = true;
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

    private async Task RefreshIdentifiedUpdateStateV065Async()
    {
        if (_buildIdentityRefreshBusyV065 || _updateInstallInProgress)
            return;

        _buildIdentityRefreshBusyV065 = true;
        SetUpdateActionStateCanaryV3("Checking…", false);

        try
        {
            if (IsCanaryChannelV062())
            {
                CanaryRuntimeIdentityV065 current = GetCurrentCanaryIdentityV065();
                _currentBuildDisplayV065 = current.DisplayLabel;

                Task<CanaryUpdateCheckResult> canaryTask = _canaryUpdateServiceV062.CheckAsync(CancellationToken.None);
                Task<UpdateCheckResult> stableTask = _updateService.CheckAsync(CancellationToken.None);
                await Task.WhenAll(canaryTask, stableTask);

                CanaryUpdateCheckResult canary = await canaryTask;
                UpdateCheckResult stable = await stableTask;
                UpdateCheckResult release = canary.Release;

                string latestCanary = canary.DisplayLabel
                    ?? (string.IsNullOrWhiteSpace(release.LatestVersion)
                        ? "Unavailable"
                        : release.LatestVersion + " Canary");
                string latestDisplay = string.IsNullOrWhiteSpace(stable.Error) && !string.IsNullOrWhiteSpace(stable.LatestVersion)
                    ? $"{latestCanary} · Stable {stable.LatestVersion}"
                    : latestCanary;

                SetUpdateBuildLines(current.DisplayLabel, latestDisplay);

                if (!string.IsNullOrWhiteSpace(release.Error) ||
                    string.IsNullOrWhiteSpace(release.LatestVersion) ||
                    string.IsNullOrWhiteSpace(canary.BuildId))
                {
                    _availableReleaseV065 = null;
                    _availableBuildIdV065 = null;
                    SetUpdateActionStateCanaryV3("Unavailable", false);
                    return;
                }

                bool available = string.IsNullOrWhiteSpace(current.BuildId) ||
                                 !string.Equals(current.BuildId, canary.BuildId, StringComparison.OrdinalIgnoreCase);
                if (available)
                {
                    _availableReleaseV065 = release;
                    _availableBuildIdV065 = canary.BuildId;
                    SetUpdateActionStateCanaryV3("Update", true);
                }
                else
                {
                    _availableReleaseV065 = null;
                    _availableBuildIdV065 = null;
                    SetUpdateActionStateCanaryV3("Up to date", false);
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
            SetUpdateActionStateCanaryV3(stableAvailable ? "Update" : "Up to date", stableAvailable);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Build-identity update refresh failed.", ex);
            _availableReleaseV065 = null;
            _availableBuildIdV065 = null;
            string current = string.IsNullOrWhiteSpace(_currentBuildDisplayV065)
                ? GetCurrentBuildVersion()
                : _currentBuildDisplayV065;
            SetUpdateBuildLines(current, "Unavailable");
            SetUpdateActionStateCanaryV3("Unavailable", false);
        }
        finally
        {
            _buildIdentityRefreshBusyV065 = false;
        }
    }

    private async void InstallIdentifiedUpdateV065_Click(object sender, RoutedEventArgs e)
    {
        if (_availableReleaseV065 is null || _updateInstallInProgress)
            return;

        UpdateCheckResult release = _availableReleaseV065;
        string current = string.IsNullOrWhiteSpace(_currentBuildDisplayV065)
            ? GetCurrentBuildVersion()
            : _currentBuildDisplayV065;

        var window = new UpdateAvailableWindow(this, current, release);
        if (window.ShowDialog() != true || !window.InstallRequested)
            return;

        if (IsCanaryChannelV062() && !string.IsNullOrWhiteSpace(_availableBuildIdV065))
        {
            _settings.UpdateChannel = "Canary";
            _settings.InstalledCanaryBuild = _availableBuildIdV065;
            _settingsService.Save(_settings);
        }

        bool launched = await InstallAvailableReleaseWithResilientHandoffCanaryV4(release);
        if (!launched)
            await RefreshIdentifiedUpdateStateV065Async();
    }

    private static string ShortShaV065(string? sha)
        => string.IsNullOrWhiteSpace(sha)
            ? "unknown"
            : sha[..Math.Min(7, sha.Length)];
}
