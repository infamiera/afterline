using System.Windows;
using System.Windows.Markup;

namespace Afterline.Services;

public static class ThemeUiStyles
{
    private static bool _initialized;

    public static void Ensure()
    {
        if (_initialized || System.Windows.Application.Current is null) return;
        _initialized = true;

        const string xaml = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Style x:Key="AfterlineScrollThumb" TargetType="{x:Type Thumb}">
        <Setter Property="Background" Value="{DynamicResource AfterlineScrollbarThumb}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Thumb}">
                    <Border x:Name="ThumbRoot"
                            Background="{TemplateBinding Background}"
                            CornerRadius="4"
                            Margin="2,1"/>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="ThumbRoot" Property="Background" Value="{DynamicResource Accent}"/>
                        </Trigger>
                        <Trigger Property="IsDragging" Value="True">
                            <Setter TargetName="ThumbRoot" Property="Background" Value="{DynamicResource AccentHover}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <ControlTemplate x:Key="AfterlineVerticalScrollBar" TargetType="{x:Type ScrollBar}">
        <Grid Width="11" Background="{DynamicResource AfterlineScrollbarTrack}">
            <Track x:Name="PART_Track"
                   IsDirectionReversed="True"
                   Minimum="{TemplateBinding Minimum}"
                   Maximum="{TemplateBinding Maximum}"
                   Value="{TemplateBinding Value}"
                   ViewportSize="{TemplateBinding ViewportSize}">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command="{x:Static ScrollBar.PageUpCommand}"
                                  CommandTarget="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                                  Focusable="False"
                                  Opacity="0"/>
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                    <Thumb MinHeight="34" Style="{StaticResource AfterlineScrollThumb}"/>
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command="{x:Static ScrollBar.PageDownCommand}"
                                  CommandTarget="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                                  Focusable="False"
                                  Opacity="0"/>
                </Track.IncreaseRepeatButton>
            </Track>
        </Grid>
    </ControlTemplate>

    <ControlTemplate x:Key="AfterlineHorizontalScrollBar" TargetType="{x:Type ScrollBar}">
        <Grid Height="11" Background="{DynamicResource AfterlineScrollbarTrack}">
            <Track x:Name="PART_Track"
                   IsDirectionReversed="False"
                   Minimum="{TemplateBinding Minimum}"
                   Maximum="{TemplateBinding Maximum}"
                   Value="{TemplateBinding Value}"
                   ViewportSize="{TemplateBinding ViewportSize}">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command="{x:Static ScrollBar.PageLeftCommand}"
                                  CommandTarget="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                                  Focusable="False"
                                  Opacity="0"/>
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                    <Thumb MinWidth="34" Style="{StaticResource AfterlineScrollThumb}"/>
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command="{x:Static ScrollBar.PageRightCommand}"
                                  CommandTarget="{Binding RelativeSource={RelativeSource TemplatedParent}}"
                                  Focusable="False"
                                  Opacity="0"/>
                </Track.IncreaseRepeatButton>
            </Track>
        </Grid>
    </ControlTemplate>

    <Style TargetType="{x:Type ScrollBar}">
        <Setter Property="Background" Value="{DynamicResource AfterlineScrollbarTrack}"/>
        <Setter Property="Template" Value="{StaticResource AfterlineVerticalScrollBar}"/>
        <Style.Triggers>
            <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Template" Value="{StaticResource AfterlineHorizontalScrollBar}"/>
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style x:Key="AfterlineSidebarNavigationButton" TargetType="{x:Type Button}">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="BorderBrush" Value="Transparent"/>
        <Setter Property="BorderThickness" Value="2,0,0,0"/>
        <Setter Property="Foreground" Value="{DynamicResource AfterlineNavOverview}"/>
        <Setter Property="Padding" Value="8,7"/>
        <Setter Property="MinHeight" Value="35"/>
        <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
        <Setter Property="Cursor" Value="Hand"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Button}">
                    <Grid Background="Transparent">
                        <Border x:Name="ActiveRail"
                                Width="2"
                                HorizontalAlignment="Left"
                                Background="{TemplateBinding BorderBrush}"
                                CornerRadius="1"/>
                        <Grid Margin="{TemplateBinding Padding}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="23"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Text="{TemplateBinding Tag}"
                                       FontFamily="Segoe MDL2 Assets"
                                       FontSize="16"
                                       Foreground="{TemplateBinding Foreground}"
                                       VerticalAlignment="Center"/>
                            <ContentPresenter x:Name="Label"
                                              Grid.Column="1"
                                              HorizontalAlignment="Left"
                                              VerticalAlignment="Center"
                                              TextElement.Foreground="{DynamicResource MutedText}"/>
                        </Grid>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Label" Property="TextElement.Foreground" Value="{DynamicResource Text}"/>
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter Property="Opacity" Value="0.72"/>
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Opacity" Value="0.42"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType="{x:Type ToolTip}">
        <Setter Property="Background" Value="{DynamicResource Raised}"/>
        <Setter Property="Foreground" Value="{DynamicResource Text}"/>
        <Setter Property="BorderBrush" Value="{DynamicResource Border}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="10,7"/>
        <Setter Property="MaxWidth" Value="440"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type ToolTip}">
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="7"
                            SnapsToDevicePixels="True">
                        <ContentPresenter Margin="{TemplateBinding Padding}"
                                          Content="{TemplateBinding Content}"
                                          ContentTemplate="{TemplateBinding ContentTemplate}"
                                          ContentTemplateSelector="{TemplateBinding ContentTemplateSelector}"
                                          TextElement.Foreground="{TemplateBinding Foreground}"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>
""";

        try
        {
            if (XamlReader.Parse(xaml) is ResourceDictionary resources)
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(resources);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Error("Unable to install themed scrollbar and tooltip styles.", ex);
        }
    }
}
