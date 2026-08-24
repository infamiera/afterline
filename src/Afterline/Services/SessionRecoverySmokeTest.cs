using Afterline.Models;

namespace Afterline.Services;

internal static class SessionRecoverySmokeTest
{
    public static async Task RunAsync(string archiveRoot)
    {
        if (string.IsNullOrWhiteSpace(archiveRoot))
            throw new ArgumentException("A smoke-test archive folder is required.", nameof(archiveRoot));

        Directory.CreateDirectory(archiveRoot);
        AppPaths.EnsureLocalDirectories();

        DateTime startedAt = DateTime.Today.AddHours(4).AddMinutes(40);
        var server = new ServerSessionInfo
        {
            Name = "Afterline Recovery Smoke Server",
            Address = "127.0.0.1:30120"
        };
        var initial = new SessionJournal();
        ChatEntry? marker = await initial.EnsureStartedAsync(
            archiveRoot,
            startedAt,
            server,
            CancellationToken.None);
        if (marker is null)
            throw new InvalidOperationException("The initial journal did not create its login marker.");

        string firstLine = "[04:40:56] Welcome to the recovery smoke test.";
        string secondLine = "[04:41:13] Recovery checkpoint line.";
        var firstLineColors = new[]
        {
            new ChatColorRun(0, 10, 56, 150, 243),
            new ChatColorRun(10, firstLine.Length - 10, 255, 255, 255)
        };
        await initial.AppendAsync(
            new ChatEntry(
                startedAt.AddSeconds(56),
                firstLine,
                capturedColorRuns: firstLineColors),
            CancellationToken.None);
        await initial.AppendAsync(
            new ChatEntry(startedAt.AddMinutes(1).AddSeconds(13), secondLine),
            CancellationToken.None);
        await initial.UpdateVisibleSnapshotAsync(
            new[] { firstLine, secondLine },
            CancellationToken.None);

        // Simulate the replay cache being absent after a power interruption. The
        // resumed journal must reconstruct it from its write-through backup.
        if (File.Exists(AppPaths.LastSessionCacheFile))
            File.Delete(AppPaths.LastSessionCacheFile);
        ChatColorSidecarService.DeleteForTextFile(AppPaths.LastSessionCacheFile);

        var resumed = new SessionJournal();
        IReadOnlyList<string> visible = await resumed.RecoverAsync(
            archiveRoot,
            CancellationToken.None);
        if (!resumed.HasActiveSession || resumed.StartedAt != startedAt || visible.Count != 2)
            throw new InvalidOperationException("The interrupted journal did not resume its active session.");

        IReadOnlyList<ChatEntry> cached = await new LastSessionCacheService().ReadAsync(CancellationToken.None);
        ChatEntry? recoveredFirst = cached.FirstOrDefault(entry =>
            entry.Text.Contains(firstLine, StringComparison.Ordinal));
        if (cached.Count != 3 ||
            recoveredFirst is null ||
            !ChatColorData.HasCompleteCoverage(recoveredFirst.Text, recoveredFirst.CapturedColorRuns))
            throw new InvalidOperationException("The last-session replay cache was not rebuilt from the journal backup.");

        VerifyHtmlChatExport(recoveredFirst, startedAt);

        string continuation = "[04:42:00] Continued after Afterline restarted.";
        await resumed.AppendAsync(
            new ChatEntry(startedAt.AddMinutes(2), continuation),
            CancellationToken.None);

        string archiveFile = resumed.ActiveFile
            ?? throw new InvalidOperationException("The resumed journal has no archive file.");
        string archiveText = await File.ReadAllTextAsync(archiveFile);
        int loginMarkers = archiveText.Split("[NEW LOGIN]", StringSplitOptions.None).Length - 1;
        if (loginMarkers != 1 || !archiveText.Contains(continuation, StringComparison.Ordinal))
            throw new InvalidOperationException("Restarting the journal created a false session boundary or lost its continuation.");

        await resumed.FinalizeAsync(archiveRoot, CancellationToken.None);
    }

    private static void VerifyHtmlChatExport(ChatEntry exactColorEntry, DateTime exportedAt)
    {
        const string tattoo = "[05:58:23] [INFO] You have bought the My Crazy Life tattoo for $735.";
        const string unsafeText = "[05:59:00] <script>alert('Afterline')</script>";
        string html = ChatHtmlExportService.BuildDocument(
            "Afterline <Export>",
            "Smoke test <context>",
            new[]
            {
                new ChatHtmlExportItem(exactColorEntry, exactColorEntry.Text, 1),
                new ChatHtmlExportItem(new ChatEntry(exportedAt, tattoo), tattoo, 2),
                new ChatHtmlExportItem(new ChatEntry(exportedAt, unsafeText), unsafeText, 3)
            },
            useAutomaticColors: true,
            exportedAt: exportedAt);

        if (!html.Contains("color:#3896F3", StringComparison.Ordinal) ||
            !html.Contains("color:#FBF724", StringComparison.Ordinal) ||
            !html.Contains("color:#56D64B", StringComparison.Ordinal) ||
            !html.Contains("&lt;script&gt;", StringComparison.Ordinal) ||
            !html.Contains("&lt;/script&gt;", StringComparison.Ordinal) ||
            html.Contains("<script>alert", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The HTML export did not preserve exact/manual colors or safely encode chat text.");
        }
    }
}
