using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Fallout.Common.IO;
using Fallout.Solutions;
using Xunit;

namespace StageKit.Fallout.Tests;

/// <summary>
/// Covers the WiX installer project scaffolded by the <c>GenerateWindowsWixInstaller</c> target.
/// </summary>
public sealed class WixInstallerProjectTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly Guid X64UpgradeCode = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Arm64UpgradeCode = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Verifies that generation creates every file the WiX project needs, including placeholder artwork.
    /// </summary>
    [Fact]
    public void ExecuteGenerateWindowsWixInstaller_MissingDirectory_WritesProject()
    {
        var directory = CreateTemporaryPath();
        try
        {
            var build = new TestBuild(directory);

            build.InvokeExecuteGenerateWindowsWixInstaller();

            var projectFile = Path.Combine(directory, "Sample.WixInstaller.wixproj");
            var resourcesDirectory = Path.Combine(directory, "Resources");
            Assert.True(File.Exists(projectFile));
            Assert.True(File.Exists(Path.Combine(directory, "Package.wxs")));
            Assert.True(File.Exists(Path.Combine(directory, "Strings.en-us.wxl")));
            Assert.True(File.Exists(Path.Combine(directory, "README.md")));
            Assert.True(File.Exists(Path.Combine(resourcesDirectory, "License.rtf")));
            Assert.True(File.Exists(Path.Combine(resourcesDirectory, "InstallerBannerImage.png")));
            Assert.True(File.Exists(Path.Combine(resourcesDirectory, "InstallerDialogImage.png")));

            var project = File.ReadAllText(projectFile);
            Assert.Contains("11111111-1111-1111-1111-111111111111", project, StringComparison.Ordinal);
            Assert.Contains("22222222-2222-2222-2222-222222222222", project, StringComparison.Ordinal);
            Assert.Contains("<OutputName>$(ApplicationName)_$(RuntimeIdentifier)_v$(BuildVersion)</OutputName>",
                project, StringComparison.Ordinal);
            Assert.Contains(
                "<IntermediateOutputPath>$(ArtifactsPath)\\obj\\$(MSBuildProjectName)\\$(Configuration)\\$(OutputName)\\</IntermediateOutputPath>",
                project, StringComparison.Ordinal);
            Assert.Contains("<TargetFrameworks></TargetFrameworks>", project, StringComparison.Ordinal);
            Assert.Contains("<SuppressIces>$(SuppressIces);ICE57;ICE61</SuppressIces>", project,
                StringComparison.Ordinal);
            Assert.Contains("<InstallerScope Condition=\"'$(InstallerScope)' == ''\">perMachineOrUser</InstallerScope>",
                project, StringComparison.Ordinal);
            Assert.Contains("InstallerScope must be perMachineOrUser, perUser, or perMachine", project,
                StringComparison.Ordinal);
            Assert.Contains("InstallerPathRegistrationMode", project, StringComparison.Ordinal);
            Assert.Contains("InstallerPathRegistration must be Register, UserDefaultNo, or UserDefaultYes, but was",
                project, StringComparison.Ordinal);
            Assert.Contains(@"Resources\License.rtf", project, StringComparison.Ordinal);
            Assert.Contains("ValidateInstallerInputs", project, StringComparison.Ordinal);
            Assert.Contains("WixToolset.Util.wixext", project, StringComparison.Ordinal);
            Assert.Contains("SignInstallerPayload", project, StringComparison.Ordinal);
            Assert.Contains("<Target Name=\"SignMsi\"", project, StringComparison.Ordinal);

            var package = File.ReadAllText(Path.Combine(directory, "Package.wxs"));
            Assert.Contains("!(bindpath.Publish)", package, StringComparison.Ordinal);
            Assert.Contains("WixUI_InstallDir", package, StringComparison.Ordinal);
            Assert.Contains("InstallOptionsDlg", package, StringComparison.Ordinal);
            Assert.Contains("<?define PackageScope = \"perUserOrMachine\" ?>", package, StringComparison.Ordinal);
            Assert.Contains("Scope=\"$(var.PackageScope)\"", package, StringComparison.Ordinal);
            Assert.Contains("<?if $(var.InstallerScope) = \"perMachineOrUser\" ?>", package,
                StringComparison.Ordinal);
            Assert.Contains("This application will be installed only for the current user.", package,
                StringComparison.Ordinal);
            Assert.Contains("This application will be installed for all users", package, StringComparison.Ordinal);
            Assert.DoesNotContain("Event=\"SetTargetPath\" Value=\"[INSTALLFOLDER]\"", package,
                StringComparison.Ordinal);
            Assert.Contains("<MajorUpgrade AllowDowngrades=\"yes\"/>", package, StringComparison.Ordinal);
            Assert.Contains("<util:QueryNativeMachine/>", package, StringComparison.Ordinal);
            Assert.Contains(
                "Installed OR WIX_UPGRADE_DETECTED OR (\"$(sys.BUILDARCH)\" ~= \"x64\" IMP WIX_NATIVE_MACHINE = 34404)",
                package, StringComparison.Ordinal);
            Assert.Contains("<Publish Property=\"INSTALLSCOPE_UI_COMPLETE\" Value=\"1\" Order=\"1\"/>", package,
                StringComparison.Ordinal);
            Assert.Contains("<Publish Property=\"ALLUSERS\" Value=\"2\" Order=\"2\"", package,
                StringComparison.Ordinal);
            Assert.Contains("<Publish Property=\"MSIINSTALLPERUSER\" Value=\"1\" Order=\"3\"", package,
                StringComparison.Ordinal);
            Assert.Contains("<Publish Property=\"ALLUSERS\" Value=\"2\" Order=\"4\"", package,
                StringComparison.Ordinal);
            Assert.Contains("<Property Id=\"INSTALLSCOPE_UI_COMPLETE\" Secure=\"yes\"/>", package,
                StringComparison.Ordinal);
            Assert.Contains("<Property Id=\"INSTALLFOLDER\" Secure=\"yes\"/>", package,
                StringComparison.Ordinal);
            var executePerUserAction = package[package.IndexOf(
                "Action=\"Set_INSTALLSCOPE_PerUser_Execute\"", StringComparison.Ordinal)..];
            Assert.StartsWith("Action=\"Set_INSTALLSCOPE_PerUser_Execute\"", executePerUserAction,
                StringComparison.Ordinal);
            Assert.Contains(
                "Condition=\"NOT INSTALLSCOPE_UI_COMPLETE AND USERINSTALLFOLDER AND NOT MACHINEINSTALLFOLDER\"",
                executePerUserAction[..300], StringComparison.Ordinal);

            var executePerMachineAction = package[package.IndexOf(
                "Action=\"Set_INSTALLSCOPE_PerMachine_Execute\"", StringComparison.Ordinal)..];
            Assert.StartsWith("Action=\"Set_INSTALLSCOPE_PerMachine_Execute\"", executePerMachineAction,
                StringComparison.Ordinal);
            Assert.Contains(
                "Condition=\"NOT INSTALLSCOPE_UI_COMPLETE AND MACHINEINSTALLFOLDER AND NOT USERINSTALLFOLDER\"",
                executePerMachineAction[..300], StringComparison.Ordinal);
            Assert.Contains("Value=\"[MACHINEPROGRAMFILESFOLDER]\\$(var.ApplicationName)\"", package,
                StringComparison.Ordinal);
            Assert.Contains("<RemoveRegistryValue Root=\"HKMU\"", package, StringComparison.Ordinal);
            Assert.Contains("Name=\"ScopedInstallLocation\"/>", package, StringComparison.Ordinal);
            Assert.Contains("Property=\"INSTALLSCOPE\"", package, StringComparison.Ordinal);
            Assert.Contains("Id=\"WIXUI_EXITDIALOGOPTIONALCHECKBOXTEXT\" Value=\"Start program after install\"",
                package, StringComparison.Ordinal);
            Assert.Contains("WIXUI_EXITDIALOGOPTIONALCHECKBOX = 1", package, StringComparison.Ordinal);
            Assert.DoesNotContain("Property=\"STARTAFTERINSTALL\"", package, StringComparison.Ordinal);
            Assert.Contains("<?define InstallerRegistryKey =", package, StringComparison.Ordinal);
            Assert.Equal(18, package.Split("Key=\"$(var.InstallerRegistryKey)\"").Length - 1);
            Assert.DoesNotContain("Key=\"Software\\$(var.Company)\\$(var.ApplicationName)\\Installer\"", package,
                StringComparison.Ordinal);
            Assert.Contains("SearchMachineInstallLocation", package, StringComparison.Ordinal);
            Assert.Contains("SearchUserInstallLocation", package, StringComparison.Ordinal);
            Assert.Contains("<Component Id=\"InstallLocationRegistryComponent\" Guid=\"\">", package,
                StringComparison.Ordinal);
            Assert.Contains("<Component Id=\"InstalledStateRegistryComponent\" Guid=\"*\">", package,
                StringComparison.Ordinal);
            Assert.Contains("<RegistryKey Root=\"HKMU\" Key=\"$(var.InstallerRegistryKey)\">", package,
                StringComparison.Ordinal);
            Assert.Contains("Name=\"InstallLocation\" Type=\"string\" Value=\"[INSTALLFOLDER]\"", package,
                StringComparison.Ordinal);
            Assert.Contains("Name=\"ProductCode\" Type=\"string\" Value=\"[ProductCode]\"", package,
                StringComparison.Ordinal);
            Assert.Contains("Name=\"InstalledStateComponent-$(var.Platform)\" Type=\"integer\" Value=\"1\"", package,
                StringComparison.Ordinal);
            Assert.Contains("Name=\"Version\" Type=\"string\" Value=\"$(var.BuildVersion)\"", package,
                StringComparison.Ordinal);
            Assert.Contains("Name=\"Architecture\" Type=\"string\" Value=\"$(var.Platform)\"", package,
                StringComparison.Ordinal);
            Assert.Contains("Name=\"ExecutablePath\" Type=\"string\"", package, StringComparison.Ordinal);
            Assert.Contains("Value=\"[INSTALLFOLDER]$(var.ApplicationExecutableName).exe\"", package,
                StringComparison.Ordinal);
            Assert.Contains("SearchUserPathRegistration", package, StringComparison.Ordinal);
            Assert.Contains("SearchMachinePathRegistration", package, StringComparison.Ordinal);
            Assert.Contains("Name=\"PathRegistration\" Type=\"integer\"", package, StringComparison.Ordinal);
            Assert.Contains("<Environment Id=\"AddUserInstallPath\" Name=\"PATH\" Action=\"set\" Part=\"last\"",
                package, StringComparison.Ordinal);
            Assert.Contains("<Environment Id=\"AddMachineInstallPath\" Name=\"PATH\" Action=\"set\" Part=\"last\"",
                package, StringComparison.Ordinal);
            Assert.Contains("System=\"no\" Value=\"[INSTALLFOLDER]\"", package, StringComparison.Ordinal);
            Assert.Contains("System=\"yes\" Value=\"[INSTALLFOLDER]\"", package, StringComparison.Ordinal);
            Assert.Contains("Value=\"[PATHREGISTRATIONSTATE]\"", package, StringComparison.Ordinal);
            Assert.Contains("Name=\"UserPathEnvironment\" Type=\"integer\" Value=\"1\" KeyPath=\"yes\"", package,
                StringComparison.Ordinal);
            Assert.Contains("Name=\"MachinePathEnvironment\" Type=\"integer\" Value=\"1\" KeyPath=\"yes\"", package,
                StringComparison.Ordinal);
            Assert.Contains("<Publish Property=\"PATHREGISTRATIONSTATE\" Value=\"0\"", package,
                StringComparison.Ordinal);
            Assert.Contains("Transitive=\"yes\"", package, StringComparison.Ordinal);
            Assert.Contains("RegisterInstallPath", package, StringComparison.Ordinal);
            Assert.Contains("UserDefaultNo", package, StringComparison.Ordinal);
            Assert.Contains("SearchUserStartMenuShortcut", package, StringComparison.Ordinal);
            Assert.Contains("SearchMachineStartMenuShortcut", package, StringComparison.Ordinal);
            Assert.Contains("SearchUserDesktopShortcut", package, StringComparison.Ordinal);
            Assert.Contains("SearchMachineDesktopShortcut", package, StringComparison.Ordinal);
            Assert.Contains("Set_CREATESTARTMENUSHORTCUT_Unchecked", package, StringComparison.Ordinal);
            Assert.Contains("Set_CREATEDESKTOPSHORTCUT_Unchecked", package, StringComparison.Ordinal);

            var startMenuComponent = GetComponent(package, "StartMenuShortcutComponent");
            Assert.Contains("Directory=\"ApplicationProgramsFolder\"", startMenuComponent,
                StringComparison.Ordinal);
            Assert.Contains("Root=\"HKMU\"", startMenuComponent, StringComparison.Ordinal);
            Assert.DoesNotContain("Root=\"HKCU\"", startMenuComponent, StringComparison.Ordinal);
            Assert.DoesNotContain("Root=\"HKLM\"", startMenuComponent, StringComparison.Ordinal);

            var desktopComponent = GetComponent(package, "DesktopShortcutComponent");
            Assert.Contains("Directory=\"DesktopFolder\"", desktopComponent, StringComparison.Ordinal);
            Assert.Contains("Root=\"HKMU\"", desktopComponent, StringComparison.Ordinal);
            Assert.DoesNotContain("Root=\"HKCU\"", desktopComponent, StringComparison.Ordinal);
            Assert.DoesNotContain("Root=\"HKLM\"", desktopComponent, StringComparison.Ordinal);

            Assert.StartsWith(@"{\rtf1", File.ReadAllText(Path.Combine(resourcesDirectory, "License.rtf")),
                StringComparison.Ordinal);

            var banner = ReadPng(File.ReadAllBytes(Path.Combine(resourcesDirectory, "InstallerBannerImage.png")));
            Assert.Equal(WixInstallerProject.ImageWidth, banner.Width);
            Assert.Equal(WixInstallerProject.BannerImageHeight, banner.Height);

            var dialog = ReadPng(File.ReadAllBytes(Path.Combine(resourcesDirectory, "InstallerDialogImage.png")));
            Assert.Equal(WixInstallerProject.ImageWidth, dialog.Width);
            Assert.Equal(WixInstallerProject.DialogImageHeight, dialog.Height);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies that an existing project file fails the target instead of losing its upgrade codes.
    /// </summary>
    [Fact]
    public void ExecuteGenerateWindowsWixInstaller_ExistingProjectFile_Throws()
    {
        var directory = CreateTemporaryPath();
        try
        {
            Directory.CreateDirectory(directory);
            var projectFile = Path.Combine(directory, "Sample.WixInstaller.wixproj");
            File.WriteAllText(projectFile, "<Project/>");
            var build = new TestBuild(directory);

            var exception = Assert.Throws<InvalidOperationException>(
                build.InvokeExecuteGenerateWindowsWixInstaller);

            Assert.Contains("Sample.WixInstaller", exception.Message, StringComparison.Ordinal);
            Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
            Assert.Equal("<Project/>", File.ReadAllText(projectFile));
            Assert.False(Directory.Exists(Path.Combine(directory, "Resources")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies that any other content in the destination also fails the target.
    /// </summary>
    [Fact]
    public void ExecuteGenerateWindowsWixInstaller_NonEmptyDirectory_Throws()
    {
        var directory = CreateTemporaryPath();
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Package.wxs"), "<Wix/>");
            var build = new TestBuild(directory);

            var exception = Assert.Throws<InvalidOperationException>(
                build.InvokeExecuteGenerateWindowsWixInstaller);

            Assert.Contains("not empty", exception.Message, StringComparison.Ordinal);
            Assert.Equal("<Wix/>", File.ReadAllText(Path.Combine(directory, "Package.wxs")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies that an empty destination directory is generated into rather than rejected.
    /// </summary>
    [Fact]
    public void ExecuteGenerateWindowsWixInstaller_EmptyDirectory_WritesProject()
    {
        var directory = CreateTemporaryPath();
        try
        {
            Directory.CreateDirectory(directory);
            var build = new TestBuild(directory);

            build.InvokeExecuteGenerateWindowsWixInstaller();

            Assert.True(File.Exists(Path.Combine(directory, "Sample.WixInstaller.wixproj")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Verifies that the placeholder license escapes the characters RTF cannot carry literally.
    /// </summary>
    [Fact]
    public void CreateLicenseFile_SpecialCharacters_EscapesRtf()
    {
        var license = WixInstallerProject.CreateLicenseFile(
            "Sample",
            "Sample Company",
            @"Copyright (c) 2026 A\B {C} Tiago Conceição");

        Assert.Contains(@"A\\B \{C\}", license, StringComparison.Ordinal);
        Assert.Contains(@"Concei\u231?\u227?o", license, StringComparison.Ordinal);
        Assert.DoesNotContain("ç", license, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a blank copyright composes one from the company name.
    /// </summary>
    [Fact]
    public void CreateLicenseFile_BlankCopyright_UsesCompany()
    {
        var license = WixInstallerProject.CreateLicenseFile("Sample", "Sample Company", "   ");

        Assert.Contains("Sample Company", license, StringComparison.Ordinal);
        Assert.Contains("Sample License", license, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the generated artwork is a decodable truecolor PNG of the size WiX expects.
    /// </summary>
    [Fact]
    public void CreateImages_UseWixDimensionsAndDecodableScanlines()
    {
        var banner = ReadPng(WixInstallerProject.CreateBannerImage());
        Assert.Equal(WixInstallerProject.ImageWidth, banner.Width);
        Assert.Equal(WixInstallerProject.BannerImageHeight, banner.Height);
        Assert.Equal(banner.Height * (1 + (banner.Width * 3)), banner.RawLength);

        var dialog = ReadPng(WixInstallerProject.CreateDialogImage());
        Assert.Equal(WixInstallerProject.ImageWidth, dialog.Width);
        Assert.Equal(WixInstallerProject.DialogImageHeight, dialog.Height);
        Assert.Equal(dialog.Height * (1 + (dialog.Width * 3)), dialog.RawLength);
    }

    /// <summary>
    /// Walks a PNG, validating its chunk framing, and returns the image size with the decompressed scanline length.
    /// </summary>
    /// <param name="png">The encoded image.</param>
    /// <returns>The decoded width, height, and raw scanline byte count.</returns>
    private static (int Width, int Height, int RawLength) ReadPng(byte[] png)
    {
        Assert.True(
            png.AsSpan(0, 8).SequenceEqual(PngSignature),
            "The image does not start with the PNG signature.");

        var offset = 8;
        var width = 0;
        var height = 0;
        var rawLength = 0;
        var sawEnd = false;
        while (offset < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var data = png.AsSpan(offset + 8, length).ToArray();
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                    Assert.Equal(8, data[8]); // Bit depth.
                    Assert.Equal(2, data[9]); // Truecolor.
                    break;
                case "IDAT":
                {
                    using var input = new MemoryStream(data);
                    using var decompressor = new ZLibStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    decompressor.CopyTo(output);
                    rawLength += (int)output.Length;
                    break;
                }
                case "IEND":
                    sawEnd = true;
                    break;
            }

            offset += 12 + length;
        }

        Assert.True(sawEnd, "The image is missing its IEND chunk.");
        Assert.Equal(png.Length, offset);
        return (width, height, rawLength);
    }

    private static string GetComponent(string package, string componentId)
    {
        var start = package.IndexOf($"<Component Id=\"{componentId}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Component '{componentId}' was not found.");

        var end = package.IndexOf("</Component>", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Component '{componentId}' is not closed.");
        return package[start..end];
    }

    private static string CreateTemporaryPath()
    {
        return Path.Combine(Path.GetTempPath(), $"stagekit-wix-{Guid.NewGuid():N}");
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private sealed class TestBuild(AbsolutePath directory) : StageKitBuild
    {
        private int _upgradeCodeIndex;

        public override string SoftwareName => "Sample";

        public override AbsolutePath WindowsWixInstallerDirectory => directory;

        public override IReadOnlyCollection<Project> InstallerProjects => [];

        internal void InvokeExecuteGenerateWindowsWixInstaller()
        {
            ExecuteGenerateWindowsWixInstaller();
        }

        protected override string CreateWindowsWixInstallerLicense()
        {
            return WixInstallerProject.CreateLicenseFile("Sample", "Sample Company", "Copyright (c) 2026 Sample");
        }

        protected override Guid CreateWindowsWixInstallerUpgradeCode()
        {
            return _upgradeCodeIndex++ == 0 ? X64UpgradeCode : Arm64UpgradeCode;
        }
    }
}