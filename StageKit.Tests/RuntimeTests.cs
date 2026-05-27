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
        Assert.Equal(EntryApplication.BundleType.ToString(), info["EntryApplication.BundleType"]);
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
}
