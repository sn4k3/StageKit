using Fallout.Common.IO;

namespace StageKit.Fallout;

/// <summary>
/// Defines the options and metadata used to build Windows installers (WiX MSI).
/// </summary>
public class WindowsInstallerOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsInstallerOptions"/> class.
    /// </summary>
    public WindowsInstallerOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsInstallerOptions"/> class using the specified build information.
    /// </summary>
    /// <param name="build">The build information.</param>
    public WindowsInstallerOptions(StageKitBuild build)
    {
        ArgumentNullException.ThrowIfNull(build);

        AuthenticodeCertificateThumbprint = build.WindowsAuthenticodeCertificateThumbprint;

        IconFile = build.WindowsIconFile;

        FileAssociations.UnionWith(build.FileAssociations);

        var contextMenuSetting = build.GetMainProjectProperty("ContextMenuOpenWithFileAssociations");
        if (!string.IsNullOrWhiteSpace(contextMenuSetting))
        {
            var tokens = contextMenuSetting.Split([';', ','], StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var trimmed = token.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    ContextMenuOpenWithFileAssociations.Add(new FileAssociation(trimmed));
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the file associations configured for the Windows installer to register their capabilities in the registry.
    /// </summary>
    public HashSet<FileAssociation> FileAssociations { get; set; } = [];

    /// <summary>
    /// Gets or sets the file associations registered in the Windows Explorer "Open with" context menu.
    /// </summary>
    /// <remarks>
    /// If empty, context menu registration is omitted. If <c>*</c> is present in the association extensions, the context menu registers for all files.
    /// Extensions and patterns can be specified with or without leading dots/wildcards (e.g. <c>.sl1</c>, <c>sl1</c>, <c>*.zip</c>).
    /// </remarks>
    public HashSet<FileAssociation> ContextMenuOpenWithFileAssociations { get; set; } = [];

    /// <summary>
    /// Gets or sets the path or command name of the Microsoft SignTool executable used to sign Windows installers.
    /// </summary>
    /// <value>The path or command name of <c>signtool.exe</c>. The default is <c>signtool.exe</c>.</value>
    public string SignToolPath { get; set; } = "signtool.exe";

    /// <summary>
    /// Gets or sets the SHA-1 thumbprint of the Authenticode certificate used to sign Windows installers.
    /// </summary>
    /// <value>The certificate thumbprint, or <see langword="null"/> to disable signing.</value>
    public string? AuthenticodeCertificateThumbprint { get; set; }

    /// <summary>
    /// Gets or sets the RFC 3161 timestamp URL used when signing Windows installers.
    /// </summary>
    /// <value>The timestamp server URL. The default is <c>http://timestamp.digicert.com</c>.</value>
    public string AuthenticodeTimestampUrl { get; set; } = "http://timestamp.digicert.com";

    /// <summary>
    /// Gets or sets a value indicating whether Windows installers use the single-file executable as their payload.
    /// </summary>
    /// <value><see langword="true"/> if the single-file executable is used; otherwise, <see langword="false"/>. The default is <see langword="false"/>.</value>
    public bool UseSingleFile { get; set; }

    /// <summary>
    /// Gets or sets the application icon file (.ico) used for the Windows installer.
    /// </summary>
    /// <value>The icon file path, or <see langword="null"/> to use <see cref="StageKitBuild.WindowsIconFile"/>.</value>
    public AbsolutePath? IconFile { get; set; }

    /// <summary>
    /// Synchronizes the context menu "Open with" file associations with the main file associations.
    /// </summary>
    public void SyncContextMenuOpenWithFileAssociations()
    {
        ContextMenuOpenWithFileAssociations.Clear();
        ContextMenuOpenWithFileAssociations.UnionWith(FileAssociations);
    }
}
