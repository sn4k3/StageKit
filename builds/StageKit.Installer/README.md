# Installer artwork

Set these MSBuild properties on `StageKit.Installer.wixproj` to replace the standard WiX artwork:

| Property               | Image size       | Layout                                                                                                                                |
|------------------------|------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| `InstallerDialogImage` | 493 × 312 pixels | Welcome and completion background. Place artwork in the leftmost 164 pixels; keep the right side clear for wizard text.               |
| `InstallerBannerImage` | 493 × 58 pixels  | Header on subsequent pages, including installation options. Place the logo at the right edge and keep the left side clear for titles. |

Use BMP or PNG images. Pass absolute paths with `-p:InstallerDialogImage="..."` and
`-p:InstallerBannerImage="..."`, or set the properties in this project for release builds. Each property is optional;
omitting it retains the corresponding WiX default. A configured missing file fails the build. Use artwork only, without
the wizard text and controls shown in reference screenshots.

See the [WiX artwork documentation](https://docs.firegiant.com/wix/tools/wixext/wixui/#replacing-the-default-bitmaps).

## Installation scope

`InstallerScope` defaults to `perMachineOrUser`, which lets the user choose the installation scope in the wizard and
initially selects a non-elevated per-user installation. Set it to `perUser` or `perMachine` in the project or with
`-p:InstallerScope=...` to enforce that scope and hide the selector. Unsupported values fail the build.

## PATH registration

Set `InstallerPathRegistration` in the project or with `-p:InstallerPathRegistration=...`:

| Value            | Behavior                                                         |
|------------------|------------------------------------------------------------------|
| *(blank)*        | Disable PATH registration and hide the option.                   |
| `Register`       | Always append the install directory to PATH and hide the option. |
| `UserDefaultNo`  | Show an unchecked option for the user.                           |
| `UserDefaultYes` | Show a checked option for the user.                              |

Per-user installations update the current user's PATH; per-machine installations update the system PATH. The selected
option is restored on upgrades, and uninstall removes this install directory from PATH while preserving unrelated
entries.

## Authenticode signing

Set `AuthenticodeCertificateThumbprint` to the SHA-1 thumbprint of a code-signing certificate in the current user's
certificate store. The build signs the staged application executable and the final MSI with SHA-256 and an RFC 3161
timestamp. Override `AuthenticodeTimestampUrl` or `SignToolPath` when required. Leaving the thumbprint blank disables
signing.
