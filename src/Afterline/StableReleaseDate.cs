using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Afterline;

internal static class StableReleaseDate
{
    internal static string Value { get; } = Load();

    internal static void ValidatePackagedBuild()
    {
        string informational = (Assembly.GetEntryAssembly() ?? typeof(StableReleaseDate).Assembly)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        if (Regex.IsMatch(informational, @"^v\d+\.\d+\.\d+\+", RegexOptions.IgnoreCase) &&
            Value == "Release date pending")
            throw new InvalidDataException("The Stable package is missing its release date.");
    }

    private static string Load()
    {
        try
        {
            using Stream? stream = typeof(StableReleaseDate).Assembly
                .GetManifestResourceStream("Afterline.StableReleaseDate.json");
            if (stream is null) return "Release date pending";
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.GetProperty("date").GetString() ?? "Release date pending";
        }
        catch (Exception)
        {
            return "Release date pending";
        }
    }
}
