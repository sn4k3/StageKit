using StageKit.Demo;
using StageKit.Runtime;
using Avalonia.Styling;
using Xunit;

namespace StageKit.Demo.Tests;

public class DemoPresentationTests
{
    [Fact]
    public void FeatureCatalog_MainWorkflows_ExposesFourDistinctModules()
    {
        var modules = DemoFeatureCatalog.All;

        Assert.Collection(modules,
            module =>
            {
                Assert.Equal("Runtime", module.Name);
                Assert.Contains("HostSystem", module.APIs);
                Assert.Contains("ProcessHelper", module.APIs);
            },
            module => Assert.Equal("Settings", module.Name),
            module => Assert.Equal("Storage", module.Name),
            module => Assert.Equal("Updates", module.Name));
        Assert.Equal(modules.Count, modules.Select(module => module.Name).Distinct().Count());
    }

    [Fact]
    public void CreateUpdater_DefaultConfiguration_TargetsUvtoolsSafely()
    {
        using var updater = DemoUpdateManager.Create();

        Assert.Equal("sn4k3", updater.Owner);
        Assert.Equal("UVtools", updater.Repository);
        Assert.StartsWith("^UVtools_", updater.AssetRegexPattern);
        Assert.Contains(EntryApplication.GenericRuntimeIdentifier, updater.AssetRegexPattern);
        Assert.True(updater.RequireAssetChecksum);
        Assert.False(updater.AllowPreReleases);
    }

    [Fact]
    public void ThemeOptions_ExposeSystemLightAndDarkChoices()
    {
        Assert.Equal(["System", "Light", "Dark"], DemoThemeOptions.Values);
    }

    [Theory]
    [InlineData("System", "Default")]
    [InlineData("Light", "Light")]
    [InlineData("Dark", "Dark")]
    [InlineData("unexpected", "Default")]
    public void ThemeOptions_ResolveKnownAndUnknownValues(string value, string expected)
    {
        var variant = DemoThemeOptions.Resolve(value);

        Assert.Equal(expected, variant.ToString());
    }

    [Theory]
    [InlineData(false, 42, "System.InvalidOperationException: Demo crash")]
    [InlineData(true, 0, "System.InvalidOperationException: Demo crash")]
    [InlineData(true, 42, null)]
    public void CreateCrashPresentation_IncompleteCrashContext_ReturnsNull(
        bool hasCrashReportFlag,
        long crashReportIndex,
        string? crashReport)
    {
        var result = DemoCrashPresentation.Create(
            hasCrashReportFlag,
            crashReportIndex,
            crashReport);

        Assert.Null(result);
    }

    [Fact]
    public void CreateCrashPresentation_CompleteCrashContext_ReturnsReadableReport()
    {
        var result = DemoCrashPresentation.Create(
            true,
            42,
            "System.InvalidOperationException: Intentional StageKit demo crash.");

        Assert.NotNull(result);
        Assert.Equal(42, result.ReportId);
        Assert.Contains("Intentional StageKit demo crash", result.ReportText);
    }

    [Theory]
    [InlineData(0, 0, 0, "0.00 MB / 0.00 MB (0%)")]
    [InlineData(5.25, 10.5, 50, "5.25 MB / 10.50 MB (50%)")]
    [InlineData(12, 10, 120, "12.00 MB / 10.00 MB (100%)")]
    public void FormatDownloadProgress_Values_ReturnsStableBoundedText(
        double downloadedMegabytes,
        double totalMegabytes,
        double percentage,
        string expected)
    {
        var result = DemoFormatting.FormatDownloadProgress(
            downloadedMegabytes,
            totalMegabytes,
            percentage);

        Assert.Equal(expected, result);
    }
}
