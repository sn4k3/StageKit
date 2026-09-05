using StageKit.Runtime;
using Xunit;

namespace StageKit.Fallout.Tests;

public class PackagingMetadataTests
{
    [Fact]
    public void ApplicationPackagingType_Values_AreSequentialWithoutFlagsAttribute()
    {
        Assert.False(typeof(ApplicationPackagingType).IsDefined(typeof(FlagsAttribute), false));
        Assert.Equal(0, (int)ApplicationPackagingType.None);
        Assert.Equal(1, (int)ApplicationPackagingType.Portable);
        Assert.Equal(2, (int)ApplicationPackagingType.DotNetSingleFile);
        Assert.Equal(3, (int)ApplicationPackagingType.WindowsInstaller);
        Assert.Equal(4, (int)ApplicationPackagingType.LinuxAppImage);
        Assert.Equal(5, (int)ApplicationPackagingType.LinuxFlatpak);
        Assert.Equal(6, (int)ApplicationPackagingType.LinuxSnap);
        Assert.Equal(7, (int)ApplicationPackagingType.LinuxDeb);
        Assert.Equal(8, (int)ApplicationPackagingType.LinuxRpm);
        Assert.Equal(9, (int)ApplicationPackagingType.LinuxArchPackage);
        Assert.Equal(10, (int)ApplicationPackagingType.MacOSAppBundle);
        Assert.Equal(11, (int)ApplicationPackagingType.MacOSDmg);
        Assert.Equal(12, (int)ApplicationPackagingType.MacOSPkg);
    }

    [Theory]
    [InlineData("My App", "my-app")]
    [InlineData("my.app+cli", "my.app+cli")]
    public void GetPackageName_ValidSoftwareName_ReturnsDistributionName(string softwareName, string expected)
    {
        Assert.Equal(expected, LinuxPackage.GetPackageName(softwareName));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("É")]
    [InlineData("---")]
    public void GetPackageName_InvalidDistributionName_Throws(string softwareName)
    {
        Assert.Throws<InvalidOperationException>(() => LinuxPackage.GetPackageName(softwareName));
    }

