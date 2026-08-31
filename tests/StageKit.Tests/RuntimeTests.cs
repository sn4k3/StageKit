using System.Globalization;
using System.Runtime.InteropServices;
using StageKit.Runtime;

namespace StageKit.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public void RuntimeDiagnostics_GetInfoDict_IncludesRuntimeAndEntryApplicationInfo()
    {
        var info = RuntimeDiagnostics.GetInfoDict();

        Assert.Equal(RuntimeInformation.FrameworkDescription, info["Runtime.FrameworkDescription"]);
        Assert.Equal(RuntimeInformation.RuntimeIdentifier, info["Runtime.RuntimeIdentifier"]);
        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), info["Process.Id"]);
        Assert.Equal(EntryApplication.PackagingType.ToString(), info["EntryApplication.PackagingType"]);
    }

    [Fact]
    public void RuntimeDiagnostics_GetReport_AppendsLoadedAssembliesOnlyWhenRequested()
    {
        var report = RuntimeDiagnostics.GetReport();
        var reportWithAssemblies = RuntimeDiagnostics.GetReport(includeLoadedAssemblies: true);

        Assert.Contains("Runtime.FrameworkDescription:", report);
        Assert.DoesNotContain("Loaded Assemblies:", report);
        Assert.Contains("Loaded Assemblies:", reportWithAssemblies);
    }

    [Fact]
    public void DotNetSingleFileDetection_UsesProcessPathInsteadOfDotNetHostPath()
    {
        var processPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", "TestApp");

        var detectedPath = EntryApplication.DetectDotNetSingleFileAppPath(
            assemblyLocation: null,
            isRunningFromDotNetProcess: false,
            processPath);

        Assert.Equal(processPath, detectedPath);
    }

    [Fact]
    public void DotNetSingleFileDetection_UsesProcessPathWhenAssemblyWasExtractedToAnotherDirectory()
    {
        var processPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", "publish", "TestApp.exe");
        var assemblyLocation = Path.Combine(Path.GetTempPath(), ".net", "TestApp", "assembly", "TestApp.dll");

        var detectedPath = EntryApplication.DetectDotNetSingleFileAppPath(
            assemblyLocation,
            isRunningFromDotNetProcess: false,
            processPath);

        Assert.Equal(processPath, detectedPath);
    }

    [Fact]
    public void DotNetSingleFileDetection_ReturnsNullWhenAssemblyAndProcessShareDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "StageKit.Tests", "publish");
        var processPath = Path.Combine(directory, "TestApp.exe");
        var assemblyLocation = Path.Combine(directory, "TestApp.dll");

        var detectedPath = EntryApplication.DetectDotNetSingleFileAppPath(
            assemblyLocation,
            isRunningFromDotNetProcess: false,
            processPath);

        Assert.Null(detectedPath);
    }

    [Fact]
    public void ExecutablePathSelection_DoesNotUseFlatpakMarkerAsExecutablePath()
    {
        var processPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", "TestApp");

        var selectedPath = EntryApplication.SelectExecutablePath(
            linuxAppImagePath: null,
            linuxFlatpakPath: "flatpak",
            macOsAppBundlePath: null,
            dotNetSingleFileAppPath: null,
            isRunningFromDotNetProcess: false,
            assemblyLocation: null,
            processPath);

        Assert.Equal(processPath, selectedPath);
    }

    [Fact]
    public void IsSingleFileBundle_ReturnsFalseForFlatpak()
    {
        Assert.False(EntryApplication.IsSingleFileBundle(ApplicationPackagingType.LinuxFlatpak));
        Assert.True(EntryApplication.IsSingleFileBundle(ApplicationPackagingType.DotNetSingleFile));
        Assert.True(EntryApplication.IsSingleFileBundle(ApplicationPackagingType.LinuxAppImage));
    }

    [Theory]
    [InlineData(ApplicationPackagingType.WindowsInstaller)]
    [InlineData(ApplicationPackagingType.LinuxDeb)]
    [InlineData(ApplicationPackagingType.LinuxRpm)]
    [InlineData(ApplicationPackagingType.LinuxArchPackage)]
    [InlineData(ApplicationPackagingType.LinuxSnap)]
    [InlineData(ApplicationPackagingType.MacOSDmg)]
    [InlineData(ApplicationPackagingType.MacOSPkg)]
    public void RuntimeManifestDetection_ReturnsManifestPackagingType(ApplicationPackagingType expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(directory + "\\build-runtime.json", "{\"PackagingType\":\"" + expected + "\"}");

            var detected = EntryApplication.DetectRuntimeManifestPackagingType(directory, null, null);

            Assert.Equal(expected, detected);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
