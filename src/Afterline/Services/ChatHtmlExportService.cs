using System.Globalization;
using System.Net;
using System.Text;
using System.Windows.Media;
using Afterline.Models;

namespace Afterline.Services;

internal sealed record ChatHtmlExportItem(
    ChatEntry Entry,
    string DisplayText,
    int? LineNumber = null);

internal static class ChatHtmlExportService
{
    internal static async Task ExportAsync(
        string destination,
        string title,
        string context,
        IReadOnlyList<ChatHtmlExportItem> lines,
        bool useAutomaticColors,
        DateTime exportedAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("An HTML export destination is required.", nameof(destination));
        if (lines.Count == 0)
            throw new InvalidOperationException("There are no visible chat lines to export.");

        string? folder = Path.GetDirectoryName(Path.GetFullPath(destination));
        if (string.IsNullOrWhiteSpace(folder))
            throw new InvalidOperationException("The HTML export folder could not be resolved.");

        Directory.CreateDirectory(folder);
        string temporary = Path.Combine(
            folder,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.writing");

        try
        {
            string html = BuildDocument(title, context, lines, useAutomaticColors, exportedAt);
            await using (var stream = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.Read,
                             16 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(html.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporary, destination, false);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            catch
            {
                // A stale temporary file is harmless and must not hide the export result.
            }
        }
    }

    internal static string BuildDocument(
        string title,
        string context,
        IReadOnlyList<ChatHtmlExportItem> lines,
        bool useAutomaticColors,
        DateTime exportedAt)
    {
        string safeTitle = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(title) ? "Afterline Chat Export" : title.Trim());
        string safeContext = WebUtility.HtmlEncode(context ?? string.Empty);
        var html = new StringBuilder(Math.Max(8192, lines.Count * 180));

        html.AppendLine("<!doctype html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"utf-8\">");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("  <title>").Append(safeTitle).AppendLine("</title>");
        html.AppendLine("  <style>");
        html.AppendLine("    :root { color-scheme: dark; font-family: Inter, Segoe UI, Arial, sans-serif; }");
        html.AppendLine("    * { box-sizing: border-box; }");
        html.AppendLine("    body { margin: 0; background: #080d12; color: #edf2f7; }");
        html.AppendLine("    main { width: min(1180px, calc(100% - 32px)); margin: 28px auto; }");
        html.AppendLine("    header { margin-bottom: 16px; padding: 18px 20px; border: 1px solid #273342; border-radius: 12px; background: #111820; }");
        html.AppendLine("    h1 { margin: 0 0 7px; font-size: 20px; font-weight: 650; }");
        html.AppendLine("    .context { color: #a8b2be; font-size: 13px; overflow-wrap: anywhere; }");
        html.AppendLine("    .meta { margin-top: 5px; color: #748190; font-size: 12px; }");
        html.AppendLine("    .chat-log { overflow: hidden; border: 1px solid #273342; border-radius: 12px; background: #05090d; padding: 12px 0; }");
        html.AppendLine("    .chat-line { display: grid; grid-template-columns: minmax(0, 1fr); gap: 12px; padding: 3px 18px; }");
        html.AppendLine("    .chat-line.numbered { grid-template-columns: 54px minmax(0, 1fr); }");
        html.AppendLine("    .line-number { color: #687585; font: 12px/1.45 Consolas, monospace; text-align: right; user-select: none; }");
        html.AppendLine("    .message { min-width: 0; white-space: pre-wrap; overflow-wrap: anywhere; font-family: Arial, sans-serif; font-size: 15px; font-weight: 700; line-height: 1.35; text-shadow: -1px -1px 0 #000, 1px -1px 0 #000, -1px 1px 0 #000, 1px 1px 0 #000; }");
        html.AppendLine("    @media (max-width: 620px) { main { width: 100%; margin: 0; } header, .chat-log { border-left: 0; border-right: 0; border-radius: 0; } .chat-line { padding-inline: 10px; } .chat-line.numbered { grid-template-columns: 38px minmax(0, 1fr); } }");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<main>");
        html.AppendLine("  <header>");
        html.Append("    <h1>").Append(safeTitle).AppendLine("</h1>");
        if (!string.IsNullOrWhiteSpace(context))
            html.Append("    <div class=\"context\">").Append(safeContext).AppendLine("</div>");
        html.Append("    <div class=\"meta\">Exported ")
            .Append(WebUtility.HtmlEncode(exportedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)))
            .Append(" · ")
            .Append(lines.Count.ToString("N0", CultureInfo.InvariantCulture))
            .AppendLine(lines.Count == 1 ? " line</div>" : " lines</div>");
        html.AppendLine("  </header>");
        html.AppendLine("  <section class=\"chat-log\" aria-label=\"Exported chat lines\">");

        foreach (ChatHtmlExportItem item in lines)
            AppendLine(html, item, useAutomaticColors);

        html.AppendLine("  </section>");
        html.AppendLine("</main>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static void AppendLine(
        StringBuilder html,
        ChatHtmlExportItem item,
        bool useAutomaticColors)
    {
        string text = item.DisplayText ?? string.Empty;
        html.Append(item.LineNumber.HasValue
            ? "    <div class=\"chat-line numbered\">"
            : "    <div class=\"chat-line\">");

        if (item.LineNumber is int lineNumber)
        {
            html.Append("<span class=\"line-number\">")
                .Append(lineNumber.ToString(CultureInfo.InvariantCulture))
                .Append("</span>");
        }

        html.Append("<span class=\"message\">");
        if (text.Length == 0)
        {
            html.Append("&nbsp;");
        }
        else
        {
            IReadOnlyList<EditorChatSegment> segments = ResolveSegments(item.Entry, text, useAutomaticColors);
            foreach (EditorChatSegment segment in segments)
            {
                if (segment.Text.Length == 0) continue;
                html.Append("<span style=\"color:")
                    .Append(ToCssColor(segment.Color))
                    .Append("\">")
                    .Append(WebUtility.HtmlEncode(segment.Text))
                    .Append("</span>");
            }
        }

        html.AppendLine("</span></div>");
    }

    private static IReadOnlyList<EditorChatSegment> ResolveSegments(
        ChatEntry entry,
        string text,
        bool useAutomaticColors)
    {
        Color fallback = entry.Foreground is SolidColorBrush brush
            ? brush.Color
            : EditorChatFormatter.White;

        if (entry.IsSystemMessage && EditorChatFormatter.IsSessionBoundaryMarker(text))
            return new[] { new EditorChatSegment(text, EditorChatFormatter.Blue) };

        if (!useAutomaticColors || entry.IsSystemMessage)
            return new[] { new EditorChatSegment(text, fallback) };

        IReadOnlyList<ChatColorRun> exactRuns = ChatColorData.NormalizeRuns(
            text,
            entry.GetColorRunsForText(text));
        if (exactRuns.Count > 0 && ChatColorData.HasCompleteCoverage(text, exactRuns))
        {
            return exactRuns.Select(run => new EditorChatSegment(
                    text.Substring(run.Start, run.Length),
                    Color.FromArgb(run.Alpha, run.Red, run.Green, run.Blue)))
                .ToArray();
        }

        EditorChatLine? formatted = UnifiedChatFormatter
            .FormatLines(text, showTimestamps: true)
            .FirstOrDefault();
        if (formatted is not null &&
            formatted.Segments.Count > 0 &&
            string.Equals(
                string.Concat(formatted.Segments.Select(segment => segment.Text)),
                text,
                StringComparison.Ordinal))
        {
            return formatted.Segments;
        }

        return new[] { new EditorChatSegment(text, fallback) };
    }

    private static string ToCssColor(Color color)
    {
        if (color.A == byte.MaxValue)
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        string alpha = (color.A / 255d).ToString("0.###", CultureInfo.InvariantCulture);
        return $"rgba({color.R}, {color.G}, {color.B}, {alpha})";
    }
}
