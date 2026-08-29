using Avalonia.Styling;

namespace StageKit.Demo;

public static class DemoThemeOptions
{
    public static IReadOnlyList<string> Values { get; } = ["System", "Light", "Dark"];

    public static ThemeVariant Resolve(string? value)
    {
        return value?.Trim() switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }
}
