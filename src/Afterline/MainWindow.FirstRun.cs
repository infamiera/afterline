using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _firstRunChecked;

    private void EnsureFirstRunSetup()
    {
        if (_firstRunChecked) return;
        _firstRunChecked = true;
        if (_settings.FirstRunCompleted) return;

        var welcome = new WelcomeWindow(this, _settings, _settingsService);
        if (welcome.ShowDialog() == true)
        {
            ThemeService.Apply(_settings.Theme);
            PopulateSettingsUi();
            _ = RefreshArchiveAsync();
        }
    }
}
