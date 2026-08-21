using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;
using Microsoft.Win32;

namespace Afterline;

public partial class MainWindow
{
    private bool _profileUiInitialized;
    private Button? _profileHeaderButton;
    private Border? _profileSettingsCard;
    private Image? _profileHeaderImage;
    private TextBlock? _profileHeaderPlaceholder;
    private Image? _profileSettingsImage;
    private TextBlock? _profileSettingsPlaceholder;
    private Button? _removeProfilePictureButton;

    private void EnsureProfileUi()
    {
        if (_profileUiInitialized) return;
        _profileUiInitialized = true;

        AddProfileHeaderButton();
        AddProfileSettingsCard();
        RefreshProfilePictures();
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
            ToolTip = "Profile picture and settings"
        };
        _profileHeaderButton.Click += (_, _) => OpenSettingsFromProfile();

        Grid.SetColumn(_profileHeaderButton, 2);
        headerGrid.Children.Add(_profileHeaderButton);
    }

    private void AddProfileSettingsCard()
    {
        StackPanel? settingsStack = FindSettingsStackPanel();
        if (settingsStack is null || settingsStack.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "AfterlineProfileCard")))
            return;

        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _profileSettingsImage = new Image
        {
            Width = 66,
            Height = 66,
            Stretch = Stretch.UniformToFill,
            Clip = new EllipseGeometry(new Point(33, 33), 33, 33)
        };

        _profileSettingsPlaceholder = new TextBlock
        {
            Text = "\uE77B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("MutedText")
        };

        var avatarFrame = new Border
        {
            Width = 70,
            Height = 70,
            CornerRadius = new CornerRadius(35),
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2)
        };
        var avatar = new Grid();
        avatar.Children.Add(_profileSettingsPlaceholder);
        avatar.Children.Add(_profileSettingsImage);
        avatarFrame.Child = avatar;
        content.Children.Add(avatarFrame);

        var description = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        description.Children.Add(new TextBlock
        {
            Text = "Profile picture",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });
        description.Children.Add(new TextBlock
        {
            Text = "Choose a local image for the profile button. You can crop, reposition and zoom it before saving.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 18, 0)
        });
        description.Children.Add(new TextBlock
        {
            Text = "The picture is stored locally and is not uploaded anywhere.",
            Foreground = (Brush)FindResource("MutedText"),
            FontSize = 11,
            Margin = new Thickness(0, 4, 18, 0)
        });
        Grid.SetColumn(description, 2);
        content.Children.Add(description);

        var actions = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        var change = new Button
        {
            Content = "Choose picture",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 0, 8)
        };
        change.Click += (_, _) => ChooseProfilePicture();
        actions.Children.Add(change);

        _removeProfilePictureButton = new Button
        {
            Content = "Remove",
            Padding = new Thickness(12, 7, 12, 7)
        };
        _removeProfilePictureButton.Click += (_, _) => RemoveProfilePicture();
        actions.Children.Add(_removeProfilePictureButton);
        Grid.SetColumn(actions, 3);
        content.Children.Add(actions);

        _profileSettingsCard = new Border
        {
            Tag = "AfterlineProfileCard",
            Style = (Style)FindResource("CardStyle"),
            Margin = new Thickness(0, 0, 0, 14),
            Child = content
        };

        settingsStack.Children.Insert(0, _profileSettingsCard);
    }

    private void OpenSettingsFromProfile()
    {
        SettingsNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        _ = Dispatcher.BeginInvoke(new Action(() => _profileSettingsCard?.BringIntoView()));
    }

    private void ChooseProfilePicture()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose profile picture",
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|PNG|*.png|JPEG|*.jpg;*.jpeg|Bitmap|*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var editor = new ProfilePictureEditorWindow(this, dialog.FileName);
            if (editor.ShowDialog() == true && editor.Saved)
                RefreshProfilePictures();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open the selected profile picture.", ex);
            MessageBox.Show(this, "That image could not be opened.", "Afterline", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemoveProfilePicture()
    {
        ProfilePictureService.Delete();
        RefreshProfilePictures();
    }

    private void RefreshProfilePictures()
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

        if (_profileSettingsImage is not null)
        {
            _profileSettingsImage.Source = picture;
            _profileSettingsImage.Visibility = hasPicture ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_profileSettingsPlaceholder is not null)
            _profileSettingsPlaceholder.Visibility = hasPicture ? Visibility.Collapsed : Visibility.Visible;

        if (_removeProfilePictureButton is not null)
            _removeProfilePictureButton.IsEnabled = hasPicture;
    }
}
