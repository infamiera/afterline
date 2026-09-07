using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Afterline.Services;

namespace Afterline;

internal sealed class ManualUpdateWindow : Window
{
    private readonly StackPanel _panel = new() { Margin = new Thickness(22) };
    private readonly TextBlock _status = new() { Text = "Loading current releases…", TextWrapping = TextWrapping.Wrap };
    private readonly Button _install = new() { Content = "Select downloaded .exe and install", IsEnabled = false, Margin = new Thickness(0, 16, 0, 8) };
    private readonly List<UpdateCheckResult> _releases = new();

    public ManualUpdateWindow(Window owner)
    {
        Owner = owner;
        Title = "Manual update";
        Width = 500;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        _panel.Children.Add(new TextBlock { Text = "Current Versions", FontSize = 20, Margin = new Thickness(0, 0, 0, 12) });
        AddLink("Stable", "https://github.com/infamiera/afterline/releases/latest");
        AddLink("Canary", "https://github.com/infamiera/afterline/releases/tag/canary");
        _panel.Children.Add(_install);
        _panel.Children.Add(_status);
        Content = _panel;
        ThemeService.ApplyWindow(this);
        Loaded += async (_, _) => await LoadReleasesAsync();
        _install.Click += async (_, _) => await InstallAsync();
    }

    private void AddLink(string label, string url)
    {
        var link = new Hyperlink(new Run(label)) { NavigateUri = new Uri(url) };
        link.SetResourceReference(Hyperlink.ForegroundProperty, "Accent");
        link.Click += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        var row = new TextBlock { Margin = new Thickness(0, 5, 0, 5) };
        row.Inlines.Add(link);
        _panel.Children.Insert(_panel.Children.Count > 2 ? _panel.Children.Count - 2 : _panel.Children.Count, row);
    }

    private async Task LoadReleasesAsync()
    {
        try
        {
            UpdateCheckResult stable = await new UpdateService().CheckAsync(CancellationToken.None);
            CanaryUpdateCheckResult canary = await new CanaryUpdateService().CheckAsync(CancellationToken.None, forceRefresh: true);
            foreach (var release in new[] { stable, canary.Release })
                if (release.Error is null && release.DownloadUrl is not null && release.ChecksumUrl is not null)
                    _releases.Add(release);
            foreach (var row in _panel.Children.OfType<TextBlock>().Where(row => row.Inlines.OfType<Hyperlink>().Any()).ToArray())
                _panel.Children.Remove(row);
            AddLink($"Stable: {stable.LatestVersion ?? "latest"}", stable.DownloadUrl ?? "https://github.com/infamiera/afterline/releases/latest");
            AddLink($"Canary (#{canary.BuildNumber?.ToString() ?? "unknown"})", canary.Release.DownloadUrl ?? "https://github.com/infamiera/afterline/releases/tag/canary");
            _install.IsEnabled = _releases.Count > 0;
            _status.Text = "Choose an official current Stable or Canary executable. Its checksum will be verified before installation.";
        }
        catch (Exception ex) { _status.Text = "Unable to verify current releases: " + ex.Message; }
    }

    private async Task InstallAsync()
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Afterline executable (*.exe)|*.exe" };
        if (picker.ShowDialog(this) != true) return;
        _install.IsEnabled = false;
        string? staged = null;
        try
        {
            if (!UpdateService.CanSelfUpdate(out string? reason)) throw new InvalidOperationException(reason);
            Directory.CreateDirectory(AppPaths.UpdatesDirectory);
            staged = Path.Combine(AppPaths.UpdatesDirectory, $"manual-{Guid.NewGuid():N}.exe");
            File.Copy(picker.FileName, staged);
            string hash;
            await using (var stream = File.OpenRead(staged))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream));
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            UpdateCheckResult? matched = null;
            foreach (var release in _releases)
            {
                string checksum = await http.GetStringAsync(release.ChecksumUrl!);
                Match match = Regex.Match(checksum, @"\A\s*([0-9a-fA-F]{64})(?:\s|$)");
                if (match.Success && string.Equals(match.Groups[1].Value, hash, StringComparison.OrdinalIgnoreCase))
                { matched = release; break; }
            }
            if (matched is null) throw new InvalidDataException("This file does not match a current official release. Download it again using the links above.");
            if (MessageBox.Show(this, "Install this verified Afterline release and restart? Save any Editor work first.", "Confirm update", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            CanaryUpdateInstaller.LaunchUpdater(new UpdateDownloadResult(staged, hash, matched.LatestVersion!));
            staged = null;
            if (Owner is MainWindow main) main.PrepareManualUpdateExit();
            Application.Current.Shutdown();
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally
        {
            if (staged is not null) { try { File.Delete(staged); } catch { } }
            _install.IsEnabled = true;
        }
    }
}
