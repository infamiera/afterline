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
        <Setter Property="Background" Value="{DynamicResource Border}"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Thumb}">
                    <Border x:Name="ThumbRoot"
                            Background="{TemplateBinding Background}"
                            CornerRadius="5"
                            Margin="2"/>
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
        <Grid Width="11" Background="{DynamicResource Raised}">
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
                    <Thumb MinHeight="26" Style="{StaticResource AfterlineScrollThumb}"/>
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
        <Grid Height="11" Background="{DynamicResource Raised}">
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
                    <Thumb MinWidth="26" Style="{StaticResource AfterlineScrollThumb}"/>
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
        <Setter Property="Background" Value="{DynamicResource Raised}"/>
        <Setter Property="Template" Value="{StaticResource AfterlineVerticalScrollBar}"/>
        <Style.Triggers>
            <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Template" Value="{StaticResource AfterlineHorizontalScrollBar}"/>
            </Trigger>
        </Style.Triggers>
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
