using System.Text;
using Afterline.Models;

namespace Afterline.Services;

public sealed record ManualChatLineRemovalResult(string BackupPath);

/// <summary>
/// Performs a user-selected single-line removal from a closed chatlog. Unlike
/// duplicate scanning, the caller supplies the exact zero-based source row.
/// The original and optional colour sidecar are backed up before replacement.
/// </summary>
public static class ManualChatLineRemovalService
{
    public static async Task<ManualChatLineRemovalResult> RemoveAtAsync(
        string chatlogPath,
        int zeroBasedLineIndex,
        string expectedLine,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(chatlogPath))
            throw new FileNotFoundException("The selected chatlog no longer exists.", chatlogPath);

        string original = await File.ReadAllTextAsync(chatlogPath, cancellationToken);
        string newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        bool endedWithNewline = original.EndsWith("\n", StringComparison.Ordinal);
        string[] lines = original.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (endedWithNewline && lines.Length > 0 && lines[^1].Length == 0)
            lines = lines[..^1];

        if (zeroBasedLineIndex < 0 || zeroBasedLineIndex >= lines.Length ||
            !string.Equals(lines[zeroBasedLineIndex], expectedLine, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The selected row no longer matches the chatlog. Nothing was changed.");
        }

        IReadOnlyDictionary<int, ChatColorLineRecord> colors =
            await ChatColorSidecarService.MatchLinesAsync(chatlogPath, lines, cancellationToken);
        (string Line, int OriginalIndex)[] retained = lines
            .Select((line, index) => (Line: line, OriginalIndex: index))
            .Where(item => item.OriginalIndex != zeroBasedLineIndex)
            .ToArray();

        Directory.CreateDirectory(AppPaths.RecoveryBackupsDirectory);
        string backupPath = UniquePath(
            AppPaths.RecoveryBackupsDirectory,
            $"Manual Line Removal Backup [{DateTime.Now:yyyy-MM-dd - HH-mm-ss}] {Path.GetFileNameWithoutExtension(chatlogPath)}",
            ".txt");
        File.Copy(chatlogPath, backupPath, overwrite: false);
        ChatColorSidecarService.CopyForTextFile(chatlogPath, backupPath, overwrite: false);

        string temporary = chatlogPath + $".{Environment.ProcessId}.manual-remove.tmp";
        try
        {
            string replacement = string.Join(newline, retained.Select(item => item.Line));
            if (endedWithNewline) replacement += newline;
            await File.WriteAllTextAsync(temporary, replacement, new UTF8Encoding(false), cancellationToken);
            File.Move(temporary, chatlogPath, overwrite: true);

            try
            {
                await ChatColorSidecarService.ReplaceAsync(
                    chatlogPath,
                    retained.Where(item => colors.ContainsKey(item.OriginalIndex))
                        .Select(item => colors[item.OriginalIndex]),
                    CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DiagnosticLogger.Error("Manual line removal succeeded, but exact chat colours could not be rebuilt.", ex);
            }

            return new ManualChatLineRemovalResult(backupPath);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch { }
        }
    }

    internal static async Task RunSmokeTestAsync(string root, CancellationToken cancellationToken)
    {
        string folder = Path.Combine(root, "manual-line-removal-smoke");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Chatlog [Manual Line Removal Smoke].txt");
        string[] original = { "[12:00:00] Original.", "[12:00:01] Remove this.", "[12:00:02] Original." };
        await File.WriteAllLinesAsync(path, original, new UTF8Encoding(false), cancellationToken);

        ManualChatLineRemovalResult result = await RemoveAtAsync(path, 1, original[1], cancellationToken);
        string[] updated = await File.ReadAllLinesAsync(path, cancellationToken);
        if (!updated.SequenceEqual(new[] { original[0], original[2] }, StringComparer.Ordinal) ||
            !File.Exists(result.BackupPath) ||
            !File.ReadAllLines(result.BackupPath).SequenceEqual(original, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Manual line removal did not preserve the exact chatlog backup.");
        }
    }

    private static string UniquePath(string folder, string baseName, string extension)
    {
        string path = Path.Combine(folder, baseName + extension);
        if (!File.Exists(path)) return path;
        for (int suffix = 2; ; suffix++)
        {
            string candidate = Path.Combine(folder, $"{baseName} ({suffix}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
