using System.Text;
using System.Text.Json;
using Afterline.Models;

namespace Afterline.Services;

public static class ChatColorSidecarService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string GetSidecarPath(string textFilePath)
        => Path.ChangeExtension(textFilePath, ".colors.jsonl");

    public static async Task AppendAsync(
        string textFilePath,
        string text,
        IEnumerable<ChatColorRun>? colorRuns,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatColorRun> runs = ChatColorData.NormalizeRuns(text, colorRuns);
        if (runs.Count == 0)
            return;

        var record = new ChatColorLineRecord
        {
            Text = text,
            ColorRuns = runs.ToList()
        };
        string json = JsonSerializer.Serialize(record);
        string sidecarPath = GetSidecarPath(textFilePath);

        await Gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
            await using FileStream stream = new(
                sidecarPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<IReadOnlyDictionary<int, ChatColorLineRecord>> MatchLinesAsync(
        string textFilePath,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatColorLineRecord> records;
        try
        {
            records = await ReadRecordsAsync(textFilePath, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error(
                $"Unable to read exact chat colors for {Path.GetFileName(textFilePath)}; automatic colors will be used.",
                ex);
            return new Dictionary<int, ChatColorLineRecord>();
        }
        if (records.Count == 0 || lines.Count == 0)
            return new Dictionary<int, ChatColorLineRecord>();

        var matches = new Dictionary<int, ChatColorLineRecord>();
        int searchStart = 0;
        foreach (ChatColorLineRecord record in records)
        {
            for (int index = searchStart; index < lines.Count; index++)
            {
                if (!string.Equals(lines[index], record.Text, StringComparison.Ordinal))
                    continue;

                IReadOnlyList<ChatColorRun> runs = ChatColorData.NormalizeRuns(
                    record.Text,
                    record.ColorRuns);
                if (runs.Count > 0)
                {
                    record.ColorRuns = runs.ToList();
                    matches[index] = record;
                }
                searchStart = index + 1;
                break;
            }
        }

        return matches;
    }

    public static void CopyForTextFile(string sourceTextFile, string destinationTextFile, bool overwrite)
    {
        try
        {
            string source = GetSidecarPath(sourceTextFile);
            if (!File.Exists(source))
                return;

            string destination = GetSidecarPath(destinationTextFile);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to copy optional exact chat color metadata.", ex);
        }
    }

    public static void MoveForTextFile(string sourceTextFile, string destinationTextFile, bool overwrite)
    {
        try
        {
            string source = GetSidecarPath(sourceTextFile);
            if (!File.Exists(source))
                return;

            string destination = GetSidecarPath(destinationTextFile);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(source, destination, overwrite);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to move optional exact chat color metadata.", ex);
        }
    }

    public static void DeleteForTextFile(string textFilePath)
    {
        try
        {
            string path = GetSidecarPath(textFilePath);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to delete optional exact chat color metadata.", ex);
        }
    }

    public static async Task ReplaceAsync(
        string textFilePath,
        IEnumerable<ChatColorLineRecord> records,
        CancellationToken cancellationToken)
    {
        string sidecarPath = GetSidecarPath(textFilePath);
        string temporary = sidecarPath + $".{Environment.ProcessId}.tmp";
        ChatColorLineRecord[] normalized = records
            .Select(record => new ChatColorLineRecord
            {
                Text = record.Text,
                ColorRuns = ChatColorData.NormalizeRuns(record.Text, record.ColorRuns).ToList()
            })
            .Where(record => !string.IsNullOrEmpty(record.Text) && record.ColorRuns.Count > 0)
            .ToArray();

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (normalized.Length == 0)
            {
                if (File.Exists(sidecarPath)) File.Delete(sidecarPath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
            await using (FileStream stream = new(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                foreach (ChatColorLineRecord record in normalized)
                {
                    string json = JsonSerializer.Serialize(record);
                    await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
                }
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporary, sidecarPath, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch { }
            Gate.Release();
        }
    }

    private static async Task<IReadOnlyList<ChatColorLineRecord>> ReadRecordsAsync(
        string textFilePath,
        CancellationToken cancellationToken)
    {
        string sidecarPath = GetSidecarPath(textFilePath);
        if (!File.Exists(sidecarPath))
            return Array.Empty<ChatColorLineRecord>();

        await Gate.WaitAsync(cancellationToken);
        try
        {
            string[] lines = await File.ReadAllLinesAsync(sidecarPath, cancellationToken);
            var records = new List<ChatColorLineRecord>(lines.Length);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    ChatColorLineRecord? record = JsonSerializer.Deserialize<ChatColorLineRecord>(line);
                    if (record is not null &&
                        !string.IsNullOrEmpty(record.Text) &&
                        record.ColorRuns is not null)
                        records.Add(record);
                }
                catch (JsonException)
                {
                    // A partial final record can be left behind by a hard power loss.
                    // Earlier complete records remain usable and the plain-text log is authoritative.
                }
            }
            return records;
        }
        finally
        {
            Gate.Release();
        }
    }
}