    [Fact]
    public void GetDebianControl_ValidMetadata_UsesPolicyCompliantFields()
    {
        var control = LinuxPackage.GetDebianControl(
            "test-app", "1.2.3", "amd64", "Test Author <author@example.com>", "Test summary", "Long description");

        Assert.Contains("Package: test-app\n", control, StringComparison.Ordinal);
        Assert.Contains("Maintainer: Test Author <author@example.com>\n", control, StringComparison.Ordinal);
        Assert.EndsWith("Description: Test summary\n Long description\n", control, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDebianControl_MaintainerContainsLineBreak_Throws()
    {
        Assert.Throws<ArgumentException>(() => LinuxPackage.GetDebianControl(
            "test-app", "1.2.3", "amd64", "Test Author <author@example.com>\nSection: admin", "Summary",
            "Description"));
    }

    [Theory]
    [InlineData("1.2_3")]
    [InlineData("1.2^3")]
    public void GetDebianVersion_InvalidPunctuation_Throws(string version)
    {
        Assert.Throws<InvalidOperationException>(() => LinuxPackage.GetDebianVersion(version));
    }

    [Fact]
    public void GetRpmSpec_ValidMetadata_PopulatesBuildRootDuringInstall()
    {
        var spec = LinuxPackage.GetRpmSpec(
            "test-app", "1.2.3", "x86_64", "MIT", "Test summary", "Long description", "/tmp/payload");

        Assert.Contains("%install\n", spec, StringComparison.Ordinal);
        Assert.Contains("$RPM_BUILD_ROOT", spec, StringComparison.Ordinal);
        Assert.Contains("cp -a '/tmp/payload/.' \"$RPM_BUILD_ROOT/\"", spec, StringComparison.Ordinal);
    }

    [Fact]
    public void GetArchPkgBuild_ValidMetadata_ExpandsSourceDirectoryAndDisablesPayloadRewriting()
    {
        var pkgBuild = LinuxPackage.GetArchPkgBuild(
            "test-app", "1.2.3", "x86_64", "MIT", "Test summary", "test-app-1.2.3");

        Assert.Contains("cp -a \"$srcdir/test-app-1.2.3/usr\" \"$pkgdir/\"", pkgBuild, StringComparison.Ordinal);
        Assert.Contains("options=('!strip' '!debug')", pkgBuild, StringComparison.Ordinal);
        Assert.DoesNotContain("'$srcdir", pkgBuild, StringComparison.Ordinal);
    }

    [Fact]
    public void GetArchPkgBuild_QuotedLicense_EscapesShellValue()
    {
        var pkgBuild = LinuxPackage.GetArchPkgBuild(
            "test-app", "1.2.3", "x86_64", "custom'license", "Test summary", "test-app-1.2.3");

        Assert.Contains("license=('custom'\"'\"'license')", pkgBuild, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapcraftManifest_ValidMetadata_DeclaresPayloadAndApplication()
    {
        var manifest = LinuxPackage.GetSnapcraftManifest(
            "test-app", "1.2.3", "amd64", "amd64", "TestApp", "Test summary", "Long description", "core24", "strict",
            ["desktop", "network"], ["libicu74"]);

        Assert.Contains("name: 'test-app'", manifest, StringComparison.Ordinal);
        Assert.Contains("command: 'TestApp'", manifest, StringComparison.Ordinal);
        Assert.Contains("plugin: dump", manifest, StringComparison.Ordinal);
        Assert.Contains("source: payload", manifest, StringComparison.Ordinal);
        Assert.Contains("stage-packages:\n      - 'libicu74'", manifest, StringComparison.Ordinal);
        Assert.Contains("platforms:\n  amd64:\n    build-on: ['amd64']\n    build-for: ['amd64']\n", manifest,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapcraftManifest_Arm64TargetOnAmd64Host_DeclaresCrossArchitectureBuild()
    {
        var manifest = LinuxPackage.GetSnapcraftManifest(
            "test-app", "1.2.3", "amd64", "arm64", "TestApp", "Test summary", "Long description", "core24",
            "strict", ["desktop"], ["libicu74"]);

        Assert.Contains("platforms:\n  arm64:\n    build-on: ['amd64']\n    build-for: ['arm64']\n", manifest,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapcraftManifest_Core22_UsesLegacyArchitectureSchema()
    {
        var manifest = LinuxPackage.GetSnapcraftManifest(
            "test-app", "1.2.3", "amd64", "arm64", "TestApp", "Test summary", "Long description", "core22", "strict",
            ["desktop"], ["libicu70"]);

        Assert.Contains("architectures:\n  - build-on: ['amd64']\n    build-for: ['arm64']\n", manifest,
            StringComparison.Ordinal);
        Assert.DoesNotContain("platforms:", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapcraftManifest_PlugContainsLineBreak_Throws()
    {
        Assert.Throws<ArgumentException>(() => LinuxPackage.GetSnapcraftManifest(
            "test-app", "1.2.3", "amd64", "amd64", "TestApp", "Test summary", "Long description", "core24", "strict",
            ["desktop\napps:"], ["libicu74"]));
    }

    [Fact]
    public void GetSnapcraftManifest_NoStagePackages_OmitsStagePackagesBlock()
    {
        var manifest = LinuxPackage.GetSnapcraftManifest(
            "test-app", "1.2.3", "amd64", "amd64", "TestApp", "Test summary", "Long description", "core24", "strict",
            ["desktop"], []);

        Assert.DoesNotContain("stage-packages:", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDmgCommand_ValidPaths_CreatesCompressedDiskImage()
    {
        var command = MacPackage.GetDmgCommand(
            "Test App", "/tmp/dmg-root", "/tmp/dmg-root/Applications", "/tmp/Test App.dmg");

        Assert.StartsWith("ln -s '/Applications' '/tmp/dmg-root/Applications' && ", command,
            StringComparison.Ordinal);
        Assert.Contains("hdiutil create", command, StringComparison.Ordinal);
        Assert.Contains("-format UDRW \"$uncompressed_image\"", command, StringComparison.Ordinal);
        Assert.Contains("hdiutil convert \"$uncompressed_image\" -ov -format UDZO -tasks 1", command,
            StringComparison.Ordinal);
        Assert.Contains("trap 'rm -f \"$uncompressed_image\"' EXIT", command, StringComparison.Ordinal);
        Assert.Contains("conversion_status=$?", command, StringComparison.Ordinal);
        Assert.Contains("[ \"$conversion_status\" -eq 137 ] && [ -s '/tmp/Test App.dmg' ]", command,
            StringComparison.Ordinal);
        Assert.Contains("hdiutil verify '/tmp/Test App.dmg'", command, StringComparison.Ordinal);
        Assert.Contains("-srcfolder '/tmp/dmg-root'", command, StringComparison.Ordinal);
        Assert.EndsWith("else exit \"$conversion_status\"; fi", command, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPkgCommand_ValidMetadata_CreatesApplicationComponentPackage()
    {
        var command = MacPackage.GetPkgCommand(
            "/tmp/Test App.app", "com.example.test-app", "1.2.3", "/tmp/Test App.pkg");

        Assert.Contains("pkgbuild --component '/tmp/Test App.app'", command, StringComparison.Ordinal);
        Assert.Contains("--identifier 'com.example.test-app'", command, StringComparison.Ordinal);
        Assert.Contains("--version '1.2.3'", command, StringComparison.Ordinal);
        Assert.Contains("--install-location '/Applications'", command, StringComparison.Ordinal);
        Assert.EndsWith("'/tmp/Test App.pkg'", command, StringComparison.Ordinal);
    }
}
