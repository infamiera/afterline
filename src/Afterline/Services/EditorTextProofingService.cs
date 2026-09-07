using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Afterline.Services;

internal static partial class EditorTextProofingService
{
    private sealed record GrammarIssue(int Start, int Length, string Replacement);

    private static readonly HashSet<TextBox> Attached = new();
    private static readonly HashSet<string> YourPredicateFollowers = new(StringComparer.OrdinalIgnoreCase)
    {
        "able", "allowed", "amazing", "awesome", "back", "being", "coming", "correct",
        "doing", "done", "going", "gonna", "great", "here", "kidding", "late", "leaving",
        "lying", "next", "not", "okay", "ready", "right", "serious", "sure", "talking",
        "there", "trying", "welcome", "wrong"
    };

    public static void Attach(TextBox textBox)
    {
        if (!Attached.Add(textBox)) return;

        textBox.Language = XmlLanguage.GetLanguage("en-US");
        SpellCheck.SetIsEnabled(textBox, true);
        TryAttachPersonalDictionary(textBox);
        textBox.ContextMenu ??= new ContextMenu();

        GrammarAdorner? adorner = null;
        void RefreshGrammar()
        {
            adorner?.SetIssues(FindGrammarIssues(textBox.Text));
        }

        textBox.Loaded += (_, _) =>
        {
            AdornerLayer? layer = AdornerLayer.GetAdornerLayer(textBox);
            if (layer is null || adorner is not null) return;
            adorner = new GrammarAdorner(textBox);
            layer.Add(adorner);
            RefreshGrammar();
        };
        textBox.Unloaded += (_, _) =>
        {
            if (adorner is not null)
                AdornerLayer.GetAdornerLayer(textBox)?.Remove(adorner);
            adorner = null;
        };
        textBox.TextChanged += (_, _) => RefreshGrammar();
        textBox.LayoutUpdated += (_, _) => adorner?.InvalidateVisual();
        textBox.PreviewMouseRightButtonDown += (_, e) =>
        {
            int index = textBox.GetCharacterIndexFromPoint(e.GetPosition(textBox), true);
            if (index >= 0) textBox.CaretIndex = index;
        };
        textBox.ContextMenuOpening += (_, _) => BuildContextMenu(textBox);
    }

    private static void BuildContextMenu(TextBox textBox)
    {
        ContextMenu menu = textBox.ContextMenu ??= new ContextMenu();
        menu.Items.Clear();
        menu.SetResourceReference(Control.BackgroundProperty, "Raised");
        menu.SetResourceReference(Control.ForegroundProperty, "Text");
        menu.SetResourceReference(Control.BorderBrushProperty, "Border");

        int caret = Math.Clamp(textBox.CaretIndex, 0, textBox.Text.Length);
        GrammarIssue? grammar = FindGrammarIssues(textBox.Text)
            .FirstOrDefault(issue => caret >= issue.Start && caret <= issue.Start + issue.Length);
        if (grammar is not null)
        {
            AddAction(menu, $"Replace with “{grammar.Replacement}”", () =>
                ReplaceRange(textBox, grammar.Start, grammar.Length, grammar.Replacement));
            menu.Items.Add(new Separator());
        }

        int spellingIndex = FindSpellingIndex(textBox, caret);
        SpellingError? spelling = spellingIndex >= 0 ? textBox.GetSpellingError(spellingIndex) : null;
        if (spelling is not null)
        {
            string[] suggestions = spelling.Suggestions.Take(6).ToArray();
            foreach (string suggestion in suggestions)
            {
                string captured = suggestion;
                AddAction(menu, captured, () => spelling.Correct(captured));
            }
            if (suggestions.Length == 0)
                menu.Items.Add(new MenuItem { Header = "No spelling suggestions", IsEnabled = false });

            (int start, int length, string word) = WordAt(textBox.Text, spellingIndex);
            menu.Items.Add(new Separator());
            AddAction(menu, "Ignore this word", spelling.IgnoreAll);
            AddAction(menu, "Add to personal dictionary", () => AddToDictionary(textBox, word));
            menu.Items.Add(new Separator());
        }

        AddAction(menu, "Cut", textBox.Cut, textBox.SelectionLength > 0);
        AddAction(menu, "Copy", textBox.Copy, textBox.SelectionLength > 0);
        AddAction(menu, "Paste", textBox.Paste, Clipboard.ContainsText());
        menu.Items.Add(new Separator());
        AddAction(menu, "Select all", textBox.SelectAll, textBox.Text.Length > 0);
    }

    private static void AddAction(ItemsControl menu, string label, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = label, IsEnabled = enabled };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private static int FindSpellingIndex(TextBox textBox, int caret)
    {
        if (textBox.Text.Length == 0) return -1;
        foreach (int candidate in new[] { caret, caret - 1 }.Where(index => index >= 0 && index < textBox.Text.Length))
        {
            if (textBox.GetSpellingError(candidate) is not null)
                return candidate;
        }
        return -1;
    }

