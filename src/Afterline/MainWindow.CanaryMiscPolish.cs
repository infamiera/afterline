using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private bool _canaryMiscPolishInitialized;

    private void EnsureCanaryMiscPolish()
    {
        if (_canaryMiscPolishInitialized) return;
        _canaryMiscPolishInitialized = true;

        MoveCanaryEditorGuidesOutsideComposition();
        ApplyThemeCompliantTooltipsCanary();
        CompactFooterCanary();
        GuardDefaultEditorSizingCanary();
    }

    private void ApplyThemeCompliantTooltipsCanary()
    {
        var style = new Style(typeof(ToolTip));
        style.Setters.Add(new Setter(Control.BackgroundProperty, FindResource("Raised")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, FindResource("Text")));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, FindResource("Border")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 6, 9, 6)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
        style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 360.0));
        Application.Current.Resources[typeof(ToolTip)] = style;

        if (_editorPage is not null)
        {
            foreach (Button button in FindVisualChildrenCanary<Button>(_editorPage))
            {
                ToolTipService.SetInitialShowDelay(button, 350);
                ToolTipService.SetShowDuration(button, 12000);
            }
        }
    }

    private void CompactFooterCanary()
    {
        if (BottomStatusText.Parent is not Grid footer) return;

        footer.MinHeight = 26;
        footer.VerticalAlignment = VerticalAlignment.Center;
        BottomStatusText.FontSize = 10.5;
        BottomStatusText.VerticalAlignment = VerticalAlignment.Center;

        foreach (TextBlock text in footer.Children.OfType<TextBlock>())
        {
            text.FontSize = Math.Min(text.FontSize <= 0 ? 11 : text.FontSize, 10.5);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(0);
        }

        foreach (Button button in footer.Children.OfType<Button>())
        {
            button.Width = 28;
            button.Height = 26;
            button.MinHeight = 0;
            button.Padding = new Thickness(0);
            button.Margin = new Thickness(4, 0, 0, 0);
            button.VerticalAlignment = VerticalAlignment.Center;
        }

        if (footer.Parent is Grid main && main.RowDefinitions.Count > 3)
            main.RowDefinitions[3].Height = new GridLength(8);
    }

    private void GuardDefaultEditorSizingCanary()
    {
        SizeChanged += (_, _) =>
        {
            if (_editorPage?.Visibility != Visibility.Visible ||
                _editorToolPanelColumn is null ||
                _editorToolPanelHost?.Visibility != Visibility.Visible)
                return;

            double maxPanel = Math.Clamp(ActualWidth - 720, 220, 620);
            _editorToolPanelColumn.MaxWidth = maxPanel;
            if (_editorToolPanelColumn.Width.IsAbsolute && _editorToolPanelColumn.Width.Value > maxPanel)
                _editorToolPanelColumn.Width = new GridLength(maxPanel);
        };
    }
}
