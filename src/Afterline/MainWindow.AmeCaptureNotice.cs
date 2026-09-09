using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Afterline;

public partial class MainWindow
{
    private TextBlock CreateAmeCaptureNotice()
    {
        var notice = new TextBlock
        {
            Foreground = (Brush)FindResource("Warning"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 0)
        };
        notice.Inlines.Add(new Run("NOTE: /ame lines from other players currently cannot be displayed in chat. "));
        var link = new Hyperlink(new Run("Find out why"));
        link.SetResourceReference(Hyperlink.ForegroundProperty, "Accent");
        link.Click += (_, _) => new AmeCaptureInfoWindow(this).ShowDialog();
        notice.Inlines.Add(link);
        return notice;
    }
}
