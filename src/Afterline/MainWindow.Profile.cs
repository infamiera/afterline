using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;

namespace Afterline;

public partial class MainWindow
{
    private bool _profileUiInitialized;
    private Button? _profileHeaderButton;
    private Image? _profileHeaderImage;
    private TextBlock? _profileHeaderPlaceholder;

    private void EnsureProfileUi()
    {
        if (_profileUiInitialized) return;
        _profileUiInitialized = true;

        AddProfileHeaderButton();
        RefreshProfilePicture();
    }

    private void AddProfileHeaderButton()
    {
        if (TopStatusText.Parent is not StackPanel statusStack ||
            statusStack.Parent is not Border statusBorder ||
            statusBorder.Parent is not Grid headerGrid)
            return;

        while (headerGrid.ColumnDefinitions.Count < 3)
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(statusBorder, 1);

        _profileHeaderImage = new Image
        {
            Width = 30,
            Height = 30,
            Stretch = Stretch.UniformToFill,
            Clip = new EllipseGeometry(new Point(15, 15), 15, 15)
        };

        _profileHeaderPlaceholder = new TextBlock
        {
            Text = "\uE77B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("MutedText")
        };

        var avatar = new Grid
        {
            Width = 30,
            Height = 30
        };
        avatar.Children.Add(_profileHeaderPlaceholder);
        avatar.Children.Add(_profileHeaderImage);

        _profileHeaderButton = new Button
        {
            Content = avatar,
            Width = 38,
            Height = 38,
            Padding = new Thickness(3),
            Margin = new Thickness(10, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Profile picture"
        };
        _profileHeaderButton.Click += (_, _) => OpenProfilePictureWindow();

        Grid.SetColumn(_profileHeaderButton, 2);
        headerGrid.Children.Add(_profileHeaderButton);
    }

    private void OpenProfilePictureWindow()
    {
        var window = new ProfilePictureWindow(this);
        window.ShowDialog();
        RefreshProfilePicture();
    }

    private void RefreshProfilePicture()
    {
        BitmapImage? picture = ProfilePictureService.Load();
        bool hasPicture = picture is not null;

        if (_profileHeaderImage is not null)
        {
            _profileHeaderImage.Source = picture;
            _profileHeaderImage.Visibility = hasPicture ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_profileHeaderPlaceholder is not null)
            _profileHeaderPlaceholder.Visibility = hasPicture ? Visibility.Collapsed : Visibility.Visible;
    }
}
