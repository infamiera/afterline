using Afterline.Models;
using System.Windows.Media;

namespace Afterline.Services;

internal static class SessionRecoverySmokeTest
{
    public static async Task RunAsync(string archiveRoot)
    {
        if (string.IsNullOrWhiteSpace(archiveRoot))
            throw new ArgumentException("A smoke-test archive folder is required.", nameof(archiveRoot));

        Directory.CreateDirectory(archiveRoot);
        AppPaths.EnsureLocalDirectories();
        ApplicationHealthMonitor.RunPersistenceSmokeTest(archiveRoot);
        DiagnosticLogger.RunPreviousSessionSnapshotSmokeTest(archiveRoot);
        CaptureReplayGuard.RunSmokeTest();
        VerifyTimestampProvenance();
        await PotentialDuplicateCleanupService.RunSmokeTestAsync(
            archiveRoot,
            CancellationToken.None);

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

        string repeatedFirst = "[04:42:01] (( PM from (196) Player: hi ))";
        string repeatedSecond = "[04:42:02] (( PM from (196) Player: hi ))";
        await resumed.AppendAsync(
            new ChatEntry(startedAt.AddMinutes(2).AddSeconds(1), repeatedFirst),
            CancellationToken.None);
        await resumed.AppendAsync(
            new ChatEntry(startedAt.AddMinutes(2).AddSeconds(2), repeatedSecond),
            CancellationToken.None);
        IReadOnlyList<string> committedTail = await resumed.ReadRecentCommittedLinesAsync(
            20,
            CancellationToken.None);
        if (!committedTail.Contains(repeatedFirst, StringComparer.Ordinal) ||
            !committedTail.Contains(repeatedSecond, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Legitimate repeated messages with distinct visible timestamps were not both committed.");
        }

        string archiveFile = resumed.ActiveFile
            ?? throw new InvalidOperationException("The resumed journal has no archive file.");
        string archiveText = await File.ReadAllTextAsync(archiveFile);
        int loginMarkers = archiveText.Split("[NEW LOGIN]", StringSplitOptions.None).Length - 1;
        if (loginMarkers != 1 ||
            !archiveText.Contains(continuation, StringComparison.Ordinal) ||
            !archiveText.Contains(repeatedFirst, StringComparison.Ordinal) ||
            !archiveText.Contains(repeatedSecond, StringComparison.Ordinal))
            throw new InvalidOperationException("Restarting the journal created a false session boundary or lost its continuation.");

        string finalizedPath = await resumed.FinalizeAsync(archiveRoot, CancellationToken.None)
            ?? throw new InvalidOperationException("The resumed session did not produce a finalized archive file.");
        bool indexed = await new ArchiveService().EnsureFileIndexedAsync(
            archiveRoot,
            finalizedPath,
            CancellationToken.None);
        if (!indexed)
            throw new InvalidOperationException("A finalized FiveM session was not verified in the archive index.");

        VerifyStartupRegistrationCommand();
        VerifyOocGameplayFiltering();
        VerifyStreamerModeMasking();
        await VerifyArchiveDateFilteringAsync(archiveRoot);
    }

    private static void VerifyOocGameplayFiltering()
    {
        string[] filteredLines =
        {
            "[15:58:47] Your vehicle has been teleported to your location. Please wait for a few seconds if the vehicle does not load in.",
            "[14:23:57] [AFK CHECK] You're considered AFK, type /notafk to confirm that you're playing.",
            "[12:58:52] Little Seoul Ammu Nation: Press Y to browse ammunation.",
            "[07:37:19] [Admin Alert]: A staff message",
            "[16:21:16] * You have de-spawned your pet.",
            "[16:21:09] You have loaded Toffee their settings.",
            "[23:46:32] [INFO]: [17/JUL/2026] Doors are unlocked via fingerprint scanner. Only the Owners and CEO have access.",
            "[23:47:09] [INFO]: You are not the owner of this property. Please be aware that inactivity can affect it.",
            "[00:01:28] [Character kill] Jose Sandoval has been killed."
        };
        foreach (string line in filteredLines)
        {
            if (!new ChatEntry(DateTime.Now, line).IsOocLine)
                throw new InvalidOperationException($"The gameplay/OOC filter did not classify: {line}");
        }

        const string roleplay = "[15:59:00] Bianca says: This is an in-character line.";
        if (new ChatEntry(DateTime.Now, roleplay).IsOocLine)
            throw new InvalidOperationException("The gameplay/OOC filter hid an ordinary roleplay line.");
    }

    private static void VerifyTimestampProvenance()
    {
        DateTime observed = DateTime.Today.AddHours(17);
        var visibleTimestamp = new ChatEntry(
            observed,
            "[14:53:02] (( PM from (196) Player: hi ))");
        var localTimestamp = new ChatEntry(
            observed,
            "(( PM from (196) Player: hi ))");

        if (visibleTimestamp.TimestampSource != ChatTimestampSource.VisibleChat ||
            visibleTimestamp.CapturedAt.TimeOfDay != new TimeSpan(14, 53, 2) ||
            localTimestamp.TimestampSource != ChatTimestampSource.LocalObservation ||
            localTimestamp.CapturedAt != observed)
        {
            throw new InvalidOperationException(
                "Visible FiveM timestamps were not kept distinct from local observation time.");
        }
    }

    private static void VerifyStreamerModeMasking()
    {
        bool previous = StreamerModePresentationService.Enabled;
        try
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string path = Path.Combine(profile, "Documents", "Afterline", "Screenshots");
            StreamerModePresentationService.Enabled = true;
            string masked = StreamerModePresentationService.PathForDisplay(path);
            if (string.Equals(masked, path, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(profile) && masked.Contains(profile, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Streamer mode exposed a local user-profile path.");
            }
        }
        finally
        {
            StreamerModePresentationService.Enabled = previous;
        }
    }

    private static void VerifyStartupRegistrationCommand()
    {
        string executable = Path.Combine(
            Path.GetTempPath(),
            "Afterline Canary",
            "Afterline.exe");
        string current = StartupService.BuildCommand(executable);
        string stale = StartupService.BuildCommand(Path.Combine(
            Path.GetTempPath(),
            "Afterline Canary",
            "Afterline-old.exe"));

        if (!StartupService.CommandTargetsExecutable(current, executable) ||
            StartupService.CommandTargetsExecutable(stale, executable))
        {
            throw new InvalidOperationException(
                "Windows startup registration did not distinguish the current executable from a stale Canary path.");
        }
    }

    private static async Task VerifyArchiveDateFilteringAsync(string archiveRoot)
    {
        string filterRoot = Path.Combine(archiveRoot, "archive-filter-smoke");
        Directory.CreateDirectory(filterRoot);
        string oldPath = Path.Combine(filterRoot, "Chatlog [Archive Smoke] [01-January-2020].txt");
        string today = DateTime.Today.ToString(
            "dd-MMMM-yyyy",
            System.Globalization.CultureInfo.InvariantCulture);
        string todayPath = Path.Combine(
            filterRoot,
            $"Chatlog [Archive Smoke] [{today}].txt");
        await File.WriteAllTextAsync(oldPath, "old archive line");
        await File.WriteAllTextAsync(todayPath, "current archive line");

        // Lock the old file. A correctly prefiltered refresh never attempts to
        // open it and therefore still succeeds on Windows.
        await using var lockedOldFile = new FileStream(
            oldPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
        IReadOnlyList<SessionIndexEntry> visible = await new ArchiveService().RebuildIndexAsync(
            filterRoot,
            CancellationToken.None,
            DateTime.Today,
            DateTime.Today);
        if (visible.Count != 1 ||
            !string.Equals(visible[0].FilePath, todayPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Archive date filtering read or returned files outside the requested range.");
        }

        var boundedPaths = new List<string>();
        DateTime newestWrite = DateTime.UtcNow.AddMinutes(-1);
        for (int index = 0; index < 24; index++)
        {
            string path = Path.Combine(
                filterRoot,
                $"Chatlog [Archive Load {index:D2}] [{today}].txt");
            await File.WriteAllTextAsync(path, $"bounded archive line {index}");
            File.SetLastWriteTimeUtc(path, newestWrite.AddMinutes(-index));
            boundedPaths.Add(path);
        }

        // The oldest eligible file is deliberately unreadable. A bounded load
        // must choose its newest candidates before opening any chatlog contents.
        await using var lockedEligibleFile = new FileStream(
            boundedPaths[^1],
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);
        IReadOnlyList<SessionIndexEntry> bounded = await new ArchiveService().RebuildIndexAsync(
            filterRoot,
            CancellationToken.None,
            DateTime.Today,
            DateTime.Today,
            maxEntries: 5);
        if (bounded.Count != 5 ||
            bounded.Any(entry => string.Equals(
                entry.FilePath,
                boundedPaths[^1],
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Archive safety limiting did not restrict content reads to the newest files.");
        }
    }

    private static void VerifyHtmlChatExport(ChatEntry exactColorEntry, DateTime exportedAt)
    {
        const string tattoo = "[05:58:23] [INFO] You have bought the My Crazy Life tattoo for $735.";
        const string attachmentInstruction = "[07:05:34] Attachments found on your Weapons. Use /detach weaponIndex or /detach weaponIndex attachmentIndex to disassemble the Weapon!";
        const string unsafeText = "[05:59:00] <script>alert('Afterline')</script>";
        var initiallyWhiteInstruction = new ChatEntry(
            exportedAt,
            attachmentInstruction,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, attachmentInstruction.Length, 255, 255, 255)
            });
        VerifyRecoveredCommandAccents(initiallyWhiteInstruction, attachmentInstruction);
        VerifyCapturedAccentPrecedence(exportedAt, attachmentInstruction);
        VerifyActivityPandaPointAccents(exportedAt);
        VerifyLowSpeechNeutrality(exportedAt);
        VerifyGlobalOocRoles(exportedAt);
        VerifyItalicTypography(exportedAt);
        VerifyMixedActionSpeechColors(exportedAt);
        VerifyUniformLineColorPropagation(exportedAt);
        VerifyEditorTextRangeColors();

        string html = ChatHtmlExportService.BuildDocument(
            "Afterline <Export>",
            "Smoke test <context>",
            new[]
            {
                new ChatHtmlExportItem(exactColorEntry, exactColorEntry.Text, 1),
                new ChatHtmlExportItem(new ChatEntry(exportedAt, tattoo), tattoo, 2),
                new ChatHtmlExportItem(initiallyWhiteInstruction, attachmentInstruction, 3),
                new ChatHtmlExportItem(new ChatEntry(exportedAt, unsafeText), unsafeText, 4),
                new ChatHtmlExportItem(
                    new ChatEntry(exportedAt, "[06:00:00] Samayo says [low]: /quietly amused/"),
                    "[06:00:00] Samayo says [low]: /quietly amused/",
                    5)
            },
            useAutomaticColors: true,
            exportedAt: exportedAt);

        if (!html.Contains("color:#3896F3", StringComparison.Ordinal) ||
            !html.Contains("color:#FBF724", StringComparison.Ordinal) ||
            !html.Contains("color:#56D64B", StringComparison.Ordinal) ||
            !html.Contains("color:#EDA841", StringComparison.Ordinal) ||
            !html.Contains("font-style:italic", StringComparison.Ordinal) ||
            !html.Contains("&lt;script&gt;", StringComparison.Ordinal) ||
            !html.Contains("&lt;/script&gt;", StringComparison.Ordinal) ||
            html.Contains("<script>alert", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The HTML export did not preserve exact/manual colors or safely encode chat text.");
        }
    }

    private static void VerifyLowSpeechNeutrality(DateTime observedAt)
    {
        const string text = "[12:41:07] Alexandra Krasnova says [low]: Feels wrong doin' it to her—as... wrong as it sounds, my other one's used to it, likes it even.";
        var entry = new ChatEntry(
            observedAt,
            text,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, text.Length, 0x56, 0xD6, 0x4B)
            });

        int body = text.IndexOf("Alexandra", StringComparison.Ordinal);
        if (!HasColorAt(entry.CapturedColorRuns, body, 0xFF, 0xFF, 0xFF))
            throw new InvalidOperationException("A [low] speech line retained a leaked green row color.");

        const string faded = "[18:47:45] Samayo Yurei says [low]: Mh— neither but—";
        int fadedBody = faded.IndexOf("Samayo", StringComparison.Ordinal);
        var fadedEntry = new ChatEntry(
            observedAt,
            faded,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, fadedBody, 0x72, 0x76, 0x7B, 0xB8),
                new ChatColorRun(fadedBody, faded.Length - fadedBody, 0xFF, 0xFF, 0xFF)
            });

        int fadedMessage = faded.IndexOf("Mh", StringComparison.Ordinal);
        if (!HasColorAt(fadedEntry.CapturedColorRuns, fadedBody, 0x72, 0x76, 0x7B, 0xB8) ||
            !HasColorAt(fadedEntry.CapturedColorRuns, fadedMessage, 0x72, 0x76, 0x7B, 0xB8))
        {
            throw new InvalidOperationException(
                "A faded [low] timestamp shade was not propagated through its chat body.");
        }
    }

    private static void VerifyUniformLineColorPropagation(DateTime observedAt)
    {
        const string text = "[10:03:54] Alexandra Krasnova says (phone): Tryna' do some shoppin'— my wardrobe is outdated.";
        int bodyStart = text.IndexOf("Alexandra", StringComparison.Ordinal);
        var entry = new ChatEntry(
            observedAt,
            text,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, bodyStart, 0x9C, 0x99, 0x16, 0xC0),
                new ChatColorRun(bodyStart, text.Length - bodyStart, 0xFF, 0xFF, 0xFF)
            });

