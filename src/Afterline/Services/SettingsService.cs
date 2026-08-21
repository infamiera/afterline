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
            if (!File.Exists(AppPaths.SettingsFile)) return new AppSettings();
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
