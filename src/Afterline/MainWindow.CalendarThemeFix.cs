using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Afterline;

public partial class MainWindow
{
    private bool _darkSearchCalendarsInitialized;

    private void EnsureDarkSearchCalendarPopups()
    {
        if (_darkSearchCalendarsInitialized) return;
        _darkSearchCalendarsInitialized = true;

        AttachDarkCalendar(_searchFromDatePicker);
        AttachDarkCalendar(_searchToDatePicker);
    }

    private void AttachDarkCalendar(DatePicker? picker)
    {
        if (picker is null) return;
        picker.CalendarOpened += (_, _) => ApplyDarkCalendarPopup(picker);
    }

    private void ApplyDarkCalendarPopup(DatePicker picker)
    {
        picker.ApplyTemplate();
        if (picker.Template.FindName("PART_Popup", picker) is not Popup popup) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (popup.Child is not DependencyObject popupRoot) return;
            Calendar? calendar = FindVisualDescendant<Calendar>(popupRoot);
            if (calendar is null) return;

            Brush raised = (Brush)FindResource("Raised");
            Brush panel = (Brush)FindResource("Panel");
            Brush text = (Brush)FindResource("Text");
            Brush muted = (Brush)FindResource("MutedText");
            Brush border = (Brush)FindResource("Border");
            Brush accent = (Brush)FindResource("Accent");

            calendar.Background = raised;
            calendar.Foreground = text;
            calendar.BorderBrush = border;
            calendar.BorderThickness = new Thickness(1);

            // The stock WPF Calendar template relies heavily on SystemColors.
            // Override those resources locally so the popup follows Afterline's dark palette.
            calendar.Resources[SystemColors.WindowBrushKey] = raised;
            calendar.Resources[SystemColors.WindowTextBrushKey] = text;
            calendar.Resources[SystemColors.ControlBrushKey] = raised;
            calendar.Resources[SystemColors.ControlTextBrushKey] = text;
            calendar.Resources[SystemColors.MenuBrushKey] = raised;
            calendar.Resources[SystemColors.MenuTextBrushKey] = text;
            calendar.Resources[SystemColors.HighlightBrushKey] = accent;
            calendar.Resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(Color.FromRgb(0x08, 0x11, 0x1D));
            calendar.Resources[SystemColors.GrayTextBrushKey] = muted;
            calendar.Resources[SystemColors.ActiveBorderBrushKey] = border;
            calendar.Resources[SystemColors.InactiveBorderBrushKey] = border;

            calendar.CalendarDayButtonStyle = CreateDarkCalendarDayButtonStyle(text, muted, accent);
            calendar.CalendarButtonStyle = CreateDarkCalendarButtonStyle(text, muted, panel);

            calendar.ApplyTemplate();
            calendar.UpdateLayout();
        }));
    }

    private Style CreateDarkCalendarDayButtonStyle(Brush text, Brush muted, Brush accent)
    {
        var style = new Style(typeof(CalendarDayButton));
        style.Setters.Add(new Setter(Control.ForegroundProperty, text));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5, 4, 5, 4)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(1)));

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x29, 0x3B, 0x50))));
        hover.Setters.Add(new Setter(Control.BorderBrushProperty, accent));
        style.Triggers.Add(hover);

        var selected = new Trigger { Property = CalendarDayButton.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, accent));
        selected.Setters.Add(new Setter(Control.BorderBrushProperty, accent));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x08, 0x11, 0x1D))));
        style.Triggers.Add(selected);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Control.ForegroundProperty, muted));
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55));
        style.Triggers.Add(disabled);
        return style;
    }

    private Style CreateDarkCalendarButtonStyle(Brush text, Brush muted, Brush panel)
    {
        var style = new Style(typeof(CalendarButton));
        style.Setters.Add(new Setter(Control.ForegroundProperty, text));
        style.Setters.Add(new Setter(Control.BackgroundProperty, panel));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 5, 6, 5)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(1)));

        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x29, 0x3B, 0x50))));
        style.Triggers.Add(hover);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Control.ForegroundProperty, muted));
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55));
        style.Triggers.Add(disabled);
        return style;
    }

    private static T? FindVisualDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T typed) return typed;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            T? match = FindVisualDescendant<T>(child);
            if (match is not null) return match;
        }
        return null;
    }
}
