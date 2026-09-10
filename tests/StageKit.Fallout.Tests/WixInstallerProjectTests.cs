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
            Assert.Contains(@"Resources\License.rtf", project, StringComparison.Ordinal);
            Assert.Contains("ValidateInstallerInputs", project, StringComparison.Ordinal);

            var package = File.ReadAllText(Path.Combine(directory, "Package.wxs"));
            Assert.Contains("!(bindpath.Publish)", package, StringComparison.Ordinal);
            Assert.Contains("WixUI_InstallDir", package, StringComparison.Ordinal);
            Assert.Contains("InstallOptionsDlg", package, StringComparison.Ordinal);
            Assert.Contains("SearchStartMenuShortcut", package, StringComparison.Ordinal);
            Assert.Contains("SearchDesktopShortcut", package, StringComparison.Ordinal);
            Assert.Contains("Set_CREATESTARTMENUSHORTCUT_Unchecked", package, StringComparison.Ordinal);
            Assert.Contains("Set_CREATEDESKTOPSHORTCUT_Unchecked", package, StringComparison.Ordinal);

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
