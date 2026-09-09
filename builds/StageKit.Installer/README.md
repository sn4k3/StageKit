# Installer artwork

Set these MSBuild properties on `StageKit.Installer.wixproj` to replace the standard WiX artwork:

| Property | Image size | Layout |
| --- | --- | --- |
| `InstallerDialogImage` | 493 × 312 pixels | Welcome and completion background. Place artwork in the leftmost 164 pixels; keep the right side clear for wizard text. |
| `InstallerBannerImage` | 493 × 58 pixels | Header on subsequent pages, including installation options. Place the logo at the right edge and keep the left side clear for titles. |

Use BMP or PNG images. Pass absolute paths with `-p:InstallerDialogImage="..."` and
`-p:InstallerBannerImage="..."`, or set the properties in this project for release builds.
Each property is optional; omitting it retains the corresponding WiX default. A configured missing file fails the build.
Use artwork only, without the wizard text and controls shown in reference screenshots.

See the [WiX artwork documentation](https://docs.firegiant.com/wix/tools/wixext/wixui/#replacing-the-default-bitmaps).
