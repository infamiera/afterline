using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Afterline.Services;
using Microsoft.Win32;

namespace Afterline;

internal sealed class ProfilePictureWindow : Window
{
    private readonly Image _previewImage;
    private readonly TextBlock _placeholder;
    private readonly Button _removeButton;
    private readonly TextBlock _statusText;

    public ProfilePictureWindow(Window owner)
    {
        Owner = owner;
        Title = "Afterline Profile Picture";
        Width = 430;
        Height = 430;
        MinWidth = 400;
        MinHeight = 400;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel();
        header.Children.Add(new TextBlock
        {
            Text = "Profile picture",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Choose a local image to use in the profile circle. You can crop, reposition and zoom it before saving.",
            Foreground = (Brush)FindResource("MutedText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        root.Children.Add(header);

        var card = new Border
        {
            Style = (Style)FindResource("CardStyle"),
            Padding = new Thickness(18)
        };
        var body = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var frame = new Border
        {
            Width = 132,
            Height = 132,
            CornerRadius = new CornerRadius(66),
            Background = (Brush)FindResource("Raised"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

        var avatar = new Grid();
        _placeholder = new TextBlock
        {
            Text = "\uE77B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 42,
            Foreground = (Brush)FindResource("MutedText"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _previewImage = new Image
        {
            Width = 122,
            Height = 122,
            Stretch = Stretch.UniformToFill,
            Clip = new EllipseGeometry(new Point(61, 61), 61, 61)
        };
        avatar.Children.Add(_placeholder);
        avatar.Children.Add(_previewImage);
        frame.Child = avatar;
        body.Children.Add(frame);

        _statusText = new TextBlock
        {
            Text = "Your profile picture is stored locally and is never uploaded by Afterline.",
            Foreground = (Brush)FindResource("MutedText"),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0),
            MaxWidth = 320
        };
        body.Children.Add(_statusText);

        card.Child = body;
        Grid.SetRow(card, 2);
        root.Children.Add(card);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var choose = new Button
        {
            Content = "Choose picture",
            Style = (Style)FindResource("PrimaryButton"),
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        choose.Click += (_, _) => ChoosePicture();
        footer.Children.Add(choose);

        _removeButton = new Button
        {
            Content = "Remove",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        _removeButton.Click += (_, _) => RemovePicture();
        footer.Children.Add(_removeButton);

        var close = new Button
        {
            Content = "Close",
            Padding = new Thickness(12, 7, 12, 7)
        };
        close.Click += (_, _) => Close();
        footer.Children.Add(close);

        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        ThemeService.ApplyWindow(this);
        RefreshPreview();
    }

    private void ChoosePicture()
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
            {
                RefreshPreview();
                _statusText.Text = "Profile picture saved locally.";
                _statusText.Foreground = (Brush)FindResource("Success");
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to open the selected profile picture.", ex);
            _statusText.Text = "That image could not be opened.";
            _statusText.Foreground = (Brush)FindResource("Warning");
        }
    }

    private void RemovePicture()
    {
        ProfilePictureService.Delete();
        RefreshPreview();
        _statusText.Text = "Profile picture removed.";
        _statusText.Foreground = (Brush)FindResource("MutedText");
    }

    private void RefreshPreview()
    {
        BitmapImage? picture = ProfilePictureService.Load();
        bool hasPicture = picture is not null;

        _previewImage.Source = picture;
        _previewImage.Visibility = hasPicture ? Visibility.Visible : Visibility.Collapsed;
        _placeholder.Visibility = hasPicture ? Visibility.Collapsed : Visibility.Visible;
        _removeButton.IsEnabled = hasPicture;
    }
}
