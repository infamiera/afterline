using System.Globalization;
using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

internal enum ServerClockSource
{
    UtcFallback,
    LearnedOffset,
    DetectedTimeZone,
    ManualTimeZone
}

internal sealed record ServerClockResult(
    DateTime ServerTime,
    ServerClockSource Source,
    string Description);

internal static class ServerTimeService
{
    private static readonly Regex TimestampPrefix = new(
        @"^\[(?<time>\d{1,2}:\d{2}:\d{2})\]\s*",
        RegexOptions.Compiled);

    private static readonly TimeSpan MinimumOffset = TimeSpan.FromHours(-12);
    private static readonly TimeSpan MaximumOffset = TimeSpan.FromHours(14);
    private const int OffsetStepMinutes = 15;

    public static ServerClockResult Resolve(
        AppSettings settings,
        ServerSessionInfo? server,
        DateTimeOffset utcNow)
    {
        ServerTimeZonePreference? preference = Find(settings, server);
        string? timeZoneId = preference?.ManualTimeZoneId;
        ServerClockSource source = ServerClockSource.ManualTimeZone;

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZoneId = preference?.DetectedTimeZoneId ?? server?.TimeZoneIdHint;
            source = ServerClockSource.DetectedTimeZone;
        }

        if (TryFindTimeZone(timeZoneId, out TimeZoneInfo? zone))
        {
            DateTime time = TimeZoneInfo.ConvertTime(utcNow, zone).DateTime;
            return new ServerClockResult(
                time,
                source,
                source == ServerClockSource.ManualTimeZone
                    ? $"manual · {zone.DisplayName}"
                    : $"automatic · {zone.DisplayName}");
        }

        int? offsetMinutes = preference?.LearnedUtcOffsetMinutes ?? server?.UtcOffsetMinutesHint;
        if (offsetMinutes is int minutes && IsValidOffset(minutes))
        {
            DateTime time = utcNow.ToOffset(TimeSpan.FromMinutes(minutes)).DateTime;
            return new ServerClockResult(
                time,
                ServerClockSource.LearnedOffset,
                $"automatic · UTC{FormatOffset(minutes)}");
        }