        int speaker = text.IndexOf("Alexandra", StringComparison.Ordinal);
        int message = text.IndexOf("Tryna'", StringComparison.Ordinal);
        if (!HasColorAt(entry.CapturedColorRuns, speaker, 0x9C, 0x99, 0x16, 0xC0) ||
            !HasColorAt(entry.CapturedColorRuns, message, 0x9C, 0x99, 0x16, 0xC0))
        {
            throw new InvalidOperationException(
                "A recognized whole-line phone color was applied only to its timestamp.");
        }

        var exact = new Dictionary<int, ChatColorLineRecord>
        {
            [0] = new ChatColorLineRecord
            {
                Text = text,
                ColorRuns = new List<ChatColorRun>
                {
                    new(0, bodyStart, 0x9C, 0x99, 0x16, 0xC0),
                    new(bodyStart, text.Length - bodyStart, 0xFF, 0xFF, 0xFF)
                }
            }
        };
        EditorChatLine displayed = UnifiedChatFormatter
            .FormatLines(text, showTimestamps: true, exactColors: exact)
            .First();
        Color fadedPhone = Color.FromArgb(0xC0, 0x9C, 0x99, 0x16);
        if (!displayed.Segments.Any(segment =>
                segment.Color == fadedPhone &&
                segment.Text.Contains("Tryna'", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The shared Live Chat/Log Reader formatter retained a neutral phone body.");
        }
    }

    private static void VerifyGlobalOocRoles(DateTime observedAt)
    {
        VerifyGlobalOocRole(observedAt, 0xFF, 0x00, 0x00, 0xFF, 0x00, 0x00);
        VerifyGlobalOocRole(observedAt, 0xED, 0xA8, 0x41, 0xED, 0xA8, 0x41);
        VerifyGlobalOocRole(observedAt, 0x38, 0x96, 0xF3, 0x38, 0x96, 0xF3);
    }

    private static void VerifyGlobalOocRole(
        DateTime observedAt,
        byte capturedRed,
        byte capturedGreen,
        byte capturedBlue,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        const string text = "[11:02:42] (( Global OOC: (64) Loke: If anyone died due to the explosions, please PM me ))";
        int name = text.IndexOf("Loke", StringComparison.Ordinal);
        int nameEnd = name + "Loke".Length;
        var entry = new ChatEntry(
            observedAt,
            text,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, name, 0xFB, 0xF7, 0x24),
                new ChatColorRun(name, nameEnd - name, capturedRed, capturedGreen, capturedBlue),
                new ChatColorRun(nameEnd, text.Length - nameEnd, 0xFB, 0xF7, 0x24)
            });

