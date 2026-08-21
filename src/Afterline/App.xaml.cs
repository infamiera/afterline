using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Afterline;

public partial class App : System.Windows.Application
{
    private bool _visualBrandingApplied;
    private bool _loadedHandlerAttached;
    private bool _trayBrandingApplied;
    private System.Drawing.Icon? _trayBrandIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(TryApplyBranding));
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        TryApplyBranding();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayBrandIcon?.Dispose();
        _trayBrandIcon = null;
        base.OnExit(e);
    }

    private void TryApplyBranding()
    {
        if (MainWindow is not Afterline.MainWindow window)
            return;

        if (!window.IsLoaded)
        {
            if (!_loadedHandlerAttached)
            {
                _loadedHandlerAttached = true;
                window.Loaded += Window_Loaded;
            }
            return;
        }

        ApplyBranding(window);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Afterline.MainWindow window)
        {
            window.Loaded -= Window_Loaded;
            _loadedHandlerAttached = false;
            ApplyBranding(window);
        }
    }

    private void ApplyBranding(Afterline.MainWindow window)
    {
        if (!_visualBrandingApplied)
        {
            var iconUri = new Uri("pack://application:,,,/Assets/Afterline.ico", UriKind.Absolute);
            window.Icon = BitmapFrame.Create(iconUri);
            ReplaceSidebarBrand(window, iconUri);
            _visualBrandingApplied = true;
        }

        if (!_trayBrandingApplied)
            _trayBrandingApplied = TryApplyTrayIcon(window);
    }

    private static void ReplaceSidebarBrand(DependencyObject root, Uri iconUri)
    {
        TextBlock? brandText = FindBrandText(root);
        if (brandText?.Parent is not Panel parent)
            return;

        int index = parent.Children.IndexOf(brandText);
        if (index < 0)
            return;

        var image = new Image
        {
            Source = BitmapFrame.Create(iconUri),
            Width = 48,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Stretch = System.Windows.Media.Stretch.Uniform
        };

        Grid.SetRow(image, Grid.GetRow(brandText));
        Grid.SetColumn(image, Grid.GetColumn(brandText));
        Grid.SetRowSpan(image, Grid.GetRowSpan(brandText));
        Grid.SetColumnSpan(image, Grid.GetColumnSpan(brandText));

        parent.Children.RemoveAt(index);
        parent.Children.Insert(index, image);
    }

    private static TextBlock? FindBrandText(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is TextBlock textBlock && string.Equals(textBlock.Text, "AFTERLINE", StringComparison.Ordinal))
                return textBlock;

            if (child is DependencyObject dependencyObject)
            {
                TextBlock? result = FindBrandText(dependencyObject);
                if (result is not null)
                    return result;
            }
        }

        return null;
    }

    private bool TryApplyTrayIcon(Afterline.MainWindow window)
    {
        FieldInfo? trayField = typeof(Afterline.MainWindow).GetField("_trayIcon", BindingFlags.Instance | BindingFlags.NonPublic);
        if (trayField?.GetValue(window) is not Forms.NotifyIcon trayIcon)
            return false;

        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Afterline.Assets.AfterlineTray.ico");
        if (stream is null)
            return false;

        using var sourceIcon = new System.Drawing.Icon(stream);
        _trayBrandIcon?.Dispose();
        _trayBrandIcon = (System.Drawing.Icon)sourceIcon.Clone();
        trayIcon.Icon = _trayBrandIcon;
        return true;
    }
}
