using System.Text.Json;
using StageKit.Runtime;
using Xunit;

namespace StageKit.Fallout.Tests;

/// <summary>
/// Verifies runtime manifest construction and serialization behavior.
/// </summary>
public class BuildRuntimeTests
{
    /// <summary>
    /// Verifies that construction populates required values and environment-derived defaults.
    /// </summary>
    [Fact]
    public void Constructor_RequiredValuesAndDefaults_ArePopulated()
    {
        var before = DateTime.UtcNow;
        var runtime = new BuildRuntime("linux-x64", "2.3.4", true,
            ApplicationPackagingType.LinuxAppImage);

        Assert.Equal("linux-x64", runtime.Runtime);
        Assert.Equal("2.3.4", runtime.BuildVersion);
        Assert.True(runtime.IsBundle);
        Assert.Equal(ApplicationPackagingType.LinuxAppImage, runtime.PackagingType);
        Assert.InRange(runtime.BuildDateTimeUtc, before, DateTime.UtcNow);
        Assert.False(string.IsNullOrWhiteSpace(runtime.BuildOSDescription));
    }

    /// <summary>
    /// Verifies that JSON serialization writes the bundle type as a string.
    /// </summary>
    [Fact]
    public void JsonSerialization_PackagingType_IsWrittenAsString()
    {
        var runtime = new BuildRuntime("win-x64", "1.0.0", true,
            ApplicationPackagingType.WindowsInstaller);

        var json = JsonSerializer.Serialize(runtime);

        Assert.Contains($"\"PackagingType\":\"{nameof(ApplicationPackagingType.WindowsInstaller)}\"", json,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the .NET single-file bundle type is serialized by name.
    /// </summary>
    [Fact]
    public void JsonSerialization_DotNetSingleFileApp_IsWrittenAsString()
    {
        var runtime = new BuildRuntime("win-x64", "1.0.0", true,
            ApplicationPackagingType.DotNetSingleFile);

        var json = JsonSerializer.Serialize(runtime);

        Assert.Contains($"\"PackagingType\":\"{nameof(ApplicationPackagingType.DotNetSingleFile)}\"", json,
            StringComparison.Ordinal);
    }
}