        return new ServerClockResult(
            utcNow.UtcDateTime,
            ServerClockSource.UtcFallback,
            "UTC · server timezone not detected");
    }

    public static bool ApplyServerHint(
        AppSettings settings,
        ServerSessionInfo? server)
    {
        if (server is null || string.Equals(server.StableKey, "unknown", StringComparison.Ordinal))
            return false;

        string? detectedId = TryFindTimeZone(server.TimeZoneIdHint, out TimeZoneInfo? zone)
            ? zone.Id
            : null;
        int? detectedOffset = server.UtcOffsetMinutesHint is int minutes && IsValidOffset(minutes)
            ? minutes
            : null;
        if (detectedId is null && detectedOffset is null)
            return false;

        ServerTimeZonePreference preference = GetOrCreate(settings, server);
        if (preference.IsManual)
            return false;

        bool changed = !string.Equals(
                           preference.DetectedTimeZoneId,
                           detectedId,
                           StringComparison.OrdinalIgnoreCase) ||
                       preference.LearnedUtcOffsetMinutes != detectedOffset;
        if (!changed) return false;

        preference.DetectedTimeZoneId = detectedId;
        if (detectedOffset is not null)
            preference.LearnedUtcOffsetMinutes = detectedOffset;
        preference.UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    public static bool TryLearnFromFreshTimestamp(
        AppSettings settings,
        ServerSessionInfo? server,
        string line,
        DateTimeOffset observedUtc)
    {
        if (server is null || string.Equals(server.StableKey, "unknown", StringComparison.Ordinal))
            return false;

        ServerTimeZonePreference? existing = Find(settings, server);
        if (existing?.IsManual == true || !string.IsNullOrWhiteSpace(existing?.DetectedTimeZoneId))
            return false;

        Match match = TimestampPrefix.Match(line ?? string.Empty);
        if (!match.Success || !DateTime.TryParseExact(
                match.Groups["time"].Value,
                "H:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
            return false;

        DateTime utc = observedUtc.UtcDateTime;
        int? previousOffset = existing?.LearnedUtcOffsetMinutes;
        var candidates = Enumerable.Range(-1, 3)
            .Select(day => utc.Date.AddDays(day).Add(parsed.TimeOfDay) - utc)
            .Where(offset => offset >= MinimumOffset && offset <= MaximumOffset)
            .Select(offset => new
            {
                Minutes = (int)Math.Round(
                    offset.TotalMinutes / OffsetStepMinutes,
                    MidpointRounding.AwayFromZero) * OffsetStepMinutes,
                ResidualSeconds = Math.Abs(
                    offset.TotalSeconds - Math.Round(
                        offset.TotalMinutes / OffsetStepMinutes,
                        MidpointRounding.AwayFromZero) * OffsetStepMinutes * 60)
            })
            .Where(candidate => IsValidOffset(candidate.Minutes) && candidate.ResidualSeconds <= 90)
            .OrderBy(candidate => previousOffset is int previous
                ? Math.Abs(candidate.Minutes - previous)
                : Math.Abs(candidate.Minutes))
            .ThenBy(candidate => candidate.ResidualSeconds)
            .ToArray();

        if (candidates.Length == 0)
            return false;

        int learnedOffset = candidates[0].Minutes;
        ServerTimeZonePreference preference = existing ?? GetOrCreate(settings, server);
        if (preference.LearnedUtcOffsetMinutes == learnedOffset)
            return false;

        preference.LearnedUtcOffsetMinutes = learnedOffset;
        preference.UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    public static void SetManualTimeZone(
        AppSettings settings,
        ServerSessionInfo server,
        string? timeZoneId)
    {
        ServerTimeZonePreference preference = GetOrCreate(settings, server);
        preference.ManualTimeZoneId = TryFindTimeZone(timeZoneId, out TimeZoneInfo? zone)
            ? zone.Id
            : null;
        preference.UpdatedAtUtc = DateTime.UtcNow;
    }

    public static string? GetManualTimeZoneId(AppSettings settings, ServerSessionInfo? server)
        => Find(settings, server)?.ManualTimeZoneId;

    public static bool TryParseServerHint(
        string? value,
        out string? timeZoneId,
        out int? utcOffsetMinutes)
    {
        timeZoneId = null;
        utcOffsetMinutes = null;
        if (string.IsNullOrWhiteSpace(value)) return false;

        string candidate = value.Trim();
        if (TryFindTimeZone(candidate, out TimeZoneInfo? zone))
        {
            timeZoneId = zone.Id;
            return true;
        }

        candidate = candidate
            .Replace("UTC", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("GMT", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        if (candidate.Length == 0)
        {
            utcOffsetMinutes = 0;
            return true;
        }

        bool negative = candidate.StartsWith("-", StringComparison.Ordinal);
        string unsignedCandidate = candidate.TrimStart('+', '-');
        if (TimeSpan.TryParseExact(
                unsignedCandidate,
                new[] { @"h\:mm", @"hh\:mm" },
                CultureInfo.InvariantCulture,
                TimeSpanStyles.None,
                out TimeSpan parsed))
        {
            if (negative)
                parsed = -parsed;
            int minutes = (int)parsed.TotalMinutes;
            if (IsValidOffset(minutes))
            {
                utcOffsetMinutes = minutes;
                return true;
            }
        }

        if (double.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out double hours))
        {
            int minutes = (int)Math.Round(hours * 60);
            if (IsValidOffset(minutes))
            {
                utcOffsetMinutes = minutes;
                return true;
            }
        }

        return false;
    }

    internal static void RunSmokeTest()
    {
        var settings = new AppSettings();
        var server = new ServerSessionInfo
        {
            Name = "Timestamp Test",
            Address = "127.0.0.1:30120"
        };
        DateTimeOffset observedUtc = new(2026, 9, 7, 18, 0, 1, TimeSpan.Zero);

        ServerClockResult fallback = Resolve(settings, server, observedUtc);
        if (fallback.Source != ServerClockSource.UtcFallback ||
            fallback.ServerTime != observedUtc.UtcDateTime)
            throw new InvalidOperationException("Timestamp-free chat fell back to the player's local clock.");

        if (!TryLearnFromFreshTimestamp(settings, server, "[14:00:00] Test", observedUtc))
            throw new InvalidOperationException("A fresh server timestamp did not establish its UTC offset.");

        ServerClockResult learned = Resolve(settings, server, observedUtc);
        if (learned.Source != ServerClockSource.LearnedOffset ||
            learned.ServerTime.TimeOfDay != new TimeSpan(14, 0, 1))
            throw new InvalidOperationException("The learned server offset was not used for timestamp-free chat.");

        if (!TryParseServerHint("UTC+05:30", out _, out int? parsedOffset) || parsedOffset != 330)
            throw new InvalidOperationException("A server UTC-offset hint was not parsed.");
        if (!TryParseServerHint("UTC-05:00", out _, out parsedOffset) || parsedOffset != -300)
            throw new InvalidOperationException("A negative server UTC-offset hint was not parsed.");
    }

    private static ServerTimeZonePreference? Find(
        AppSettings settings,
        ServerSessionInfo? server)
    {
        if (server is null) return null;
        lock (settings.ServerTimeZones)
        {
            return settings.ServerTimeZones.FirstOrDefault(item => string.Equals(
                item.ServerKey,
                server.StableKey,
                StringComparison.OrdinalIgnoreCase));
        }
    }

    private static ServerTimeZonePreference GetOrCreate(
        AppSettings settings,
        ServerSessionInfo server)
    {
        lock (settings.ServerTimeZones)
        {
            ServerTimeZonePreference? existing = settings.ServerTimeZones.FirstOrDefault(
                item => string.Equals(
                    item.ServerKey,
                    server.StableKey,
                    StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.ServerName = server.DisplayName;
                return existing;
            }

            var created = new ServerTimeZonePreference
            {
                ServerKey = server.StableKey,
                ServerName = server.DisplayName
            };
            settings.ServerTimeZones.Add(created);
            return created;
        }
    }

    private static bool TryFindTimeZone(
        string? timeZoneId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(timeZoneId)) return false;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidOffset(int minutes)
        => minutes >= MinimumOffset.TotalMinutes && minutes <= MaximumOffset.TotalMinutes;

    private static string FormatOffset(int minutes)
    {
        TimeSpan offset = TimeSpan.FromMinutes(minutes);
        string sign = minutes >= 0 ? "+" : "-";
        return $"{sign}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}";
    }
}
