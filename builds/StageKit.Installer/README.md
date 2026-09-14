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

`InstallerScope` defaults to `perMachineOrUser` (`InstallerScope.PerMachineOrUser`), which lets the user choose the
installation scope in the wizard and initially selects a non-elevated per-user installation. Set it to `perUser`
(`InstallerScope.PerUser`) or `perMachine` (`InstallerScope.PerMachine`) in the project, via Fallout's
`WindowsInstallerOptions.Scope`, or with `-p:InstallerScope=...` to enforce that scope and hide the selector.
Unsupported values fail the build.

Windows Installer keeps a product in the context it was first installed in, so the selector is disabled while a previous
installation is detected. A silent installation is per-user unless `ALLUSERS=1` is passed:

```powershell
msiexec /i StageKit_win-x64_v0.0.0.msi ALLUSERS=1 /qn
```

## PATH registration

Set `InstallerPathRegistration` in the project, via Fallout's `WindowsInstallerOptions.PathRegistration` (using the
`PathRegistration` enum), or with `-p:InstallerPathRegistration=...`:

| Value / Enum                   | Behavior                                                         |
|--------------------------------|------------------------------------------------------------------|
| *(blank)* / `None`             | Disable PATH registration and hide the option.                   |
| `Register` / `Register`        | Always append the install directory to PATH and hide the option. |
| `UserDefaultNo` / `UserDefaultNo` | Show an unchecked option for the user.                         |
| `UserDefaultYes` / `UserDefaultYes` | Show a checked option for the user.                           |

Per-user installations update the current user's PATH; per-machine installations update the system PATH. Which of the
two applies follows the installation context Windows Installer resolved, not the scope picked in the wizard, because a
per-user installation cannot write the system PATH. The selected option is restored on upgrades, including silent ones,
and uninstall removes this install directory from PATH while preserving unrelated entries. The entry is the
installation directory as Windows Installer resolves it, so it carries a trailing separator.

## Context menu ("Open with") and file associations

File associations can be configured uniformly across all bundle formats by populating `StageKitBuild.FileAssociations`
with `FileAssociation` items, or customized for Windows directly through `WindowsInstallerOptions.ContextMenuOpenWithFileAssociations`:

```csharp
FileAssociations =
[
    new FileAssociation(".stg", "StageKit File", "application/x-stagekit")
];
```

In the WiX project or on the command line via MSBuild, configure `ContextMenuOpenWithFileAssociations` directly:

```xml
<ContextMenuOpenWithFileAssociations>.sl1;.sl1s;*.zip;*.photon</ContextMenuOpenWithFileAssociations>
```

Leave it blank to omit context menu registration. Specify `*` to show the context menu for all files.

## Shortcut & launch defaults

Configure default checkboxes in `WindowsInstallerOptions` or through MSBuild:
- `DefaultDesktopShortcut`: whether the Desktop shortcut checkbox is checked by default (`true`).
- `DefaultStartMenuShortcut`: whether the Start menu shortcut checkbox is checked by default (`true`).
- `DefaultStartProgramAfterInstall`: whether to launch the app after setup completes (`true`).
- `SingleFile`: whether the installer packages a single-file payload instead of a folder payload.

## Authenticode signing

Set `AuthenticodeCertificateThumbprint` to the SHA-1 thumbprint of a code-signing certificate in the current user's
certificate store. The build signs the staged application executable and the final MSI with SHA-256 and an RFC 3161
timestamp. Override `AuthenticodeTimestampUrl` or `SignToolPath` in `WindowsInstallerOptions` when required. Leaving
the thumbprint blank disables signing.

Every `.exe` and `.dll` in the payload is signed, which for a self-contained publish replaces the signatures the .NET
runtime binaries ship with. Redefine the `InstallerPayloadToSign` item to narrow that set:

```xml
<ItemGroup Condition="'$(SignOutput)' == 'true'">
    <InstallerPayloadToSign Remove="@(InstallerPayloadToSign)"/>
    <InstallerPayloadToSign Include="$(PublishDirectory)\$(ApplicationExecutableName).exe"/>
</ItemGroup>
```

Each file costs one timestamped `signtool` invocation, so a self-contained payload of a few hundred assemblies adds
several minutes to a signed release build.
