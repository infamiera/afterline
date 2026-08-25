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
                return new AppSettings { FirstRunCompleted = false, ArchiveLoadingPolicyVersion = 1 };

            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), _jsonOptions)
                                   ?? new AppSettings();

            // Older or manually edited settings files can explicitly contain null
            // values even though these properties are non-nullable in current builds.
            // Normalize them before any startup UI reads Editor or theme preferences.
            settings.Editor ??= new EditorPreferences();
            settings.Theme ??= ThemeService.CreateDefault();
            settings.CustomThemes ??= new List<SavedThemePreset>();
            settings.CustomThemes = settings.CustomThemes
                .Where(preset => preset is not null && !string.IsNullOrWhiteSpace(preset.Name))
                .GroupBy(preset => preset.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(ThemeService.MaximumCustomThemes)
                .Select(preset => new SavedThemePreset
                {
                    Name = ThemeService.NormalizeCustomThemeName(preset.Name),
                    Theme = ThemeService.Normalize(preset.Theme),
                    SavedAtUtc = preset.SavedAtUtc
                })
                .ToList();
            settings.RecentLogPaths ??= new List<string>();
            settings.PinnedLogPaths ??= new List<string>();
            settings.ArchiveFilterMode = settings.ArchiveFilterMode switch
            {
                "All" => "All",
                "Between" => "Between",
                _ => "LastDays"
            };
            // Version 1 changes the historical 30-day implicit default to seven days.
            // Preserve ranges users deliberately changed, while migrating untouched
            // older installs away from an expensive startup/archive scan.
            bool archivePolicyChanged = false;
            if (settings.ArchiveLoadingPolicyVersion is null)
            {
                if (settings.ArchiveFilterMode == "LastDays" && settings.ArchiveLastDays == 30)
                    settings.ArchiveLastDays = 7;
                settings.ArchiveLoadingPolicyVersion = 1;
                archivePolicyChanged = true;
            }
            settings.ArchiveLastDays = Math.Clamp(settings.ArchiveLastDays, 1, 3650);
            settings.Editor.ProjectAutosaveMinutes = settings.Editor.ProjectAutosaveMinutes switch
            {
                0 or 1 or 5 or 10 or 15 or 30 => settings.Editor.ProjectAutosaveMinutes,
                _ => 5
            };
            settings.Editor.ExportKeybind ??= "Ctrl+S";
            settings.Editor.UndoKeybind ??= "Ctrl+Z";
            settings.Editor.RedoKeybind ??= "Ctrl+Shift+Z";
            settings.Editor.FullscreenKeybind ??= "F11";
            settings.Editor.RulerKeybind ??= "R";
            if (string.IsNullOrWhiteSpace(settings.Editor.ProjectsFolder))
            {
                settings.Editor.ProjectsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Afterline Projects");
            }

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

            if (archivePolicyChanged)
            {
                try
                {
                    Save(settings);
                }
                catch (Exception ex)
                {
                    DiagnosticLogger.Error("Unable to persist the archive loading-policy migration.", ex);
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
