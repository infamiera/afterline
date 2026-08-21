using System.Windows.Media;

namespace Afterline.Models;

public sealed class LogReaderLineItem
{
    public int LineNumber { get; }
    public string RawLine { get; }
    public ChatEntry Entry { get; }

    public string Display => Entry.Display;
    public Brush Foreground => Entry.Foreground;
    public bool IsOocLine => Entry.IsOocLine;

    public LogReaderLineItem(int lineNumber, string rawLine, ChatEntry entry)
    {
        LineNumber = lineNumber;
        RawLine = rawLine;
        Entry = entry;
    }
}
