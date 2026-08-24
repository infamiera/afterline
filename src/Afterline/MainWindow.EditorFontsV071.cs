using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private sealed record EditorFontChoiceV071(
        string Label,
        string FamilyStack,
        FontWeight Weight)
    {
        public override string ToString() => Label;
    }

    private static readonly IReadOnlyList<EditorFontChoiceV071> EditorFontChoicesV071 = new[]
    {
        new EditorFontChoiceV071("Arial Bold", "Arial, Helvetica, Segoe UI", FontWeights.Bold),
        new EditorFontChoiceV071("Arial", "Arial, Helvetica, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("Segoe UI Semibold", "Segoe UI", FontWeights.SemiBold),
        new EditorFontChoiceV071("Arial, Helvetica, sans-serif", "Arial, Helvetica, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("Georgia, serif", "Georgia, Times New Roman", FontWeights.Normal),
        new EditorFontChoiceV071("\"Palatino Linotype\", \"Book Antiqua\", Palatino, serif", "Palatino Linotype, Book Antiqua, Palatino, Georgia", FontWeights.Normal),
        new EditorFontChoiceV071("\"Times New Roman\", Times, serif", "Times New Roman, Times, Georgia", FontWeights.Normal),
        new EditorFontChoiceV071("\"Arial Black\", Gadget, sans-serif", "Arial Black, Arial, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Comic Sans MS\", cursive, sans-serif", "Comic Sans MS, Segoe Print, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("Impact, Charcoal, sans-serif", "Impact, Arial Black, Arial", FontWeights.Normal),
        new EditorFontChoiceV071("\"Lucida Sans Unicode\", \"Lucida Grande\", sans-serif", "Lucida Sans Unicode, Lucida Grande, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("Tahoma, Geneva, sans-serif", "Tahoma, Geneva, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Trebuchet MS\", Helvetica, sans-serif", "Trebuchet MS, Helvetica, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("Verdana, Geneva, sans-serif", "Verdana, Geneva, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Courier New\", Courier, monospace", "Courier New, Courier, Consolas", FontWeights.Normal),
        new EditorFontChoiceV071("\"Lucida Console\", Monaco, monospace", "Lucida Console, Monaco, Consolas", FontWeights.Normal),
        new EditorFontChoiceV071("Calibri, sans-serif", "Calibri, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Raleway\", sans-serif", "Raleway, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Roboto\", sans-serif", "Roboto, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Ubuntu\", sans-serif", "Ubuntu, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Mukta\", sans-serif", "Mukta, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Open Sans\", sans-serif", "Open Sans, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Nunito Sans\", sans-serif", "Nunito Sans, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("\"Inter\", sans-serif", "Inter, Segoe UI", FontWeights.Normal),
        // Keep the original short labels valid for saved 0.7.0 preferences.
        new EditorFontChoiceV071("Tahoma", "Tahoma, Geneva, Segoe UI", FontWeights.Normal),
        new EditorFontChoiceV071("Verdana", "Verdana, Geneva, Segoe UI", FontWeights.Normal)
    };

    private static void PopulateEditorFontBoxV071(ComboBox box)
    {
        box.Items.Clear();
        foreach (EditorFontChoiceV071 font in EditorFontChoicesV071)
            box.Items.Add(font);
    }

    private static EditorFontChoiceV071 ResolveEditorFontChoiceV071(string? selected)
        => EditorFontChoicesV071.FirstOrDefault(font =>
               string.Equals(font.Label, selected, StringComparison.OrdinalIgnoreCase))
           ?? EditorFontChoicesV071[0];
}
