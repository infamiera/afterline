using System.Text.Json;
using Afterline.Models;

namespace Afterline.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        AppPaths.EnsureLocalDirectories();
        try
        {
            // Only a genuinely new installation should see the first-run setup.
            // Existing settings files that predate FirstRunCompleted deserialize with
            // AppSettings' default value of true and are therefore left alone.
            if (!File.Exists(AppPaths.SettingsFile))
                return new AppSettings { FirstRunCompleted = false };

            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), _jsonOptions)
                                   ?? new AppSettings();

            // Frost is retired because some native/WPF controls can become unreadable
            // under the light palette. Existing Frost users are returned to the default
            // theme automatically so they never reopen into a broken interface.
            if (RetiredThemeGuard.IsRetiredFrost(settings.Theme))
            {
                settings.Theme = ThemeService.CreateDefault();
                try
                {
                    Save(settings);
                }
                catch (Exception ex)
                {
                    DiagnosticLogger.Error("Unable to persist the retired Frost theme migration.", ex);
                }
            }

            return settings;
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Failed to load settings; defaults will be used.", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        AppPaths.EnsureLocalDirectories();
        string temp = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, _jsonOptions));
        File.Move(temp, AppPaths.SettingsFile, true);
    }
}
