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

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), _jsonOptions)
                   ?? new AppSettings();
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
