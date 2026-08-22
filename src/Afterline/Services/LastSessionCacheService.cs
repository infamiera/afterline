using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Afterline.Models;

namespace Afterline.Services;

public sealed class LastSessionCacheService
{
    private static readonly Regex TimestampPrefix = new(
        @"^\[(?<time>\d{1,2}:\d{2}:\d{2})\]\s*",
        RegexOptions.Compiled);

    private static readonly Regex MarkerTimestamp = new(
        @"\[(?:NEW LOGIN|DISCONNECTED|DAY ENDED|DATE ROLLOVER)\]\s*-\s*(?<time>\d{1,2}:\d{2}:\d{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task BeginAsync(
        ServerSessionInfo server,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LastSessionCacheFile)!);

            await using FileStream stream = new(
                AppPaths.LastSessionCacheFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using StreamWriter writer = new(stream, new UTF8Encoding(false));

            await writer.WriteLineAsync(
                $"[AFTERLINE LAST SESSION: {server.DisplayName} · {startedAt:yyyy-MM-dd HH:mm:ss}]".AsMemory(),
                cancellationToken);
            await writer.FlushAsync(cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AppendAsync(ChatEntry entry, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LastSessionCacheFile)!);

            string line = entry.IsSystemMessage
                ? entry.Text
                : TimestampPrefix.IsMatch(entry.Text)
                    ? entry.Text
                    : $"[{entry.CapturedAt:HH:mm:ss}] {entry.Text}";

            await using FileStream stream = new(
                AppPaths.LastSessionCacheFile,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using StreamWriter writer = new(stream, new UTF8Encoding(false));

            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChatEntry>> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(AppPaths.LastSessionCacheFile))
                return Array.Empty<ChatEntry>();

            string[] lines = await File.ReadAllLinesAsync(AppPaths.LastSessionCacheFile, cancellationToken);
            if (lines.Length == 0)
                return Array.Empty<ChatEntry>();

            DateTime fallback = File.GetLastWriteTime(AppPaths.LastSessionCacheFile);
            var entries = new List<ChatEntry>();

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("[AFTERLINE LAST SESSION:", StringComparison.OrdinalIgnoreCase))
                    continue;

                Match markerMatch = MarkerTimestamp.Match(line);
                if (markerMatch.Success &&
                    DateTime.TryParseExact(
                        markerMatch.Groups["time"].Value,
                        "H:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime markerTime))
                {
                    DateTime markerTimestamp = fallback.Date.Add(markerTime.TimeOfDay);
                    if (markerTimestamp > fallback.AddHours(12))
                        markerTimestamp = markerTimestamp.AddDays(-1);

                    entries.Add(ChatEntry.System(markerTimestamp, line));
                    continue;
                }

                entries.Add(new ChatEntry(fallback, line));
            }

            return entries;
        }
        finally
        {
            _gate.Release();
        }
    }
}
