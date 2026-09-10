using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace StageKit.Fallout;

/// <summary>
/// Creates the files of a WiX installer project scaffolded by <see cref="StageKitBuild.GenerateWindowsWixInstaller"/>.
/// </summary>
internal static class WixInstallerProject
{
    /// <summary>
    /// The width, in pixels, that WiX expects for both the banner and the dialog artwork.
    /// </summary>
    internal const int ImageWidth = 493;

    /// <summary>
    /// The height, in pixels, that WiX expects for the banner artwork.
    /// </summary>
    internal const int BannerImageHeight = 58;

    /// <summary>
    /// The height, in pixels, that WiX expects for the dialog artwork.
    /// </summary>
    internal const int DialogImageHeight = 312;

    /// <summary>
    /// The width, in pixels, of the dialog artwork area reserved for a logo.
    /// </summary>
    private const int DialogArtworkWidth = 164;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    #region Project files

    /// <summary>
    /// Creates the WiX project file.
    /// </summary>
    /// <param name="x64UpgradeCode">The stable upgrade code used for x64 installers.</param>
    /// <param name="arm64UpgradeCode">The stable upgrade code used for arm64 installers.</param>
    /// <returns>The complete <c>.wixproj</c> contents.</returns>
    internal static string CreateProjectFile(Guid x64UpgradeCode, Guid arm64UpgradeCode)
    {
        return ProjectTemplate
            .Replace("{{UPGRADE_CODE_X64}}", FormatUpgradeCode(x64UpgradeCode), StringComparison.Ordinal)
            .Replace("{{UPGRADE_CODE_ARM64}}", FormatUpgradeCode(arm64UpgradeCode), StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates the authoring file that declares the package, its shortcuts, and the wizard.
    /// </summary>
    /// <returns>The complete <c>Package.wxs</c> contents.</returns>
    internal static string CreatePackageFile()
    {
        return PackageTemplate;
    }

    /// <summary>
    /// Creates the English localization file that titles every wizard dialog.
    /// </summary>
    /// <returns>The complete <c>Strings.en-us.wxl</c> contents.</returns>
    internal static string CreateLocalizationFile()
    {
        return LocalizationTemplate;
    }

    /// <summary>
    /// Creates a placeholder license shown by the wizard's license agreement page.
    /// </summary>
    /// <param name="softwareName">The product name.</param>
    /// <param name="company">The company shown in the copyright line.</param>
    /// <param name="copyright">The copyright line. A blank value composes one from the current year.</param>
    /// <returns>The complete <c>License.rtf</c> contents.</returns>
    internal static string CreateLicenseFile(string softwareName, string company, string? copyright)
    {
        var notice = string.IsNullOrWhiteSpace(copyright)
            ? $"Copyright (c) {DateTime.Now.Year.ToString(CultureInfo.InvariantCulture)} {company}"
            : copyright.Trim();

        return LicenseTemplate
            .Replace("{{SOFTWARE_NAME}}", EscapeRtf(softwareName), StringComparison.Ordinal)
            .Replace("{{COPYRIGHT}}", EscapeRtf(notice), StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates the project readme describing the artwork contract and the pipeline properties.
    /// </summary>
    /// <param name="projectName">The generated project name.</param>
    /// <returns>The complete <c>README.md</c> contents.</returns>
    internal static string CreateReadmeFile(string projectName)
    {
        return ReadmeTemplate.Replace("{{PROJECT_NAME}}", projectName, StringComparison.Ordinal);
    }

    #endregion

    #region Placeholder artwork

    /// <summary>
    /// Creates the placeholder banner shown on every wizard page after the welcome page.
    /// </summary>
    /// <returns>A 493 × 58 PNG image.</returns>
    /// <remarks>The logo area sits at the right edge, matching the layout WiX expects.</remarks>
    internal static byte[] CreateBannerImage()
    {
        return CreatePng(ImageWidth, BannerImageHeight, static (x, _) =>
            x >= ImageWidth - BannerImageHeight
                ? Accent
                : Blend(SurfaceLight, SurfaceDark, (double)x / ImageWidth));
    }

    /// <summary>
    /// Creates the placeholder background shown on the welcome and completion pages.
    /// </summary>
    /// <returns>A 493 × 312 PNG image.</returns>
    /// <remarks>The artwork band occupies the leftmost 164 pixels, matching the layout WiX expects.</remarks>
    internal static byte[] CreateDialogImage()
    {
        return CreatePng(ImageWidth, DialogImageHeight, static (x, y) =>
            x < DialogArtworkWidth
                ? Blend(Accent, AccentDark, (double)y / DialogImageHeight)
                : Blend(SurfaceLight, SurfaceDark, (double)y / DialogImageHeight));
    }

    private static readonly (byte R, byte G, byte B) SurfaceLight = (0xF7, 0xF9, 0xFC);
    private static readonly (byte R, byte G, byte B) SurfaceDark = (0xDD, 0xE4, 0xEE);
    private static readonly (byte R, byte G, byte B) Accent = (0x33, 0x41, 0x5C);
    private static readonly (byte R, byte G, byte B) AccentDark = (0x1E, 0x27, 0x38);

    private static (byte R, byte G, byte B) Blend((byte R, byte G, byte B) from, (byte R, byte G, byte B) to,
        double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return (
            (byte)Math.Round(from.R + ((to.R - from.R) * amount)),
            (byte)Math.Round(from.G + ((to.G - from.G) * amount)),
            (byte)Math.Round(from.B + ((to.B - from.B) * amount)));
    }

    /// <summary>
    /// Encodes an 8-bit truecolor PNG without any external imaging dependency.
    /// </summary>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <param name="shader">The callback that returns the color of one pixel.</param>
    /// <returns>The encoded PNG bytes.</returns>
    private static byte[] CreatePng(int width, int height, Func<int, int, (byte R, byte G, byte B)> shader)
    {
        var stride = 1 + (width * 3);
        var raw = new byte[height * stride];
        for (var y = 0; y < height; y++)
        {
            var offset = (y * stride) + 1; // Leave the leading per-scanline filter byte at zero (no filter).
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = shader(x, y);
                raw[offset++] = r;
                raw[offset++] = g;
                raw[offset++] = b;
            }
        }

        using var deflated = new MemoryStream();
        using (var compressor = new ZLibStream(deflated, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressor.Write(raw, 0, raw.Length);
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // Bit depth.
        header[9] = 2; // Color type: truecolor.

        using var png = new MemoryStream();
        png.Write(PngSignature);
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IDAT"u8, deflated.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);

        Span<byte> checksum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, ComputeCrc32(type, data));
        stream.Write(checksum);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type)
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);

        foreach (var value in data)
            crc = Crc32Table[(crc ^ value) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFFu;
    }

    private static readonly uint[] Crc32Table = CreateCrc32Table();

    private static uint[] CreateCrc32Table()
    {
        var table = new uint[256];
        for (var i = 0u; i < table.Length; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;

            table[i] = value;
        }

        return table;
    }

    #endregion

    #region Helpers

    private static string FormatUpgradeCode(Guid upgradeCode)
    {
        return upgradeCode.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    /// <summary>
    /// Escapes text for an RTF document, including the non-ASCII characters RTF cannot carry literally.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The RTF-safe text.</returns>
    internal static string EscapeRtf(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            switch (character)
            {
                case '\\':
                case '{':
                case '}':
                    builder.Append('\\').Append(character);
                    break;
                case '\n':
                    builder.Append("\\par\n");
                    break;
                case '\r':
                    break;
                default:
                    if (character < 128) builder.Append(character);
                    else builder.Append("\\u").Append(((short)character).ToString(CultureInfo.InvariantCulture))
                        .Append('?');
                    break;
            }
        }

        return builder.ToString();
    }

    #endregion

    #region Templates

    private const string ProjectTemplate =
        """
        <Project Sdk="WixToolset.Sdk/7.0.0">

            <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Package</OutputType>
                <AcceptEula>wix7</AcceptEula>
                <!-- Same-version replacement is intentional (MajorUpgrade.AllowSameVersionUpgrades). -->
                <SuppressIces>$(SuppressIces);ICE61</SuppressIces>
                <!-- Keep the MSI in the output root while resolving English UI strings. -->
                <Cultures>neutral,en-US</Cultures>
                <InstallerPlatform>$(Platform)</InstallerPlatform>
                <OutputName>$(ApplicationName)_$(RuntimeIdentifier)_v$(BuildVersion)</OutputName>
                <LicenseFile>$(MSBuildProjectDirectory)\Resources\License.rtf</LicenseFile>
                <InstallerDialogImage>$(MSBuildProjectDirectory)\Resources\InstallerDialogImage.png</InstallerDialogImage>
                <InstallerBannerImage>$(MSBuildProjectDirectory)\Resources\InstallerBannerImage.png</InstallerBannerImage>
            </PropertyGroup>
            
            <PropertyGroup Condition="'$(ArtifactsPath)' != ''">
              <OutputPath>$(ArtifactsPath)\bin\$(MSBuildProjectName)\$(Configuration)\</OutputPath>
              <IntermediateOutputPath>$(ArtifactsPath)\obj\$(MSBuildProjectName)\$(Configuration)\</IntermediateOutputPath>
            </PropertyGroup>

            <!-- Generated once; keep these stable so upgrades replace earlier installations. -->
            <PropertyGroup Condition="'$(Platform)' == 'x64'">
                <UpgradeCode>{{UPGRADE_CODE_X64}}</UpgradeCode>
            </PropertyGroup>

            <PropertyGroup Condition="'$(Platform)' == 'arm64'">
                <UpgradeCode>{{UPGRADE_CODE_ARM64}}</UpgradeCode>
            </PropertyGroup>

            <PropertyGroup>
                <DefineConstants>$(DefineConstants);ApplicationName=$(ApplicationName);ApplicationExecutableName=$(ApplicationExecutableName);BuildVersion=$(BuildVersion);Company=$(Company);Copyright=$(Copyright);RepositoryUrl=$(RepositoryUrl);ApplicationIcon=$(ApplicationIcon);LicenseFile=$(LicenseFile);UpgradeCode=$(UpgradeCode)</DefineConstants>
            </PropertyGroup>

            <ItemGroup>
                <BindPath Include="$(PublishDirectory)" BindName="Publish"/>
                <PackageReference Include="WixToolset.UI.wixext" Version="7.0.0"/>
            </ItemGroup>

            <!-- Optional artwork; omit either property to retain that WixUI default image. -->
            <PropertyGroup Condition="'$(InstallerDialogImage)' != ''">
                <DefineConstants>$(DefineConstants);InstallerDialogImage=$(InstallerDialogImage)</DefineConstants>
            </PropertyGroup>
            <PropertyGroup Condition="'$(InstallerBannerImage)' != ''">
                <DefineConstants>$(DefineConstants);InstallerBannerImage=$(InstallerBannerImage)</DefineConstants>
            </PropertyGroup>

            <ItemGroup>
                <Content Include="Resources\InstallerBannerImage.png"/>
                <Content Include="Resources\InstallerDialogImage.png"/>
                <Content Include="Resources\License.rtf"/>
            </ItemGroup>

            <Target Name="ValidateInstallerInputs" BeforeTargets="CoreCompile">
                <Error Condition="'$(InstallerDialogImage)' != '' And !Exists('$(InstallerDialogImage)')"
                       Text="Installer dialog image '$(InstallerDialogImage)' does not exist."/>
                <Error Condition="'$(InstallerBannerImage)' != '' And !Exists('$(InstallerBannerImage)')"
                       Text="Installer banner image '$(InstallerBannerImage)' does not exist."/>
                <Error Condition="'$(PublishDirectory)' == ''"
                       Text="PublishDirectory must be provided by the Fallout publish pipeline."/>
                <Error Condition="'$(ApplicationName)' == ''"
                       Text="ApplicationName must be provided by the Fallout publish pipeline."/>
                <Error Condition="'$(ApplicationExecutableName)' == ''"
                       Text="ApplicationExecutableName must be provided by the Fallout publish pipeline."/>
                <Error Condition="'$(BuildVersion)' == ''"
                       Text="BuildVersion must be provided by the Fallout publish pipeline."/>
                <Error Condition="'$(RuntimeIdentifier)' == ''"
                       Text="RuntimeIdentifier must be provided by the Fallout publish pipeline."/>
                <Error Condition="'$(Platform)' != 'x64' And '$(Platform)' != 'arm64'"
                       Text="Platform must be x64 or arm64, but was '$(Platform)'."/>
                <Error Condition="!Exists('$(PublishDirectory)')"
                       Text="PublishDirectory '$(PublishDirectory)' does not exist."/>
                <Error Condition="!Exists('$(PublishDirectory)\$(ApplicationExecutableName).exe')"
                       Text="Application executable '$(PublishDirectory)\$(ApplicationExecutableName).exe' does not exist."/>
                <Error Condition="!Exists('$(ApplicationIcon)')"
                       Text="Application icon '$(ApplicationIcon)' does not exist."/>
                <Error Condition="!Exists('$(LicenseFile)')"
                       Text="Installer license '$(LicenseFile)' does not exist."/>
            </Target>

        </Project>

        """;

    private const string PackageTemplate =
        """
        <Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
             xmlns:ui="http://wixtoolset.org/schemas/v4/wxs/ui">
            <Package Name="$(var.ApplicationName)"
                     Manufacturer="$(var.Company)"
                     Version="$(var.BuildVersion)"
                     UpgradeCode="$(var.UpgradeCode)"
                     Language="1033"
                     Scope="perMachine"
                     Compressed="yes">
                <!-- Rebuilt MSIs have new product codes; replace existing same-version installations. -->
                <MajorUpgrade AllowSameVersionUpgrades="yes"
                              DowngradeErrorMessage="A newer version of [ProductName] is already installed."/>
                <MediaTemplate EmbedCab="yes"/>
                <Property Id="CREATESTARTMENUSHORTCUT" Value="1" Secure="yes"/>
                <Property Id="CREATEDESKTOPSHORTCUT" Value="1" Secure="yes"/>

                <Property Id="STARTMENUSHORTCUT_REG" Secure="yes">
                    <RegistrySearch Id="SearchStartMenuShortcut"
                                    Root="HKCU"
                                    Key="Software\$(var.Company)\$(var.ApplicationName)\Installer"
                                    Name="StartMenuShortcut"
                                    Type="raw"/>
                </Property>
                <Property Id="DESKTOPSHORTCUT_REG" Secure="yes">
                    <RegistrySearch Id="SearchDesktopShortcut"
                                    Root="HKCU"
                                    Key="Software\$(var.Company)\$(var.ApplicationName)\Installer"
                                    Name="DesktopShortcut"
                                    Type="raw"/>
                </Property>

                <SetProperty Id="CREATESTARTMENUSHORTCUT" Value="1" After="AppSearch" Sequence="ui"
                             Condition="STARTMENUSHORTCUT_REG = &quot;#1&quot; OR STARTMENUSHORTCUT_REG = &quot;1&quot;"/>
                <SetProperty Id="CREATESTARTMENUSHORTCUT" Action="Set_CREATESTARTMENUSHORTCUT_Unchecked"
                             Value="{}" After="AppSearch" Sequence="ui"
                             Condition="STARTMENUSHORTCUT_REG = &quot;#0&quot; OR STARTMENUSHORTCUT_REG = &quot;0&quot; OR ((WIX_UPGRADE_DETECTED OR Installed) AND NOT STARTMENUSHORTCUT_REG)"/>

                <SetProperty Id="CREATEDESKTOPSHORTCUT" Value="1" After="AppSearch" Sequence="ui"
                             Condition="DESKTOPSHORTCUT_REG = &quot;#1&quot; OR DESKTOPSHORTCUT_REG = &quot;1&quot;"/>
                <SetProperty Id="CREATEDESKTOPSHORTCUT" Action="Set_CREATEDESKTOPSHORTCUT_Unchecked"
                             Value="{}" After="AppSearch" Sequence="ui"
                             Condition="DESKTOPSHORTCUT_REG = &quot;#0&quot; OR DESKTOPSHORTCUT_REG = &quot;0&quot; OR ((WIX_UPGRADE_DETECTED OR Installed) AND NOT DESKTOPSHORTCUT_REG)"/>

                <CustomAction Id="LaunchApplication" FileRef="MainExecutable" ExeCommand=""
                              Execute="immediate" Impersonate="yes" Return="asyncNoWait"/>

                <Icon Id="ProductIcon.ico" SourceFile="$(var.ApplicationIcon)"/>
                <Property Id="ARPPRODUCTICON" Value="ProductIcon.ico"/>
                <Property Id="ARPURLINFOABOUT" Value="$(var.RepositoryUrl)"/>
                <Property Id="ARPHELPLINK" Value="$(var.RepositoryUrl)/issues"/>
                <SetProperty Id="ARPINSTALLLOCATION" Value="[INSTALLFOLDER]" After="CostFinalize"/>

                <StandardDirectory Id="ProgramFiles6432Folder">
                    <Directory Id="INSTALLFOLDER" Name="$(var.ApplicationName)">
                        <Component Id="MainExecutableComponent" Guid="*">
                            <File Id="MainExecutable"
                                  Source="!(bindpath.Publish)\$(var.ApplicationExecutableName).exe"
                                  KeyPath="yes"/>
                        </Component>

                        <Component Id="StartMenuShortcutComponent" Guid="*" Condition="CREATESTARTMENUSHORTCUT = 1">
                            <Shortcut Id="StartMenuShortcut" Directory="ApplicationProgramsFolder"
                                      Name="$(var.ApplicationName)" Target="[INSTALLFOLDER]$(var.ApplicationExecutableName).exe"
                                      WorkingDirectory="INSTALLFOLDER"/>
                            <RegistryValue Root="HKCU" Key="Software\$(var.Company)\$(var.ApplicationName)\Installer"
                                           Name="StartMenuShortcut" Type="integer" Value="1" KeyPath="yes"/>
                            <RemoveFolder Id="RemoveApplicationProgramsFolder"
                                          Directory="ApplicationProgramsFolder"
                                          On="uninstall"/>
                        </Component>

                        <Component Id="DesktopShortcutComponent" Guid="*" Condition="CREATEDESKTOPSHORTCUT = 1">
                            <Shortcut Id="DesktopShortcut" Directory="DesktopFolder"
                                      Name="$(var.ApplicationName)" Target="[INSTALLFOLDER]$(var.ApplicationExecutableName).exe"
                                      WorkingDirectory="INSTALLFOLDER"/>
                            <RegistryValue Root="HKCU" Key="Software\$(var.Company)\$(var.ApplicationName)\Installer"
                                           Name="DesktopShortcut" Type="integer" Value="1" KeyPath="yes"/>
                        </Component>

                        <Files Include="!(bindpath.Publish)\**">
                            <Exclude Files="!(bindpath.Publish)\$(var.ApplicationExecutableName).exe"/>
                        </Files>
                    </Directory>
                </StandardDirectory>

                <StandardDirectory Id="ProgramMenuFolder">
                    <Directory Id="ApplicationProgramsFolder" Name="$(var.ApplicationName)"/>
                </StandardDirectory>

                <ui:WixUI Id="WixUI_InstallDir" InstallDirectory="INSTALLFOLDER"/>
                <UI>
                    <Publish Dialog="LicenseAgreementDlg" Control="Next" Event="NewDialog"
                             Value="InstallOptionsDlg" Order="2" Condition="LicenseAccepted = &quot;1&quot;"/>
                    <Publish Dialog="InstallDirDlg" Control="Back" Event="NewDialog"
                             Value="InstallOptionsDlg" Order="2" Condition="NOT Installed"/>
                    <Publish Dialog="ExitDialog" Control="Finish" Event="DoAction" Value="LaunchApplication"
                             Order="1" Condition="STARTAFTERINSTALL = 1 AND NOT Installed AND NOT WIX_UPGRADE_DETECTED"/>
                    <Dialog Id="InstallOptionsDlg" Width="370" Height="270" Title="[ProductName] v[ProductVersion] Setup">
                        <Control Id="BannerBitmap" Type="Bitmap" X="0" Y="0" Width="370" Height="44"
                                 TabSkip="yes" Text="!(loc.InstallDirDlgBannerBitmap)"/>
                        <Control Id="BannerLine" Type="Line" X="0" Y="44" Width="370" Height="0"/>
                        <Control Id="Title" Type="Text" X="15" Y="15" Width="280" Height="20"
                                 Transparent="yes" NoPrefix="yes" Text="{\WixUI_Font_Title}Installation options"/>
                        <Control Id="StartMenu" Type="CheckBox" X="20" Y="75" Width="330" Height="18"
                                 Property="CREATESTARTMENUSHORTCUT" CheckBoxValue="1" Text="Create Start menu shortcut"/>
                        <Control Id="Desktop" Type="CheckBox" X="20" Y="105" Width="330" Height="18"
                                 Property="CREATEDESKTOPSHORTCUT" CheckBoxValue="1" Text="Create Desktop shortcut"/>
                        <Control Id="Launch" Type="CheckBox" X="20" Y="135" Width="330" Height="18"
                                 Property="STARTAFTERINSTALL" CheckBoxValue="1" Text="Start program after install"/>
                        <Control Id="BottomLine" Type="Line" X="0" Y="234" Width="370" Height="0"/>
                        <Control Id="Back" Type="PushButton" X="180" Y="243" Width="56" Height="17" Text="!(loc.WixUIBack)">
                            <Publish Event="NewDialog" Value="LicenseAgreementDlg"/>
                        </Control>
                        <Control Id="Next" Type="PushButton" X="236" Y="243" Width="56" Height="17" Default="yes"
                                 Text="!(loc.WixUINext)">
                            <Publish Event="NewDialog" Value="InstallDirDlg"/>
                        </Control>
                        <Control Id="Cancel" Type="PushButton" X="304" Y="243" Width="56" Height="17" Cancel="yes"
                                 Text="!(loc.WixUICancel)">
                            <Publish Event="SpawnDialog" Value="CancelDlg"/>
                        </Control>
                    </Dialog>
                </UI>
                <WixVariable Id="WixUILicenseRtf" Value="$(var.LicenseFile)"/>
                <?ifdef InstallerDialogImage ?>
                <WixVariable Id="WixUIDialogBmp" Value="$(var.InstallerDialogImage)"/>
                <?endif ?>
                <?ifdef InstallerBannerImage ?>
                <WixVariable Id="WixUIBannerBmp" Value="$(var.InstallerBannerImage)"/>
                <?endif ?>
            </Package>
        </Wix>

        """;

    private const string LocalizationTemplate =
        """
        <WixLocalization xmlns="http://wixtoolset.org/schemas/v4/wxl" Culture="en-US">
            <String Id="BrowseDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="CancelDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="DiskCostDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="ErrorDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="ExitDialog_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="FatalError_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="FilesInUse_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="MsiRMFilesInUse_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="InstallDirDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="InvalidDirDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="LicenseAgreementDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="MaintenanceWelcomeDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="MaintenanceTypeDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="PrepareDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="ProgressDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="ResumeDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="UserExit_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="VerifyReadyDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="WelcomeDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
            <String Id="WaitForCostingDlg_Title" Value="[ProductName] v[ProductVersion] Setup"/>
        </WixLocalization>

        """;

    private const string LicenseTemplate =
        """
        {\rtf1\ansi\deff0
        {\fonttbl{\f0 Segoe UI;}}
        \fs18
        \b {{SOFTWARE_NAME}} License\b0\par
        \par
        {{COPYRIGHT}}\par
        \par
        This is a placeholder license generated by the Fallout build pipeline. Replace the contents of this file with the license the installer must display before installation.\par
        \par
        The wizard shows this document on its license agreement page, so keep the file in Rich Text Format.\par
        }

        """;

    private const string ReadmeTemplate =
        """
        # Installer

        `{{PROJECT_NAME}}.wixproj` was scaffolded by the Fallout `GenerateWindowsWixInstaller` target. The publish
        pipeline builds it once per Windows runtime identifier and passes every value it needs:

        | Property | Source |
        | --- | --- |
        | `PublishDirectory` | The staged, published payload for the runtime identifier |
        | `ApplicationName` | `SoftwareName` |
        | `ApplicationExecutableName` | `SoftwareExecutableFileNameWithoutExtension` |
        | `BuildVersion` | `SoftwareVersion` |
        | `OutputName` | The release asset name |
        | `Platform` | `x64` or `arm64` |

        `Company`, `Copyright`, `RepositoryUrl`, and `ApplicationIcon` come from the repository's
        `Directory.Build.props`.

        ## Before the first release

        1. Add this project to the solution so the pipeline discovers it.
        2. Replace `Resources/License.rtf` with the real license text.
        3. Replace the placeholder artwork in `Resources/`.
        4. Keep the generated `UpgradeCode` values stable; changing one makes Windows treat future
           installers as a different product instead of an upgrade.

        ## Installer artwork

        | Property | Image size | Layout |
        | --- | --- | --- |
        | `InstallerDialogImage` | 493 × 312 pixels | Welcome and completion background. Place artwork in the leftmost 164 pixels; keep the right side clear for wizard text. |
        | `InstallerBannerImage` | 493 × 58 pixels | Header on subsequent pages, including installation options. Place the logo at the right edge and keep the left side clear for titles. |

        Use BMP or PNG images. Each property is optional; omitting it retains the corresponding WiX default,
        and a configured missing file fails the build.

        See the [WiX artwork documentation](https://docs.firegiant.com/wix/tools/wixext/wixui/#replacing-the-default-bitmaps).

        """;

    #endregion
}
