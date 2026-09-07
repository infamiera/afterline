using System.Windows;
using System.Windows.Controls;
using Afterline.Services;

namespace Afterline;

internal sealed class ServerTimeZoneWindow : Window
{
    private readonly CheckBox _manualCheck;
    private readonly ComboBox _timeZoneBox;

    public string? SelectedTimeZoneId { get; private set; }

    public ServerTimeZoneWindow(
        Window owner,
        string serverName,
        string currentDescription,
        string? manualTimeZoneId)
    {
        Owner = owner;
        Title = "Server Time Zone";
        Width = 560;
        Height = 330;
        MinWidth = 480;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { Margin = new Thickness(22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var intro = new StackPanel();
        intro.Children.Add(new TextBlock
        {
            Text = serverName,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        intro.Children.Add(new TextBlock
        {
            Text = $"Current clock: {currentDescription}",
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedText")
        });
        root.Children.Add(intro);

        _manualCheck = new CheckBox
        {
            Content = "Override automatic detection for this server",
            IsChecked = !string.IsNullOrWhiteSpace(manualTimeZoneId)
        };
        _manualCheck.Checked += (_, _) => UpdateEnabledState();
        _manualCheck.Unchecked += (_, _) => UpdateEnabledState();
        Grid.SetRow(_manualCheck, 2);
        root.Children.Add(_manualCheck);

        _timeZoneBox = new ComboBox
        {
            ItemsSource = TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(zone => zone.BaseUtcOffset)
                .ThenBy(zone => zone.DisplayName)
                .ToArray(),
            DisplayMemberPath = nameof(TimeZoneInfo.DisplayName),
            MinWidth = 440,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        if (!string.IsNullOrWhiteSpace(manualTimeZoneId))
        {
            _timeZoneBox.SelectedItem = _timeZoneBox.Items
                .OfType<TimeZoneInfo>()
                .FirstOrDefault(zone => string.Equals(
                    zone.Id,
                    manualTimeZoneId,
                    StringComparison.OrdinalIgnoreCase));
        }
        _timeZoneBox.SelectedItem ??= _timeZoneBox.Items
            .OfType<TimeZoneInfo>()
            .FirstOrDefault(zone => string.Equals(
                zone.Id,
                TimeZoneInfo.Utc.Id,
                StringComparison.OrdinalIgnoreCase));
        _timeZoneBox.SelectedItem ??= _timeZoneBox.Items
            .OfType<TimeZoneInfo>()
            .FirstOrDefault();
        Grid.SetRow(_timeZoneBox, 4);
        root.Children.Add(_timeZoneBox);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancel.Click += (_, _) => Close();
        var save = new Button
        {
            Content = "Save",
            Padding = new Thickness(14, 7, 14, 7),
            Style = (Style)FindResource("PrimaryButton")
        };
        save.Click += (_, _) =>
        {
            SelectedTimeZoneId = _manualCheck.IsChecked == true
                ? (_timeZoneBox.SelectedItem as TimeZoneInfo)?.Id
                : null;
            DialogResult = true;
            Close();
        };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        Grid.SetRow(actions, 6);
        root.Children.Add(actions);

        Content = root;
        ThemeService.ApplyWindow(this);
        UpdateEnabledState();
    }

    private void UpdateEnabledState()
        => _timeZoneBox.IsEnabled = _manualCheck.IsChecked == true;
}