        int message = text.IndexOf("If anyone", StringComparison.Ordinal);
        int globalLabel = text.IndexOf("Global OOC", StringComparison.Ordinal);
        if (!HasColorAt(entry.CapturedColorRuns, globalLabel, 0xFF, 0xFF, 0xFF) ||
            !HasColorAt(entry.CapturedColorRuns, message, 0xFF, 0xFF, 0xFF) ||
            !HasColorAt(entry.CapturedColorRuns, name, expectedRed, expectedGreen, expectedBlue))
        {
            throw new InvalidOperationException(
                "Global OOC did not keep its message white and its sender role-colored.");
        }
    }

    private static void VerifyItalicTypography(DateTime observedAt)
    {
        const string text = "[06:00:00] Samayo says [low]: /quietly amused/ before replying.";
        EditorChatLine line = UnifiedChatFormatter
            .FormatLines(text, showTimestamps: true)
            .First();
        if (!line.Segments.Any(segment =>
                segment.IsItalic && segment.Text.Contains("/quietly amused/", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Slash-delimited roleplay emphasis was not italicized.");
        }

        const string commandText = "Use /detach weaponIndex or /detach weaponIndex attachmentIndex.";
        if (UnifiedChatFormatter.FormatLines(commandText, showTimestamps: false)
            .SelectMany(value => value.Segments)
            .Any(segment => segment.IsItalic))
        {
            throw new InvalidOperationException("Slash commands were mistaken for italic roleplay text.");
        }

        int italicStart = text.IndexOf("/quietly amused/", StringComparison.Ordinal);
        var exact = new ChatEntry(
            observedAt,
            text,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, italicStart, 255, 255, 255),
                new ChatColorRun(italicStart, "/quietly amused/".Length, 255, 255, 255, 255, true),
                new ChatColorRun(
                    italicStart + "/quietly amused/".Length,
                    text.Length - italicStart - "/quietly amused/".Length,
                    255,
                    255,
                    255)
            });
        if (!exact.CapturedColorRuns.Any(run => run.Italic && run.Start == italicStart))
            throw new InvalidOperationException("Computed FiveM italics were not retained in captured metadata.");
    }

    private static void VerifyActivityPandaPointAccents(DateTime observedAt)
    {
        const string text = "[08:35:00] ((You've received 100 Panda Points for your activity! You can earn up to 800 Panda Points per day.))";
        var entry = new ChatEntry(
            observedAt,
            text,
            capturedColorRuns: new[] { new ChatColorRun(0, text.Length, 255, 255, 255) });

        int firstValue = text.IndexOf("100 Panda Points", StringComparison.Ordinal);
        int secondValue = text.IndexOf("800 Panda Points", StringComparison.Ordinal);
        if (!HasColorAt(entry.DisplayColorRuns, firstValue, 0x56, 0xD6, 0x4B) ||
            !HasColorAt(entry.DisplayColorRuns, secondValue, 0x56, 0xD6, 0x4B))
        {
            throw new InvalidOperationException(
                "An all-white FiveM snapshot suppressed Panda Point activity values in Live Chat.");
        }
    }

    private static void VerifyMixedActionSpeechColors(DateTime observedAt)
    {
        const string text = "[17:39:53] * Bianca Yurei grabs the mop from the counter. Welp— someone's gotta do it. She starts sweeping the floor.";
        var prematurelyFlat = new CapturedChatLine(
            text,
            new[] { new ChatColorRun(0, text.Length, 0xC2, 0xA3, 0xDA) });
        if (!FiveMDevToolsChatReader.ContainsFlattenedLeadingAction(new[] { prematurelyFlat }))
        {
            throw new InvalidOperationException(
                "A prematurely flattened leading-star action row would bypass capture stabilization.");
        }

        int speechStart = text.IndexOf("Welp", StringComparison.Ordinal);
        int secondAction = text.IndexOf("She starts", StringComparison.Ordinal);
        var exact = new ChatEntry(
            observedAt,
            text,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, speechStart, 0xC2, 0xA3, 0xDA),
                new ChatColorRun(speechStart, secondAction - speechStart, 0xFF, 0xFF, 0xFF),
                new ChatColorRun(secondAction, text.Length - secondAction, 0xC2, 0xA3, 0xDA)
            });
        if (!HasColorAt(exact.CapturedColorRuns, text.IndexOf("grabs", StringComparison.Ordinal), 0xC2, 0xA3, 0xDA) ||
            !HasColorAt(exact.CapturedColorRuns, speechStart, 0xFF, 0xFF, 0xFF) ||
            !HasColorAt(exact.CapturedColorRuns, secondAction, 0xC2, 0xA3, 0xDA))
        {
            throw new InvalidOperationException(
                "A valid mixed FiveM action/speech snapshot was flattened into one color.");
        }

        const string paired = "Bianca says [low]: Hmmm. *She scans the items.* Still here. *She turns back.*";
        EditorChatLine fallback = UnifiedChatFormatter.FormatLines(paired, showTimestamps: false).First();
        if (!fallback.Segments.Any(segment =>
                segment.Color == EditorChatFormatter.Purple &&
                segment.Text.Contains("She scans", StringComparison.Ordinal)) ||
            !fallback.Segments.Any(segment =>
                segment.Color == EditorChatFormatter.White &&
                segment.Text.Contains("Still here", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Paired-star action ranges were not colored independently from speech.");
        }

        const string lowMixed = "[17:38:23] Bianca Yurei says [low]: Hmmm. She scans the items in the room.";
        int lowAction = lowMixed.IndexOf("She scans", StringComparison.Ordinal);
        var lowExact = new ChatEntry(
            observedAt,
            lowMixed,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, lowAction, 255, 255, 255),
                new ChatColorRun(lowAction, lowMixed.Length - lowAction, 0xC2, 0xA3, 0xDA)
            });
        if (!HasColorAt(lowExact.CapturedColorRuns, lowMixed.IndexOf("Hmmm", StringComparison.Ordinal), 255, 255, 255) ||
            !HasColorAt(lowExact.CapturedColorRuns, lowAction, 0xC2, 0xA3, 0xDA))
        {
            throw new InvalidOperationException(
                "The [low] neutrality safeguard removed a legitimate computed action color.");
        }
    }

    private static void VerifyEditorTextRangeColors()
    {
        const string text = "[17:38:23] Bianca Yurei says [low]: /quietly amused/ before replying.";
        int start = text.IndexOf("Bianca Yurei", StringComparison.Ordinal);
        var textColors = new[]
        {
            new EditorTextColorOverride(
                0,
                start,
                "Bianca Yurei".Length,
                "Bianca Yurei",
                EditorChatFormatter.Red)
        };
        EditorChatLine line = UnifiedChatFormatter.FormatLines(
            text,
            showTimestamps: true,
            textOverrides: textColors).First();
        if (!line.Segments.Any(segment =>
                segment.Color == EditorChatFormatter.Red &&
                segment.Text.Contains("Bianca Yurei", StringComparison.Ordinal)) ||
            !line.Segments.Any(segment =>
                segment.IsItalic &&
                segment.Text.Contains("/quietly amused/", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Editor selected-text coloring did not preserve the selected range and italics.");
        }
    }

    private static void VerifyRecoveredCommandAccents(
        ChatEntry entry,
        string text)
    {
        int firstCommand = text.IndexOf("/detach weaponIndex", StringComparison.Ordinal);
        int secondCommand = text.LastIndexOf("/detach weaponIndex attachmentIndex", StringComparison.Ordinal);
        int surroundingText = text.IndexOf("Attachments found", StringComparison.Ordinal);
        EditorChatLine? manualFallback = UnifiedChatFormatter
            .FormatLines(text, showTimestamps: true)
            .FirstOrDefault();
        int manualCommandAccents = manualFallback?.Segments.Count(segment =>
            segment.Color == EditorChatFormatter.Orange &&
            segment.Text.StartsWith("/detach", StringComparison.OrdinalIgnoreCase)) ?? 0;
        if (!HasColorAt(entry.CapturedColorRuns, firstCommand, 0xED, 0xA8, 0x41) ||
            !HasColorAt(entry.CapturedColorRuns, secondCommand, 0xED, 0xA8, 0x41) ||
            !HasColorAt(entry.CapturedColorRuns, surroundingText, 0xFF, 0xFF, 0xFF) ||
            manualCommandAccents != 2)
        {
            throw new InvalidOperationException(
                "A temporarily unstyled FiveM snapshot suppressed required command accents.");
        }
    }

    private static bool HasColorAt(
        IEnumerable<ChatColorRun> runs,
        int index,
        byte red,
        byte green,
        byte blue,
        byte alpha = 255)
        => index >= 0 && runs.Any(run =>
            run.Start <= index &&
            run.End > index &&
            run.Red == red &&
            run.Green == green &&
            run.Blue == blue &&
            run.Alpha == alpha);

    private static void VerifyCapturedAccentPrecedence(
        DateTime observedAt,
        string text)
    {
        const string shortCommand = "/detach weaponIndex";
        int firstCommand = text.IndexOf(shortCommand, StringComparison.Ordinal);
        int secondCommand = text.LastIndexOf(
            "/detach weaponIndex attachmentIndex",
            StringComparison.Ordinal);
        int firstEnd = firstCommand + shortCommand.Length;
        var partlyStyled = new ChatEntry(
            observedAt,
            text,
            capturedColorRuns: new[]
            {
                new ChatColorRun(0, firstCommand, 255, 255, 255),
                new ChatColorRun(firstCommand, shortCommand.Length, 0x12, 0xB4, 0xE8),
                new ChatColorRun(firstEnd, text.Length - firstEnd, 255, 255, 255)
            });

        if (!HasColorAt(partlyStyled.CapturedColorRuns, firstCommand, 0x12, 0xB4, 0xE8) ||
            !HasColorAt(partlyStyled.CapturedColorRuns, secondCommand, 0xED, 0xA8, 0x41))
        {
            throw new InvalidOperationException(
                "The reliability check replaced a genuine FiveM accent or missed a neutral one.");
        }
    }
}
