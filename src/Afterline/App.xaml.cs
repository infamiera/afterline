using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Afterline.Services;

namespace Afterline;

public partial class App : System.Windows.Application
{
    private string[] _startupArgs = Array.Empty<string>();
    private DispatcherTimer? _completedUpdateCleanupTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        _startupArgs = e.Args.ToArray();
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        if (CanaryUpdateInstaller.TryRunUpdaterMode(e.Args) || UpdateService.TryRunUpdaterMode(e.Args))
        {
            Environment.Exit(Environment.ExitCode);
            return;
        }

        // Keep updater failures available to the still-installed build. Once a
        // newly installed build starts normally, it begins a clean diagnostic era.
        DiagnosticLogger.InitializeForCurrentBuild();
        CanaryUpdateInstaller.ReconcilePendingTransactionOnStartup();

        int canaryManifestSmokeIndex = Array.FindIndex(e.Args, value => string.Equals(
            value,
            "--afterline-smoke-canary-manifest",
            StringComparison.OrdinalIgnoreCase));
        if (canaryManifestSmokeIndex >= 0)
        {
            try
            {
                string manifestPath = e.Args.Length > canaryManifestSmokeIndex + 1
                    ? e.Args[canaryManifestSmokeIndex + 1]
                    : throw new ArgumentException("The Canary manifest smoke-test file is missing.");
                CanaryUpdateCheckResult parsed = CanaryUpdateService.ParseManifestForSmokeTest(
                    File.ReadAllText(manifestPath));

                if (!string.IsNullOrWhiteSpace(parsed.Release.Error) ||
                    parsed.BuildNumber is not int buildNumber ||
                    buildNumber <= 0 ||
                    string.IsNullOrWhiteSpace(parsed.BuildId) ||
                    string.IsNullOrWhiteSpace(parsed.Release.DownloadUrl) ||
                    string.IsNullOrWhiteSpace(parsed.Release.ChecksumUrl) ||
                    string.IsNullOrWhiteSpace(parsed.Release.PackageId))
                {
                    throw new InvalidDataException("The parsed Canary manifest was incomplete.");
                }

                if (!CanaryUpdateService.IsNewerBuild(
                        buildNumber,
                        parsed.BuildId,
                        buildNumber - 1,
                        $"{buildNumber - 1}.older") ||
                    CanaryUpdateService.IsNewerBuild(
                        buildNumber,
                        parsed.BuildId,
                        buildNumber,
                        parsed.BuildId) ||
                    CanaryUpdateService.IsNewerBuild(
                        buildNumber - 1,
                        $"{buildNumber - 1}.older",
                        buildNumber,
                        parsed.BuildId))
                {
                    throw new InvalidDataException("Canary build ordering failed its smoke test.");
                }

                string informational = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? string.Empty;
                string expectedInformational =
                    $"{parsed.Release.LatestVersion}-canary.{buildNumber}+{buildNumber}.{parsed.CommitSha}";
                if (!string.Equals(informational, expectedInformational, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"The executable identity '{informational}' did not match '{expectedInformational}'.");
                }

                DiagnosticLogger.Info("Canary update-manifest smoke test passed.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Canary update-manifest smoke test failed.", ex);
                Environment.Exit(1);
            }
            return;
        }

        int recoverySmokeIndex = Array.FindIndex(e.Args, value => string.Equals(
            value,
            "--afterline-smoke-session-recovery",
            StringComparison.OrdinalIgnoreCase));
        if (recoverySmokeIndex >= 0)
        {
            try
            {
                string archiveRoot = e.Args.Length > recoverySmokeIndex + 1
                    ? e.Args[recoverySmokeIndex + 1]
                    : throw new ArgumentException("The session-recovery smoke-test archive folder is missing.");
                // Run outside WPF's dispatcher synchronization context so the
                // file-I/O continuations used by the smoke test cannot deadlock
                // the startup thread.
                Task.Run(() => SessionRecoverySmokeTest.RunAsync(archiveRoot))
                    .GetAwaiter()
                    .GetResult();
                DiagnosticLogger.Info("Canary session-recovery smoke test passed.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Canary session-recovery smoke test failed.", ex);
                Environment.Exit(1);
            }
            return;
        }

        int archiveStressIndex = Array.FindIndex(e.Args, value => string.Equals(
            value,
            "--afterline-smoke-archive-stress",
            StringComparison.OrdinalIgnoreCase));
        if (archiveStressIndex >= 0)
        {
            try
            {
                string stressRoot = e.Args.Length > archiveStressIndex + 1
                    ? e.Args[archiveStressIndex + 1]
                    : throw new ArgumentException("The archive stress-test folder is missing.");
                Task.Run(() => ArchiveStressTest.RunAsync(stressRoot))
                    .GetAwaiter()
                    .GetResult();
                DiagnosticLogger.Info("Canary 10,000-chatlog archive stress test passed.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DiagnosticLogger.Error("Canary 10,000-chatlog archive stress test failed.", ex);
                Environment.Exit(1);
            }
            return;
        }

        ApplicationHealthMonitor.Start(Dispatcher);

        try
        {
            RetiredThemeGuard.EnsureUiFilter();
            var settings = new SettingsService().Load();
            ThemeService.Apply(settings.Theme);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to apply the saved theme during startup.", ex);
        }

        base.OnStartup(e);
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(TryInitializeMainWindowEnhancements));
    }

    internal void ConfirmHealthyStartup()
    {
        bool startedFromUpdate = _startupArgs.Any(value => string.Equals(
            value,
            "--afterline-update-complete",
            StringComparison.OrdinalIgnoreCase));
        if (_completedUpdateCleanupTimer is not null ||
            (!startedFromUpdate && !CanaryUpdateInstaller.HasPendingHealthyCleanup))
            return;

        _completedUpdateCleanupTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = TimeSpan.FromSeconds(45)
        };
        _completedUpdateCleanupTimer.Tick += (_, _) =>
        {
            _completedUpdateCleanupTimer.Stop();
            UpdateService.CleanupCompletedUpdate(_startupArgs);
            CanaryUpdateInstaller.CompletePendingHealthyTransaction();
            DiagnosticLogger.Info("Afterline remained healthy after updating; the previous executable backup was cleared.");
        };
        _completedUpdateCleanupTimer.Start();
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        DiagnosticLogger.Error(
            "Unhandled Afterline process exception. The previous update backup has been preserved when available.",
            e.ExceptionObject as Exception);
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DiagnosticLogger.Error(
            "Unhandled Afterline UI exception. The previous update backup has been preserved when available.",
            e.Exception);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ApplicationHealthMonitor.Stop();
        base.OnExit(e);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        TryInitializeMainWindowEnhancements();
    }

    private void TryInitializeMainWindowEnhancements()
    {
        if (MainWindow is not Afterline.MainWindow window) return;

        if (window.IsLoaded)
        {
            window.EnsureLiveChatEnhancements();
            return;
        }

        window.Loaded -= MainWindow_LoadedForEnhancements;
        window.Loaded += MainWindow_LoadedForEnhancements;
    }

    private void MainWindow_LoadedForEnhancements(object sender, RoutedEventArgs e)
    {
        if (sender is not Afterline.MainWindow window) return;
        window.Loaded -= MainWindow_LoadedForEnhancements;
        window.EnsureLiveChatEnhancements();
    }
}
