using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using StageKit.Runtime;

namespace StageKit.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public void EntryApplication_ProcessUptime_ApproximatelyMatchesCurrentProcessRuntime()
    {
        using var process = Process.GetCurrentProcess();
        var expectedRuntime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

        var actualRuntime = EntryApplication.ProcessUptime;

        Assert.True(EntryApplication.ProcessStartingTimestamp <= Stopwatch.GetTimestamp());
        Assert.InRange(actualRuntime, expectedRuntime - TimeSpan.FromSeconds(1),
            expectedRuntime + TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RuntimeDiagnostics_GetInfoDict_IncludesRuntimeAndEntryApplicationInfo()
    {
        var info = RuntimeDiagnostics.GetInfoDict();

        Assert.Equal(RuntimeInformation.FrameworkDescription, info["Runtime.FrameworkDescription"]);
        Assert.Equal(RuntimeInformation.RuntimeIdentifier, info["Runtime.RuntimeIdentifier"]);
        Assert.Equal(Environment.ProcessId.ToString(CultureInfo.InvariantCulture), info["Process.Id"]);
        Assert.Equal(EntryApplication.PackagingType.ToString(), info["EntryApplication.PackagingType"]);
        Assert.Contains("System.Uptime", info);
        Assert.Contains("Process.WorkingSetBytes", info);
        Assert.Contains("Runtime.IsDynamicCodeSupported", info);
        Assert.Contains("Environment.CurrentCulture", info);
        Assert.DoesNotContain("System.UpTime", info);
    }

    [Fact]
    public void RuntimeDiagnostics_GetProcessSnapshot_CapturesCurrentMeasurements()
    {
        var before = DateTimeOffset.UtcNow;

        var snapshot = RuntimeDiagnostics.GetProcessSnapshot();

        Assert.InRange(snapshot.CapturedAtUtc, before, DateTimeOffset.UtcNow);
        Assert.InRange(snapshot.ProcessUptime,
            EntryApplication.ProcessUptime - TimeSpan.FromSeconds(1),
            EntryApplication.ProcessUptime + TimeSpan.FromSeconds(1));
        Assert.True(snapshot.WorkingSetBytes >= 0);
        Assert.True(snapshot.PrivateMemoryBytes >= 0);
        Assert.True(snapshot.ThreadCount >= 0);
        Assert.True(snapshot.ManagedHeapBytes >= 0);
        Assert.True(snapshot.TotalAllocatedBytes >= 0);
        Assert.Equal(
            snapshot.PrivilegedProcessorTime + snapshot.UserProcessorTime,
            snapshot.TotalProcessorTime);

        if (snapshot.IsProcessInformationAvailable)
        {
            Assert.True(snapshot.WorkingSetBytes > 0);
            Assert.True(snapshot.ThreadCount > 0);
        }
    }

    [Fact]
    public void RuntimeDiagnostics_GetInfoDict_CanExcludeProcessSnapshot()
    {
        var info = RuntimeDiagnostics.GetInfoDict(new RuntimeDiagnosticsOptions
        {
            IncludeProcessSnapshot = false
        });

        Assert.Contains("Process.Id", info);
        Assert.DoesNotContain("Process.CapturedAtUtc", info);
        Assert.DoesNotContain("GC.ManagedHeapBytes", info);
    }

    [Fact]
    public void RuntimeDiagnostics_FormatReport_PreservesOrderAndEscapesLineEndings()
    {
        KeyValuePair<string, string?>[] info =
        [
            new("First", "line 1\r\nline 2"),
            new("Second", "value")
        ];

        var report = RuntimeDiagnostics.FormatReport(info);

        Assert.Equal($"First: line 1\\nline 2{Environment.NewLine}Second: value", report);
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
    public void RuntimeDiagnostics_GetReport_UsesOptions()
    {
        var report = RuntimeDiagnostics.GetReport(new RuntimeDiagnosticsOptions
        {
            IncludeProcessSnapshot = false,
            IncludeLoadedAssemblies = true
        });

        Assert.DoesNotContain("Process.CapturedAtUtc:", report);
        Assert.Contains("Loaded Assemblies:", report);
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
            WriteBuildRuntimeManifest(
                Path.Combine(directory, BuildRuntime.DefaultManifestFileName),
                expected);

            var detected = EntryApplication.DetectRuntimeManifestPackagingType(directory, null, null);

            Assert.Equal(expected, detected);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BuildRuntime_TryLoad_UsesSourceGeneratedMetadata()
    {
        var directory = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var manifestPath = Path.Combine(directory, BuildRuntime.DefaultManifestFileName);
        Directory.CreateDirectory(directory);

        try
        {
            var expected = WriteBuildRuntimeManifest(
                manifestPath,
                ApplicationPackagingType.LinuxAppImage);

            var loaded = BuildRuntime.TryLoad(manifestPath, out var actual);

            Assert.True(loaded);
            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BuildRuntime_TryLoad_ReturnsFalseForMissingOrInvalidManifest()
    {
        var directory = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var manifestPath = Path.Combine(directory, BuildRuntime.DefaultManifestFileName);
        Directory.CreateDirectory(directory);

        try
        {
            Assert.False(BuildRuntime.TryLoad(manifestPath, out var missing));
            Assert.Null(missing);

            File.WriteAllText(manifestPath, "not-json");

            Assert.False(BuildRuntime.TryLoad(manifestPath, out var invalid));
            Assert.Null(invalid);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void BuildRuntime_LoadFromApplicationDirectories_ContinuesAfterInvalidManifest()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        var baseDirectory = Path.Combine(rootDirectory, "base");
        var assemblyDirectory = Path.Combine(rootDirectory, "assembly");
        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(assemblyDirectory);

        try
        {
            File.WriteAllText(Path.Combine(baseDirectory, BuildRuntime.DefaultManifestFileName), "not-json");
            var expected = WriteBuildRuntimeManifest(
                Path.Combine(assemblyDirectory, BuildRuntime.DefaultManifestFileName),
                ApplicationPackagingType.LinuxRpm);

            var actual = BuildRuntime.LoadFromApplicationDirectories(
                baseDirectory,
                Path.Combine(assemblyDirectory, "StageKit.Tests.dll"),
                null);

            Assert.Equal(expected, actual);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public void BuildRuntime_Instance_IsCached()
    {
        var first = BuildRuntime.Instance;
        var second = BuildRuntime.Instance;

        Assert.True(BuildRuntime.IsInstanceCreated);
        Assert.Same(first, second);
    }

    private static BuildRuntime WriteBuildRuntimeManifest(
        string manifestPath,
        ApplicationPackagingType packagingType)
    {
        var runtime = new BuildRuntime("test-x64", "1.2.3", true, packagingType)
        {
            BuildDateTimeUtc = new DateTime(2026, 9, 4, 12, 34, 56, DateTimeKind.Utc),
            BuildOSDescription = "Test OS"
        };
        var json = JsonSerializer.Serialize(runtime, BuildRuntimeJsonContext.Default.BuildRuntime);
        File.WriteAllText(manifestPath, json);
        return runtime;
    }
}