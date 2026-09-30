using System.Reflection;
using System.Runtime.CompilerServices;

namespace StageKit.Fallout.Tests;

internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var repositoryRoot = typeof(TestEnvironment).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "RepositoryRoot")
            .Value;

        // Fallout resolves its root once, before the first build is constructed. Test runners can
        // change the working directory to the artifact cache, so supply the checkout path explicitly.
        Environment.SetEnvironmentVariable("NUKE_ROOT", repositoryRoot);
    }
}
