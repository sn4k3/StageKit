using Fallout.Common.IO;
using StageKit.Runtime;
using Xunit;

namespace StageKit.Fallout.Tests;

/// <summary>
/// Verifies generation of the GitHub Releases installation script.
/// </summary>
public class InstallScriptTests
{
    /// <summary>
    /// Verifies that native packages precede generic bundles and Portable remains the final fallback.
    /// </summary>
    [Fact]
    public void Create_SelectedPackages_UsesNativeFirstPriority()
    {
        var script = InstallScript.Create(
            "https://github.com/example/sample",
            "Sample App",
            "sample",
            [
                ApplicationPackagingType.Portable,
                ApplicationPackagingType.DotNetSingleFile,
                ApplicationPackagingType.MacOSPkg,
                ApplicationPackagingType.MacOSDmg,
                ApplicationPackagingType.MacOSAppBundle,
                ApplicationPackagingType.LinuxDeb,
                ApplicationPackagingType.LinuxRpm,
                ApplicationPackagingType.LinuxArchPackage,
                ApplicationPackagingType.LinuxAppImage,
                ApplicationPackagingType.LinuxFlatpak,
                ApplicationPackagingType.LinuxSnap,
                ApplicationPackagingType.WindowsInstaller
            ]);

        var packageListStart = script.IndexOf("PACKAGE_TYPES=(", StringComparison.Ordinal);
        var packageListEnd = script.IndexOf(")", packageListStart, StringComparison.Ordinal);
        var packageList = script[packageListStart..packageListEnd];

        AssertInOrder(packageList,
            "'linux-deb'",
            "'linux-rpm'",
            "'linux-arch'",
            "'linux-appimage'",
            "'linux-flatpak'",
            "'linux-snap'",
            "'macos-app-bundle'",
            "'macos-pkg'",
            "'macos-dmg'",
            "'dotnet-single-file'",
            "'portable'");
        Assert.DoesNotContain("windows", packageList, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that the generated command help documents version selection, downgrade, and compatibility syntax.
    /// </summary>
    [Fact]
    public void Create_CommandLineHelp_DescribesSupportedCommands()
    {
        var script = InstallScript.Create(
            "https://github.com/example/sample",
            "Sample",
            "sample",
            [ApplicationPackagingType.LinuxDeb]);

        Assert.Contains("show_header()", script, StringComparison.Ordinal);
        Assert.Contains("Usage:", script, StringComparison.Ordinal);
        Assert.Contains("--help", script, StringComparison.Ordinal);
        Assert.Contains("--version VERSION", script, StringComparison.Ordinal);
        Assert.Contains("Install or downgrade", script, StringComparison.Ordinal);
        Assert.Contains("parse_arguments", script, StringComparison.Ordinal);
        Assert.Contains("--list", script, StringComparison.Ordinal);
        Assert.Contains("list_versions", script, StringComparison.Ordinal);
        Assert.Contains("--list-changelog", script, StringComparison.Ordinal);
        Assert.Contains("list_changelogs", script, StringComparison.Ordinal);
        Assert.Contains("CHANGELOG_LIMIT='20'", script, StringComparison.Ordinal);
        Assert.Contains("--list-changelog [LIMIT]", script, StringComparison.Ordinal);
        Assert.Contains("print_release_changelogs \"$remaining\"", script, StringComparison.Ordinal);
        Assert.Contains("release.get(\"body\")", script, StringComparison.Ordinal);
        Assert.Contains(".body //", script, StringComparison.Ordinal);
        Assert.Contains("sub(\"^[vV]\"; \"\")", script, StringComparison.Ordinal);
        Assert.Contains("print(f\"\\n# {version}\\n\\n{body}\")", script, StringComparison.Ordinal);
        Assert.Contains("/releases?per_page=100", script, StringComparison.Ordinal);
        Assert.Contains("--allow-downgrades", script, StringComparison.Ordinal);
        Assert.Contains("--force-downgrade", script, StringComparison.Ordinal);
        Assert.Contains("--oldpackage", script, StringComparison.Ordinal);
        Assert.Contains("ID_LIKE", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies repository and application metadata are safely embedded in the script.
    /// </summary>
    [Fact]
    public void Create_GitHubRepositoryAndApplicationMetadata_EmbedsShellSafeValues()
    {
        var script = InstallScript.Create(
            "git@github.com:example/sample.git",
            "Sam'ple App",
            "sample-app",
            [ApplicationPackagingType.LinuxFlatpak]);

        Assert.Contains("REPOSITORY='example/sample'", script, StringComparison.Ordinal);
        Assert.Contains("APPLICATION_NAME='Sam'\"'\"'ple App'", script, StringComparison.Ordinal);
        Assert.Contains("APPLICATION_SLUG='sam-ple-app'", script, StringComparison.Ordinal);
        Assert.Contains("EXECUTABLE_NAME='sample-app'", script, StringComparison.Ordinal);
        Assert.Contains("'linux-flatpak'", script, StringComparison.Ordinal);
        Assert.Contains("dotnet-single-file) printf '.bin", script, StringComparison.Ordinal);
        Assert.EndsWith("\n", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that unsupported repositories fail before a misleading script is written.
    /// </summary>
    [Theory]
    [InlineData("https://gitlab.com/example/sample")]
    [InlineData("https://github.com/example")]
    [InlineData("https://github.com/example/sample/issues")]
    public void GetGitHubRepository_UnsupportedUrl_Throws(string repositoryUrl)
    {
        Assert.Throws<InvalidOperationException>(() => InstallScript.GetGitHubRepository(repositoryUrl));
    }

    /// <summary>
    /// Verifies that a script cannot be generated when none of the selected formats are installable by Bash.
    /// </summary>
    [Fact]
    public void Create_NoInstallableSelectedPackage_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => InstallScript.Create(
            "https://github.com/example/sample",
            "Sample",
            "sample",
            [ApplicationPackagingType.None, ApplicationPackagingType.WindowsInstaller]));
    }

    /// <summary>
    /// Verifies that Windows installers precede generic executables and Portable remains the final fallback.
    /// </summary>
    [Fact]
    public void WindowsCreate_SelectedPackages_UsesNativeFirstPriority()
    {
        var script = WindowsInstallScript.Create(
            "https://github.com/example/sample",
            "Sample App",
            "sample",
            [
                ApplicationPackagingType.Portable,
                ApplicationPackagingType.DotNetSingleFile,
                ApplicationPackagingType.LinuxDeb,
                ApplicationPackagingType.WindowsInstaller
            ]);

        var packageListStart = script.IndexOf("$PackageTypes = @(", StringComparison.Ordinal);
        var packageListEnd = script.IndexOf(")", packageListStart, StringComparison.Ordinal);
        var packageList = script[packageListStart..packageListEnd];

        AssertInOrder(packageList,
            "'windows-installer'",
            "'dotnet-single-file'",
            "'portable'");
        Assert.DoesNotContain("linux", packageList, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that PowerShell help describes version selection and downgrade usage.
    /// </summary>
    [Fact]
    public void WindowsCreate_CommandLineHelp_DescribesSupportedCommands()
    {
        var script = WindowsInstallScript.Create(
            "https://github.com/example/sample",
            "Sample",
            "sample",
            [ApplicationPackagingType.WindowsInstaller]);

        Assert.Contains("function Show-Header", script, StringComparison.Ordinal);
        Assert.Contains("Usage:", script, StringComparison.Ordinal);
        Assert.Contains("-Help", script, StringComparison.Ordinal);
        Assert.Contains("-Version VERSION", script, StringComparison.Ordinal);
        Assert.Contains("-List", script, StringComparison.Ordinal);
        Assert.Contains("-ListChangelog", script, StringComparison.Ordinal);
        Assert.Contains("[int] $ChangelogLimit = 20", script, StringComparison.Ordinal);
        Assert.Contains("-ChangelogLimit LIMIT", script, StringComparison.Ordinal);
        Assert.Contains("--list-changelog", script, StringComparison.Ordinal);
        Assert.Contains("Install or downgrade", script, StringComparison.Ordinal);
        Assert.Contains("function Show-AvailableVersions", script, StringComparison.Ordinal);
        Assert.Contains("function Show-ReleaseChangelogs", script, StringComparison.Ordinal);
        Assert.Contains("[Math]::Min(100, $Limit)", script, StringComparison.Ordinal);
        Assert.Contains("Select-Object -First $remaining", script, StringComparison.Ordinal);
        Assert.Contains("$release.body", script, StringComparison.Ordinal);
        Assert.Contains("$releaseResponse = Invoke-RestMethod", script, StringComparison.Ordinal);
        Assert.Contains("$releases = @($releaseResponse)", script, StringComparison.Ordinal);
        Assert.Contains("$release.tag_name -replace '^[vV]', ''", script, StringComparison.Ordinal);
        Assert.Contains("Write-Host \"# $availableVersion\"", script, StringComparison.Ordinal);
        Assert.Contains("/releases?per_page=100", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-RestMethod", script, StringComparison.Ordinal);
        Assert.Contains("win-$Architecture", script, StringComparison.Ordinal);
        Assert.Contains("Write-Host \"Error: $Message\" -ForegroundColor Red", script, StringComparison.Ordinal);
        Assert.Contains("Release version '$Version' was not found", script, StringComparison.Ordinal);
        Assert.DoesNotContain("throw\r\n", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies repository and application metadata are safely embedded in PowerShell syntax.
    /// </summary>
    [Fact]
    public void WindowsCreate_GitHubRepositoryAndApplicationMetadata_EmbedsPowerShellSafeValues()
    {
        var script = WindowsInstallScript.Create(
            "git@github.com:example/sample.git",
            "Sam'ple App",
            "sample-app",
            [ApplicationPackagingType.Portable]);

        Assert.Contains("$Repository = 'example/sample'", script, StringComparison.Ordinal);
        Assert.Contains("$ApplicationName = 'Sam''ple App'", script, StringComparison.Ordinal);
        Assert.Contains("$ApplicationSlug = 'sam-ple-app'", script, StringComparison.Ordinal);
        Assert.Contains("$ExecutableName = 'sample-app.exe'", script, StringComparison.Ordinal);
        Assert.EndsWith("\r\n", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a Windows script requires at least one Windows-compatible selected format.
    /// </summary>
    [Fact]
    public void WindowsCreate_NoInstallableSelectedPackage_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => WindowsInstallScript.Create(
            "https://github.com/example/sample",
            "Sample",
            "sample",
            [ApplicationPackagingType.None, ApplicationPackagingType.LinuxDeb]));
    }

    /// <summary>
    /// Verifies the build exposes the generated script target and output path as public extension points.
    /// </summary>
    [Fact]
    public void StageKitBuild_InstallScriptSurface_IsPublic()
    {
        Assert.NotNull(typeof(StageKitBuild).GetProperty(nameof(StageKitBuild.GenerateInstallScript)));
        Assert.NotNull(typeof(StageKitBuild).GetProperty(nameof(StageKitBuild.InstallScriptFile)));
        Assert.NotNull(typeof(StageKitBuild).GetProperty(nameof(StageKitBuild.WindowsInstallScriptFile)));
    }

    /// <summary>
    /// Verifies that target execution creates the output directory and writes the generated script.
    /// </summary>
    [Fact]
    public void ExecuteGenerateInstallScript_MissingOutputDirectory_WritesScript()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"stagekit-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(rootDirectory, "nested", "install-sample.sh");
        var windowsScriptPath = Path.Combine(rootDirectory, "nested", "install-sample.ps1");
        try
        {
            var build = new TestBuild(scriptPath, windowsScriptPath);

            build.InvokeExecuteGenerateInstallScript();

            Assert.Equal("#!/usr/bin/env bash\n", File.ReadAllText(scriptPath));
            Assert.Equal("# PowerShell\r\n", File.ReadAllText(windowsScriptPath));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
                Directory.Delete(rootDirectory, true);
        }
    }

    /// <summary>
    /// Verifies that the target only emits scripts supported by the selected package formats.
    /// </summary>
    [Fact]
    public void ExecuteGenerateInstallScript_WindowsInstallerOnly_WritesOnlyPowerShellScript()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"stagekit-{Guid.NewGuid():N}");
        var scriptPath = Path.Combine(rootDirectory, "install-sample.sh");
        var windowsScriptPath = Path.Combine(rootDirectory, "install-sample.ps1");
        try
        {
            var build = new TestBuild(scriptPath, windowsScriptPath,
                [ApplicationPackagingType.WindowsInstaller]);

            build.InvokeExecuteGenerateInstallScript();

            Assert.False(File.Exists(scriptPath));
            Assert.Equal("# PowerShell\r\n", File.ReadAllText(windowsScriptPath));
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
                Directory.Delete(rootDirectory, true);
        }
    }

    private sealed class TestBuild : StageKitBuild
    {
        private readonly string _installScriptFile;
        private readonly string _windowsInstallScriptFile;

        public TestBuild(
            string installScriptFile,
            string windowsInstallScriptFile,
            ApplicationPackagingType[]? packagingTypes = null)
        {
            _installScriptFile = installScriptFile;
            _windowsInstallScriptFile = windowsInstallScriptFile;
            if (packagingTypes is not null)
                PackagingTypes = packagingTypes;
        }

        public override AbsolutePath InstallScriptFile => _installScriptFile;

        public override AbsolutePath WindowsInstallScriptFile => _windowsInstallScriptFile;

        protected override string CreateInstallScript()
        {
            return "#!/usr/bin/env bash\n";
        }

        protected override string CreateWindowsInstallScript()
        {
            return "# PowerShell\r\n";
        }

        internal void InvokeExecuteGenerateInstallScript()
        {
            ExecuteGenerateInstallScript();
        }
    }

    private static void AssertInOrder(string value, params string[] expectedValues)
    {
        var previousIndex = -1;
        foreach (var expectedValue in expectedValues)
        {
            var currentIndex = value.IndexOf(expectedValue, StringComparison.Ordinal);
            Assert.True(currentIndex > previousIndex,
                $"Expected '{expectedValue}' after index {previousIndex} in:{Environment.NewLine}{value}");
            previousIndex = currentIndex;
        }
    }
}
