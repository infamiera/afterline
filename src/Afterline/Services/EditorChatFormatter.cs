using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Afterline.Services;

internal sealed record EditorChatSegment(string Text, Color Color, bool IsItalic = false);

internal sealed record EditorChatLine(
    int SourceIndex,
    string PlainText,
    string AutoStyle,
    IReadOnlyList<EditorChatSegment> Segments);

internal sealed record EditorColorPreset(string Key, string Name, Color Color);

internal static class EditorChatFormatter
{
    private static readonly Regex TimestampPrefix = new(
        @"^\s*(?<timestamp>\[\d{1,2}:\d{2}:\d{2}\])\s*(?<body>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex LeadingBracketTag = new(
        @"^\[(?<tag>[^\]]+)\](?<separator>:?)\s*(?<rest>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex MoneyToken = new(@"\$[\d,.]+", RegexOptions.Compiled);
    private static readonly Regex HashNumberToken = new(@"#(?:NUMBER|\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CommandToken = new(@"/[A-Za-z][A-Za-z0-9_-]*", RegexOptions.Compiled);
    private static readonly Regex DateToken = new(@"\[(?:\d{1,2})/[A-Za-z]{3,9}/(?:\d{2,4})\]", RegexOptions.Compiled);
    private static readonly Regex AutoCloseDurationToken = new(@"\b\d+(?:\.\d+)?\s+seconds?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TemperatureValueToken = new(@"-?\d+(?:\.\d+)?(?:°C|F)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WindSpeedValueToken = new(@"-?\d+(?:\.\d+)?\s*(?:km/h|mph)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HumidityValueToken = new(@"\b\d+(?:\.\d+)?\s*%", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PrecipitationValueToken = new(@"\b\d+(?:\.\d+)?\s*mm\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MotdLine = new(
        @"^(?<prefix>\s*\(\(\s*)(?<motd>MOTD)(?<middle>:\s*.*\s+-\s+)(?<name>.+?)(?<suffix>\s*\)\)\s*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Palette sampled from the supplied RP reference image. Keeping these exact values
    // makes automatic output line up much more closely with the in-game chat look.
    internal static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF);
    internal static readonly Color Yellow = Color.FromRgb(0xFB, 0xF7, 0x24);
    internal static readonly Color Green = Color.FromRgb(0x56, 0xD6, 0x4B);
    internal static readonly Color Purple = Color.FromRgb(0xC2, 0xA3, 0xDA);
    internal static readonly Color Blue = Color.FromRgb(0x38, 0x96, 0xF3);
    internal static readonly Color Orange = Color.FromRgb(0xED, 0xA8, 0x41);
    internal static readonly Color Red = Color.FromRgb(0xFF, 0x00, 0x00);
    internal static readonly Color Gray = Color.FromRgb(0xB8, 0xBE, 0xC7);
    internal static readonly Color Radio = Color.FromRgb(0xC8, 0xB4, 0x5A);
    internal static readonly Color MutedTimestamp = Color.FromRgb(0xA8, 0xB2, 0xBE);

    internal static readonly IReadOnlyList<EditorColorPreset> ColorPresets = new[]
    {
        new EditorColorPreset("white", "Normal / White", White),
        new EditorColorPreset("purple", "Action / Purple", Purple),
        new EditorColorPreset("blue", "Info / Blue", Blue),
        new EditorColorPreset("yellow", "Comms / Yellow", Yellow),
        new EditorColorPreset("green", "Success / Green", Green),
        new EditorColorPreset("orange", "Whisper / Orange", Orange),
        new EditorColorPreset("red", "Danger / Red", Red),
        new EditorColorPreset("gray", "OOC / Gray", Gray),
        new EditorColorPreset("radio", "Radio / Muted Gold", Radio)
    };

    public static IReadOnlyList<EditorChatLine> FormatLines(
        string input,
        bool showTimestamps,
        IReadOnlyDictionary<int, Color>? lineOverrides = null)
    {
        string normalized = (input ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');
        var result = new List<EditorChatLine>(lines.Length);
        bool emergencyBlock = false;

        for (int sourceIndex = 0; sourceIndex < lines.Length; sourceIndex++)
        {
            string rawLine = lines[sourceIndex];
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                result.Add(new EditorChatLine(sourceIndex, string.Empty, "Blank", Array.Empty<EditorChatSegment>()));
                emergencyBlock = false;
                continue;
            }

            string body = rawLine.TrimEnd();
            string? timestamp = null;
            Match timestampMatch = TimestampPrefix.Match(body);
            if (timestampMatch.Success)
            {
                timestamp = timestampMatch.Groups["timestamp"].Value;
                body = timestampMatch.Groups["body"].Value;
            }

            IReadOnlyList<EditorChatSegment> bodySegments;
            if (IsEmergencyHeader(body))
            {
                emergencyBlock = true;
                bodySegments = Single(body, Blue);
            }
            else if (emergencyBlock && body.TrimStart().StartsWith("*", StringComparison.Ordinal) && body.Contains(':'))
            {
                bodySegments = FormatEmergencyDetail(body);
            }
            else
            {
                emergencyBlock = false;
                bodySegments = FormatBody(body);
            }

            string autoStyle = DescribeAutoStyle(bodySegments);
            if (lineOverrides is not null && lineOverrides.TryGetValue(sourceIndex, out Color overrideColor))
                bodySegments = Single(body, overrideColor);

            if (showTimestamps && !string.IsNullOrWhiteSpace(timestamp))
            {
                var withTimestamp = new List<EditorChatSegment>(bodySegments.Count + 1)
                {
                    new(timestamp + " ", MutedTimestamp)
                };
                withTimestamp.AddRange(bodySegments);
                bodySegments = withTimestamp;
            }

            bodySegments = ChatTypographyService.ApplySlashItalics(bodySegments);

            result.Add(new EditorChatLine(sourceIndex, body, autoStyle, bodySegments));
        }

        return result;
    }

    internal static EditorColorPreset? FindPreset(Color color)
        => ColorPresets.FirstOrDefault(preset => preset.Color == color);

    internal static bool IsSessionBoundaryMarker(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string value = text.Trim();
        Match timestampMatch = TimestampPrefix.Match(value);
        if (timestampMatch.Success)
            value = timestampMatch.Groups["body"].Value.Trim();

        return value.StartsWith("=", StringComparison.Ordinal) &&
               ContainsAny(value, "[NEW LOGIN]", "[DISCONNECTED]", "[LOGOUT]");
    }

    private static IReadOnlyList<EditorChatSegment> FormatBody(string body)
    {
        string trimmed = body.TrimStart();
        if (trimmed.Length == 0) return Array.Empty<EditorChatSegment>();

        if (IsSessionBoundaryMarker(trimmed))
            return Single(body, Blue);

        if (ChatColorReliabilityService.IsNeutralLowSpeech(body) ||
            ChatColorReliabilityService.IsGlobalOoc(body))
            return Single(body, White);

        Match motdMatch = MotdLine.Match(body);
        if (motdMatch.Success)
            return FormatMotdLine(motdMatch);

        if (trimmed.Equals("You unlocked the property door.", StringComparison.OrdinalIgnoreCase))
            return Single(body, Green);

        if (trimmed.Equals("You locked the property door.", StringComparison.OrdinalIgnoreCase))
            return Single(body, Red);

        if (trimmed.StartsWith("You unlocked the door.", StringComparison.OrdinalIgnoreCase))
            return FormatDoorActionLine(body, "unlocked", Green);

        if (trimmed.Equals("You locked the door lock.", StringComparison.OrdinalIgnoreCase))
            return FormatDoorActionLine(body, "locked", Red);

        if (trimmed.StartsWith("Welcome to GTA World", StringComparison.OrdinalIgnoreCase))
            return HighlightPhrase(body, "GTA World", Yellow, White);

        if (trimmed.StartsWith("Weather forecast:", StringComparison.OrdinalIgnoreCase))
            return Single(body, Blue);

        if (trimmed.StartsWith("Temperature:", StringComparison.OrdinalIgnoreCase))
            return FormatTemperatureLine(body);

        if (trimmed.StartsWith("Wind:", StringComparison.OrdinalIgnoreCase))
            return FormatWindLine(body);

        if (trimmed.StartsWith("((", StringComparison.Ordinal))
        {
            bool pm = ContainsAny(trimmed, " PM ", "PM to", "PM from", "Incoming PM", "Outgoing PM", "Gelen PM", "Giden PM");
            return Single(body, pm ? Yellow : Gray);
        }

        if (IsPhoneAction(trimmed) || trimmed.StartsWith("*", StringComparison.Ordinal) || trimmed.StartsWith(">", StringComparison.Ordinal))
            return Single(body, Purple);

        if (trimmed.StartsWith("**", StringComparison.Ordinal))
            return Single(body, ContainsAny(trimmed, "->", "[S:", "[CH:") ? Radio : Yellow);

        if (ContainsAny(trimmed, "Description of ", "Tattoos description of ", "Description:", "Tattoos:"))
            return Single(body, Blue);

        if (ContainsAny(trimmed, "[Intercom]", "[İnterkom]", "[Interkom]"))
            return Single(body, Blue);

        if (ContainsAny(trimmed, "[Microphone]", "[Mikrofon]", "[Megaphone]", "[Megafon]", "(Phone)", "(Telefon)", "(cellphone)"))
            return Single(body, Yellow);

        if (ContainsAny(trimmed, "(iFruit ", "message from", "group message"))
            return Single(body, Yellow);

        if (ContainsAny(trimmed, "[PHONE]"))
            return FormatPhoneInstruction(body);

        if (ContainsAny(trimmed, "backpack items", "backpack contents", "bag items"))
            return Single(body, Green);

        if (LooksLikeBackpackItem(trimmed) || LooksLikeInventoryItem(trimmed))
            return Single(body, Yellow);

        if (ContainsAny(trimmed, "[Character kill]", "[Character Kill]"))
            return SplitAtClosingTag(body, Blue, Red);

        if (ContainsAny(trimmed, "[DRUG LAB]", "[DRUGLAB]"))
            return SplitAtClosingTag(body, Orange, White);

        Match tagMatch = LeadingBracketTag.Match(trimmed);
        if (tagMatch.Success)
            return FormatTaggedLine(body, trimmed, tagMatch);

        if (ContainsAny(trimmed, "whispers:", "whispered:", "fısıldar:", "fısıldadı:"))
        {
            if (ContainsAny(trimmed, "(Vehicle)", "(Car)", "(Araç İçi)")) return Single(body, Yellow);
            return Single(body, Orange);
        }

        if (LooksLikeReceivedMoney(trimmed))
            return FormatMoneyLine(body, White, Green);

        if (LooksLikeInfoMoney(trimmed))
            return FormatInfoMoneyLine(body);

        if (LooksLikeReceivedLocation(trimmed))
            return FormatLocationLine(body);

        if (LooksLikeSuccessOrTransfer(trimmed))
            return Single(body, Green);

        if (LooksLikePlacedItem(trimmed))
            return Single(body, Orange);

        if (LooksLikeRemovedItem(trimmed))
            return Single(body, Red);

        if (LooksLikeFoundItem(trimmed))
            return Single(body, Blue);

        if (ContainsAny(trimmed, "has shown you their", "has shown you his", "has shown you her"))
            return Single(body, Green);

        if (LooksLikeContactInfo(trimmed))
            return FormatContactInfoLine(body);

        return Single(body, White);
    }

    private static IReadOnlyList<EditorChatSegment> FormatTaggedLine(string original, string trimmed, Match match)
    {
        string tag = match.Groups["tag"].Value;
        string separator = match.Groups["separator"].Value;
        string rest = match.Groups["rest"].Value;
        int leadingWhitespace = original.Length - trimmed.Length;
        string prefixWhitespace = leadingWhitespace > 0 ? original[..leadingWhitespace] : string.Empty;
        string visibleTag = prefixWhitespace + "[" + tag + "]" + separator;
        string remainder = string.IsNullOrEmpty(rest) ? string.Empty : " " + rest;

        if (EqualsAny(tag, "CK", "CHARACTER KILL"))
            return Segments((visibleTag, Blue), (remainder, Red));

        if (EqualsAny(tag, "ERROR", "HATA", "WARNING"))
            return Segments((visibleTag, Red), (remainder, Red));

        if (EqualsAny(tag, "DRUG LAB", "DRUGLAB"))
            return Segments((visibleTag, Orange), (remainder, White));

        if (EqualsAny(tag, "INFO"))
            return FormatInfoTaggedLine(visibleTag, remainder);

        if (EqualsAny(tag, "BİLGİ", "BILGI", "CASHTAP"))
            return Segments((visibleTag, Green), (remainder, White));

        if (ContainsAny(tag, "ANTI-FALL", "ANTI FALL"))
            return Segments((visibleTag, Blue), (remainder, White));

        return Segments((visibleTag, Blue), (remainder, White));
    }

    private static IReadOnlyList<EditorChatSegment> FormatInfoTaggedLine(string visibleTag, string remainder)
    {
        var result = new List<EditorChatSegment> { new(visibleTag, Blue) };
        if (string.IsNullOrEmpty(remainder)) return result;

        Match date = DateToken.Match(remainder);
        if (!date.Success)
        {
            result.AddRange(FormatTokenHighlights(remainder, White));
            return result;
        }

        if (date.Index > 0) result.Add(new EditorChatSegment(remainder[..date.Index], White));
        result.Add(new EditorChatSegment(date.Value, Orange));
        string after = remainder[(date.Index + date.Length)..];
        result.AddRange(FormatTokenHighlights(after, White));
        return result;
    }

    private static IReadOnlyList<EditorChatSegment> FormatMotdLine(Match match)
        => Segments(
            (match.Groups["prefix"].Value, Gray),
            (match.Groups["motd"].Value, Yellow),
            (match.Groups["middle"].Value, Gray),
            (match.Groups["name"].Value, Blue),
            (match.Groups["suffix"].Value, Gray));

    private static IReadOnlyList<EditorChatSegment> FormatDoorActionLine(string body, string action, Color actionColor)
    {
        var highlights = new List<(int Index, int Length, Color Color)>();
        int actionIndex = body.IndexOf(action, StringComparison.OrdinalIgnoreCase);
        if (actionIndex >= 0)
            highlights.Add((actionIndex, action.Length, actionColor));

        Match duration = AutoCloseDurationToken.Match(body);
        if (duration.Success)
            highlights.Add((duration.Index, duration.Length, Yellow));

        return HighlightRanges(body, White, highlights);
    }

    private static IReadOnlyList<EditorChatSegment> FormatTemperatureLine(string body)
    {
        var highlights = TemperatureValueToken.Matches(body)
            .Cast<Match>()
            .Select(match => (match.Index, match.Length, Green))
            .ToList();

        int currentlyIndex = body.IndexOf("currently", StringComparison.OrdinalIgnoreCase);
        if (currentlyIndex >= 0)
        {
            int weatherStart = currentlyIndex + "currently".Length;
            while (weatherStart < body.Length && char.IsWhiteSpace(body[weatherStart])) weatherStart++;
            if (weatherStart < body.Length)
                highlights.Add((weatherStart, body.Length - weatherStart, Green));
        }

        return HighlightRanges(body, White, highlights);
    }

    private static IReadOnlyList<EditorChatSegment> FormatWindLine(string body)
    {
        var highlights = new List<(int Index, int Length, Color Color)>();
        AddRegexHighlights(highlights, WindSpeedValueToken, body, Green);
        AddRegexHighlights(highlights, HumidityValueToken, body, Green);
        AddRegexHighlights(highlights, PrecipitationValueToken, body, Green);
        return HighlightRanges(body, White, highlights);
    }

    private static IReadOnlyList<EditorChatSegment> HighlightPhrase(string body, string phrase, Color phraseColor, Color baseColor)
    {
        int index = body.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return Single(body, baseColor);
        return HighlightRanges(body, baseColor, new[] { (index, phrase.Length, phraseColor) });
    }

    private static void AddRegexHighlights(
        List<(int Index, int Length, Color Color)> highlights,
        Regex regex,
        string text,
        Color color)
    {
        foreach (Match match in regex.Matches(text))
            highlights.Add((match.Index, match.Length, color));
    }

    private static IReadOnlyList<EditorChatSegment> HighlightRanges(
        string text,
        Color baseColor,
        IEnumerable<(int Index, int Length, Color Color)> highlights)
    {
        var result = new List<EditorChatSegment>();
        int cursor = 0;
        foreach ((int index, int length, Color color) in highlights
                     .Where(value => value.Index >= 0 && value.Length > 0 && value.Index < text.Length)
                     .OrderBy(value => value.Index))
        {
            if (index < cursor) continue;
            if (index > cursor)
                result.Add(new EditorChatSegment(text[cursor..index], baseColor));

            int safeLength = Math.Min(length, text.Length - index);
            result.Add(new EditorChatSegment(text.Substring(index, safeLength), color));
            cursor = index + safeLength;
        }

        if (cursor < text.Length)
            result.Add(new EditorChatSegment(text[cursor..], baseColor));
        return result.Count == 0 ? Single(text, baseColor) : result;
    }

    private static IReadOnlyList<EditorChatSegment> FormatEmergencyDetail(string body)
    {
        int colon = body.IndexOf(':');
        if (colon < 0) return Single(body, Blue);
        return Segments((body[..(colon + 1)], Blue), (body[(colon + 1)..], White));
    }

    private static IReadOnlyList<EditorChatSegment> FormatPhoneInstruction(string body)
    {
        // Commands are intentionally highlighted individually because this is one of the
        // most visibly mixed-color line types in the reference image.
        var result = new List<EditorChatSegment>();
        AppendCommandAwareSegments(result, body, White);
        return result;
    }

    private static IReadOnlyList<EditorChatSegment> FormatMoneyLine(string body, Color baseColor, Color moneyColor)
    {
        Match money = MoneyToken.Match(body);
        if (!money.Success) return Single(body, baseColor);
        return Segments(
            (body[..money.Index], baseColor),
            (money.Value, moneyColor),
            (body[(money.Index + money.Length)..], baseColor));
    }

    private static IReadOnlyList<EditorChatSegment> FormatInfoMoneyLine(string body)
    {
        int colon = body.IndexOf(':');
        var result = new List<EditorChatSegment>();
        if (colon >= 0)
        {
            result.Add(new EditorChatSegment(body[..(colon + 1)], Orange));
            string remainder = body[(colon + 1)..];
            result.AddRange(FormatTokenHighlights(remainder, White));
            return result;
        }
        return FormatTokenHighlights(body, White);
    }

    private static IReadOnlyList<EditorChatSegment> FormatLocationLine(string body)
    {
        var result = new List<EditorChatSegment>();
        int cursor = 0;
        foreach (Match match in HashNumberToken.Matches(body).Cast<Match>().Concat(CommandToken.Matches(body).Cast<Match>()).OrderBy(m => m.Index))
        {
            if (match.Index < cursor) continue;
            if (match.Index > cursor) result.Add(new EditorChatSegment(body[cursor..match.Index], Green));
            Color color = match.Value.StartsWith("/", StringComparison.Ordinal) ? Red : Orange;
            result.Add(new EditorChatSegment(match.Value, color));
            cursor = match.Index + match.Length;
        }
        if (cursor < body.Length) result.Add(new EditorChatSegment(body[cursor..], Green));
        return result.Count == 0 ? Single(body, Green) : result;
    }

    private static IReadOnlyList<EditorChatSegment> FormatContactInfoLine(string body)
    {
        var result = new List<EditorChatSegment>();
        int cursor = 0;
        var matches = HashNumberToken.Matches(body).Cast<Match>()
            .Concat(CommandToken.Matches(body).Cast<Match>())
            .OrderBy(match => match.Index)
            .ToArray();

        foreach (Match match in matches)
        {
            if (match.Index < cursor) continue;
            if (match.Index > cursor)
                result.Add(new EditorChatSegment(body[cursor..match.Index], White));

            Color color = match.Value.StartsWith("/", StringComparison.Ordinal)
                ? ResolveCommandHighlight(match.Value)
                : Green;
            result.Add(new EditorChatSegment(match.Value, color));
            cursor = match.Index + match.Length;
        }
        if (cursor < body.Length) result.Add(new EditorChatSegment(body[cursor..], White));

        if (result.Count == 0) return Single(body, White);
        return result;
    }

    private static IReadOnlyList<EditorChatSegment> FormatTokenHighlights(string text, Color baseColor)
    {
        var result = new List<EditorChatSegment>();
        int cursor = 0;
        var matches = MoneyToken.Matches(text).Cast<Match>()
            .Concat(HashNumberToken.Matches(text).Cast<Match>())
            .Concat(CommandToken.Matches(text).Cast<Match>())
            .OrderBy(match => match.Index)
            .ToArray();

        foreach (Match match in matches)
        {
            if (match.Index < cursor) continue;
            if (match.Index > cursor) result.Add(new EditorChatSegment(text[cursor..match.Index], baseColor));

            Color highlight = match.Value.StartsWith("/", StringComparison.Ordinal)
                ? ResolveCommandHighlight(match.Value)
                : Green;
            result.Add(new EditorChatSegment(match.Value, highlight));
            cursor = match.Index + match.Length;
        }
        if (cursor < text.Length) result.Add(new EditorChatSegment(text[cursor..], baseColor));
        return result.Count == 0 ? Single(text, baseColor) : result;
    }

    private static void AppendCommandAwareSegments(List<EditorChatSegment> result, string text, Color baseColor)
    {
        int cursor = 0;
        foreach (Match match in CommandToken.Matches(text))
        {
            if (match.Index > cursor) result.Add(new EditorChatSegment(text[cursor..match.Index], baseColor));
            string command = match.Value;
            Color color = command.Equals("/pickup", StringComparison.OrdinalIgnoreCase) ? Green
                : command.Equals("/hangup", StringComparison.OrdinalIgnoreCase) ? Red
                : command.Equals("/phonecursor", StringComparison.OrdinalIgnoreCase) ? Orange
                : ResolveCommandHighlight(command);
            result.Add(new EditorChatSegment(command, color));
            cursor = match.Index + match.Length;
        }
        if (cursor < text.Length) result.Add(new EditorChatSegment(text[cursor..], baseColor));
        if (result.Count == 0) result.Add(new EditorChatSegment(text, baseColor));
    }

    private static Color ResolveCommandHighlight(string command)
        => command.Equals("/motdcheck", StringComparison.OrdinalIgnoreCase) ? Yellow : Blue;

    private static IReadOnlyList<EditorChatSegment> SplitAtClosingTag(string body, Color tagColor, Color remainderColor)
    {
        int close = body.IndexOf(']');
        if (close < 0) return Single(body, remainderColor);
        return Segments((body[..(close + 1)], tagColor), (body[(close + 1)..], remainderColor));
    }

    private static bool IsEmergencyHeader(string body)
        => ContainsAny(body, "EMERGENCY CALL", "ACİL ÇAĞRI", "ACIL CAGRI");

    private static bool IsPhoneAction(string body)
        => (body.StartsWith("(Phone)", StringComparison.OrdinalIgnoreCase) ||
            body.StartsWith("(Telefon)", StringComparison.OrdinalIgnoreCase)) && body.Contains('*');

    private static bool LooksLikeReceivedMoney(string body)
        => ContainsAny(body, "You have received $", "received $") && ContainsAny(body, "bank account", "account");

    private static bool LooksLikeInfoMoney(string body)
        => body.StartsWith("Info:", StringComparison.OrdinalIgnoreCase) && MoneyToken.IsMatch(body);

    private static bool LooksLikeReceivedLocation(string body)
        => ContainsAny(body, "received a location", "current location") && ContainsAny(body, "#NUMBER", "/removelocation", "location");

    private static bool LooksLikeSuccessOrTransfer(string body)
        => ContainsAny(body,
            "gave you", "you gave", "paid you", "you paid", "successfully sent", "transfer completed", "transfer successful",
            "successfully transferred", "adlı kişi sana", "adlı kişiye", "kişisine $", "Transfer başarıyla", "Transfer basariyla",
            "used ", "kullandın", "kullandin", "has shown you their", "has shown you his", "has shown you her");

    private static bool LooksLikePlacedItem(string body)
        => ContainsAny(body, "You placed ", "placed in property", "placed in vehicle", "Mülke ", "Mulke ", "Araca ") &&
           ContainsAny(body, "placed", "yerleştirdin", "yerlestirdin");

    private static bool LooksLikeRemovedItem(string body)
        => ContainsAny(body, "You took ", "removed from property", "took from property", "Mülkün içerisinden", "Mulkun icerisinden") &&
           ContainsAny(body, "property", "vehicle", "Mülk", "Mulkun", "Arac");

    private static bool LooksLikeFoundItem(string body)
        => ContainsAny(body, "found in property", "found in vehicle", "Mülkün içerisinde", "Mulkun icerisinde");

    private static bool LooksLikeBackpackItem(string body)
        => Regex.IsMatch(body, @"^\s*\d+[:.)]\s+.+") && ContainsAny(body, "grams", "kg", "lb", "money", "$", "item");

    private static bool LooksLikeInventoryItem(string body)
        => ContainsAny(body, "(Goods)", "(Weapon)", "(Item)") ||
           Regex.IsMatch(body, @"\bx\d+\s*\([^)]*(?:g|kg|lb)\)", RegexOptions.IgnoreCase);

    private static bool LooksLikeContactInfo(string body)
        => ContainsAny(body, "share their main phone number", "shared their contact", "shared your contact", "request to share your main phone number") ||
           (body.Contains("[INFO]", StringComparison.OrdinalIgnoreCase) && (HashNumberToken.IsMatch(body) || CommandToken.IsMatch(body)));

    private static string DescribeAutoStyle(IReadOnlyList<EditorChatSegment> segments)
    {
        Color[] colors = segments.Select(segment => segment.Color).Distinct().ToArray();
        if (colors.Length == 0) return "Blank";
        if (colors.Length > 1) return "Mixed";
        return FindPreset(colors[0])?.Name ?? "Automatic";
    }

    private static bool ContainsAny(string source, params string[] values)
        => values.Any(value => source.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool EqualsAny(string source, params string[] values)
        => values.Any(value => string.Equals(source, value, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<EditorChatSegment> Single(string text, Color color)
        => new[] { new EditorChatSegment(text, color) };

    private static IReadOnlyList<EditorChatSegment> Segments(params (string Text, Color Color)[] values)
        => values.Where(value => value.Text.Length > 0)
            .Select(value => new EditorChatSegment(value.Text, value.Color))
            .ToArray();
}