    private static void ReplaceRange(TextBox textBox, int start, int length, string replacement)
    {
        textBox.Select(start, length);
        textBox.SelectedText = replacement;
        textBox.CaretIndex = start + replacement.Length;
    }

    private static void TryAttachPersonalDictionary(TextBox textBox)
    {
        try
        {
            string path = AppPaths.EditorDictionaryFile;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path)) File.WriteAllText(path, string.Empty, Encoding.Unicode);
            Uri uri = new(path, UriKind.Absolute);
            if (!textBox.SpellCheck.CustomDictionaries.Contains(uri))
                textBox.SpellCheck.CustomDictionaries.Add(uri);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to load the Editor personal spelling dictionary.", ex);
        }
    }

    private static void AddToDictionary(TextBox textBox, string word)
    {
        word = word.Trim();
        if (word.Length == 0) return;
        try
        {
            string path = AppPaths.EditorDictionaryFile;
            string[] existing = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            if (!existing.Contains(word, StringComparer.OrdinalIgnoreCase))
                File.AppendAllText(path, word + Environment.NewLine, Encoding.Unicode);

            var dictionaries = textBox.SpellCheck.CustomDictionaries;
            Uri uri = new(path, UriKind.Absolute);
            dictionaries.Remove(uri);
            dictionaries.Add(uri);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to save a word to the Editor personal spelling dictionary.", ex);
        }
    }

    private static (int Start, int Length, string Word) WordAt(string text, int index)
    {
        if (text.Length == 0) return (0, 0, string.Empty);
        index = Math.Clamp(index, 0, text.Length - 1);
        bool IsWord(char value) => char.IsLetter(value) || value is '\'' or '’' or '-';
        int start = index;
        while (start > 0 && IsWord(text[start - 1])) start--;
        int end = index;
        while (end < text.Length && IsWord(text[end])) end++;
        return (start, end - start, text[start..end]);
    }

    private static IReadOnlyList<GrammarIssue> FindGrammarIssues(string text)
    {
        var issues = new List<GrammarIssue>();
        MatchCollection words = WordRegex().Matches(text);
        for (int i = 0; i < words.Count; i++)
        {
            Match word = words[i];
            if (word.Value.Equals("your", StringComparison.OrdinalIgnoreCase) && i + 1 < words.Count &&
                YourPredicateFollowers.Contains(words[i + 1].Value))
            {
                issues.Add(new GrammarIssue(word.Index, word.Length, MatchCase(word.Value, "you're")));
            }
        }
        return issues;
    }

    internal static void RunSmokeTest()
    {
        IReadOnlyList<GrammarIssue> mistaken = FindGrammarIssues("Bianca says: your going the wrong way.");
        if (mistaken.Count != 1 || mistaken[0].Replacement != "you're")
            throw new InvalidOperationException("Editor contextual proofing did not recognize a high-confidence your/you're error.");

        if (FindGrammarIssues("Bianca says: your car is over there.").Count != 0)
            throw new InvalidOperationException("Editor contextual proofing incorrectly marked a possessive ‘your’. ");
    }

    private static string MatchCase(string source, string replacement)
        => source.All(char.IsUpper)
            ? replacement.ToUpper(CultureInfo.InvariantCulture)
            : char.IsUpper(source[0])
                ? char.ToUpperInvariant(replacement[0]) + replacement[1..]
                : replacement;

    [GeneratedRegex(@"[\p{L}]+(?:['’][\p{L}]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    private sealed class GrammarAdorner(TextBox adornedElement) : Adorner(adornedElement)
    {
        private IReadOnlyList<GrammarIssue> _issues = Array.Empty<GrammarIssue>();
        private readonly Pen _pen = new(Brushes.Red, 1.2);

        public void SetIssues(IReadOnlyList<GrammarIssue> issues)
        {
            _issues = issues;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            foreach (GrammarIssue issue in _issues)
            {
                int end = Math.Min(adornedElement.Text.Length, issue.Start + issue.Length);
                for (int index = issue.Start; index < end; index++)
                {
                    Rect left = adornedElement.GetRectFromCharacterIndex(index, true);
                    Rect right = adornedElement.GetRectFromCharacterIndex(index + 1, true);
                    if (left.IsEmpty || right.IsEmpty || Math.Abs(left.Bottom - right.Bottom) > 2) continue;
                    double y = left.Bottom - 1;
                    var wave = new StreamGeometry();
                    using StreamGeometryContext context = wave.Open();
                    context.BeginFigure(new Point(left.Left, y), false, false);
                    double x = left.Left;
                    bool down = true;
                    while (x < right.Left)
                    {
                        x = Math.Min(x + 2, right.Left);
                        context.LineTo(new Point(x, y + (down ? 1.5 : 0)), true, false);
                        down = !down;
                    }
                    wave.Freeze();
                    drawingContext.DrawGeometry(null, _pen, wave);
                }
            }
        }
    }
}
