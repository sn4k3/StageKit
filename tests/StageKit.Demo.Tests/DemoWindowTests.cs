using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using StageKit.Demo;
using Xunit;

namespace StageKit.Demo.Tests;

public class DemoWindowTests
{
    private static readonly object AvaloniaLock = new();
    private static bool _isAvaloniaInitialized;

    [Fact]
    public void MainWindow_Startup_ContainsTheFourFeatureTabs()
    {
        EnsureAvalonia();

        var window = new MainWindow();
        try
        {
            var tabs = window.FindControl<TabControl>("FeatureTabs");

            Assert.NotNull(tabs);
            Assert.Equal(4, tabs.ItemCount);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void MainWindow_Startup_ExposesSettingsFolderAndCrashRoundTripActions()
    {
        EnsureAvalonia();

        var window = new MainWindow();
        try
        {
            Assert.NotNull(window.FindControl<Button>("OpenSettingsDirectoryButton"));
            Assert.NotNull(window.FindControl<ComboBox>("ThemePreferenceComboBox"));
            Assert.NotNull(window.FindControl<Button>("OpenProfileDirectoryButton"));
            Assert.NotNull(window.FindControl<CheckBox>("AutoInstallUpdatesCheckBox"));
            Assert.NotNull(window.FindControl<Button>("InstallUpdateButton"));
            Assert.NotNull(window.FindControl<Button>("ThrowFatalExceptionButton"));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void MainWindow_ThemePreference_UpdatesApplicationThemeVariant()
    {
        EnsureAvalonia();

        var window = new MainWindow();
        var viewModel = Assert.IsType<MainWindowViewModel>(window.DataContext);
        var originalTheme = viewModel.Settings.Theme;
        try
        {
            viewModel.Settings.Theme = "Dark";
            Assert.Equal(ThemeVariant.Dark, Application.Current?.RequestedThemeVariant);

            viewModel.Settings.Theme = "Light";
            Assert.Equal(ThemeVariant.Light, Application.Current?.RequestedThemeVariant);

            viewModel.Settings.Theme = "System";
            Assert.Equal(ThemeVariant.Default, Application.Current?.RequestedThemeVariant);
        }
        finally
        {
            viewModel.Settings.Theme = originalTheme;
            window.Close();
        }
    }

    [Theory]
    [InlineData("DemoPrimaryButtonBackgroundBrush")]
    [InlineData("DemoPrimaryButtonHoverBrush")]
    [InlineData("DemoPrimaryButtonPressedBrush")]
    [InlineData("DemoPrimaryButtonForegroundBrush")]
    [InlineData("DemoDangerButtonBackgroundBrush")]
    [InlineData("DemoDangerButtonHoverBrush")]
    [InlineData("DemoDangerButtonPressedBrush")]
    [InlineData("DemoDangerButtonForegroundBrush")]
    [InlineData("DemoDisabledBackgroundBrush")]
    [InlineData("DemoDisabledForegroundBrush")]
    [InlineData("DemoPageBackgroundBrush")]
    [InlineData("DemoCardBackgroundBrush")]
    [InlineData("DemoCardBorderBrush")]
    [InlineData("DemoPrimaryTextBrush")]
    [InlineData("DemoSecondaryTextBrush")]
    [InlineData("DemoChipBackgroundBrush")]
    public void AppTheme_DarkVariant_ProvidesEveryCustomBrush(string resourceKey)
    {
        EnsureAvalonia();

        var application = Assert.IsType<App>(Application.Current);

        Assert.True(application.TryGetResource(resourceKey, ThemeVariant.Dark, out var resource));
        Assert.IsAssignableFrom<IBrush>(resource);
    }

    [Theory]
    [InlineData("Light", "DemoPrimaryButtonBackgroundBrush", "DemoPrimaryButtonForegroundBrush")]
    [InlineData("Dark", "DemoPrimaryButtonBackgroundBrush", "DemoPrimaryButtonForegroundBrush")]
    [InlineData("Light", "DemoDangerButtonBackgroundBrush", "DemoDangerButtonForegroundBrush")]
    [InlineData("Dark", "DemoDangerButtonBackgroundBrush", "DemoDangerButtonForegroundBrush")]
    public void AppTheme_ActionButtonColors_HaveReadableContrast(
        string themeName,
        string backgroundResourceKey,
        string foregroundResourceKey)
    {
        EnsureAvalonia();

        var application = Assert.IsType<App>(Application.Current);
        var theme = themeName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        Assert.True(application.TryGetResource(backgroundResourceKey, theme, out var background));
        Assert.True(application.TryGetResource(foregroundResourceKey, theme, out var foreground));

        var backgroundColor = Assert.IsType<SolidColorBrush>(background).Color;
        var foregroundColor = Assert.IsType<SolidColorBrush>(foreground).Color;

        Assert.True(GetContrastRatio(backgroundColor, foregroundColor) >= 4.5);
    }

    private static double GetContrastRatio(Color first, Color second)
    {
        var firstLuminance = GetRelativeLuminance(first);
        var secondLuminance = GetRelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) /
               (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double GetRelativeLuminance(Color color)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linearize(color.R) +
               0.7152 * Linearize(color.G) +
               0.0722 * Linearize(color.B);
    }

    private static void EnsureAvalonia()
    {
        lock (AvaloniaLock)
        {
            if (_isAvaloniaInitialized) return;

            AppBuilder.Configure<App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();
            _isAvaloniaInitialized = true;
        }
    }
}
