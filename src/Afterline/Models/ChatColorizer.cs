using System.Windows.Media;

namespace Afterline.Models;

public static class ChatColorizer
{
    // Approximate roleplay chat palette based on the supplied in-game reference.
    public static Brush DefaultBrush { get; } = Make("#EDF2F7");
    private static Brush ActionBrush { get; } = Make("#C2A3DA");
    private static Brush WhisperBrush { get; } = Make("#EDA841");
    private static Brush PhoneBrush { get; } = Make("#FBF724");
    private static Brush BlueBrush { get; } = Make("#3896F3");
    private static Brush GreenBrush { get; } = Make("#56D64B");
    private static Brush RedBrush { get; } = Make("#F00000");
    private static Brush OrangeBrush { get; } = Make("#EDA841");

    public static Brush GetBrush(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return DefaultBrush;
        string value = text.Trim();
        string lower = value.ToLowerInvariant();

        if (lower.StartsWith("*") || lower.Contains(" (( ") || lower.EndsWith("))*") || lower.Contains(" drops "))
            return ActionBrush;

        if (lower.Contains("whispers:") || lower.Contains("[megaphone]:") || lower.StartsWith("(car) ") && lower.Contains("whispers:"))
            return WhisperBrush;

        if (lower.Contains("says (cellphone):") || lower.StartsWith("[phone]") || lower.Contains("/pickup") || lower.Contains("/hangup"))
            return PhoneBrush;

        if (lower.StartsWith("[info]") || lower.Contains(" intercom]") || lower.StartsWith("[character kill]") ||
            lower.StartsWith("description of ") || lower.StartsWith("age range:") || lower.Contains("tattoos description"))
            return BlueBrush;

        if (lower.Contains("has shown you") || lower.StartsWith("you have successfully") || lower.StartsWith("you received") ||
            lower.StartsWith("you gave ") || lower.Contains(" paid you ") || lower.Contains("you have received $") ||
            lower.Contains("successfully sent") || lower.Contains("backpack items"))
            return GreenBrush;

        if (lower.Contains("has been killed") || lower.StartsWith("you took ") && lower.Contains("from the property") ||
            lower.Contains("/removelocation"))
            return RedBrush;

        if (lower.StartsWith("info:") || lower.StartsWith("[drug lab]") || lower.StartsWith("you placed "))
            return OrangeBrush;

        if (lower.Contains("(goods)") || lower.Contains(" rifle x") || lower.Contains("total weight:") || lower.Contains("money ($"))
            return PhoneBrush;

        return DefaultBrush;
    }

    private static Brush Make(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}


