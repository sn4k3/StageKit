# StageKit.Runtime

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/main/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit.Runtime?style=for-the-badge)](https://www.nuget.org/packages/StageKit.Runtime)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

StageKit.Runtime provides runtime and entry-application helpers for StageKit libraries and .NET apps.

## Features

- Entry assembly metadata helpers through `EntryApplication`
- Process and executable path discovery
- Runtime identifier helper
- .NET single-file, Linux AppImage, Linux Flatpak, and macOS `.app` bundle detection
- Formatted application and loaded-assembly diagnostics
- Runtime, process, and entry-application diagnostic reports
- Lazy, Native AOT-compatible access to Fallout build-runtime manifests
- Best-effort application relaunch helper

## Install

```bash
dotnet add package StageKit.Runtime
```

## Requirements

- .NET 8 or newer
- C# latest language version

## EntryApplication

Use `EntryApplication` to inspect the process entry application and deployment shape.

```csharp
using StageKit.Runtime;

Console.WriteLine(EntryApplication.AssemblyTitle);
Console.WriteLine(EntryApplication.AssemblyVersionString);
Console.WriteLine(EntryApplication.GenericRuntimeIdentifier);
Console.WriteLine(EntryApplication.ExecutablePath);
Console.WriteLine(EntryApplication.PackagingType);
```

`ExecutablePath` is bundle-aware. It can report an AppImage path, a macOS `.app` path, a .NET single-file host path, a
hosted `dotnet` assembly path, the process path, or finally `Environment.GetCommandLineArgs()[0]` when the usual runtime
path APIs are unavailable.

Capture a diagnostics block:

```csharp
Console.WriteLine(EntryApplication.ApplicationInfo);
Console.WriteLine(EntryApplication.FormattedLoadedAssemblies);
```

## RuntimeDiagnostics

Use `RuntimeDiagnostics` when you want a combined report for logs, support bundles, or crash reports.

```csharp
using StageKit.Runtime;

Console.WriteLine(RuntimeDiagnostics.GetReport());
Console.WriteLine(RuntimeDiagnostics.GetReport(includeLoadedAssemblies: true));

Dictionary<string, string?> info = RuntimeDiagnostics.GetInfoDict();
```

## Build Runtime Manifest

`BuildRuntime.Instance` lazily loads `build-runtime.json` from the application base, entry-assembly, or process
directory. It is `null` when no valid manifest is available:

```csharp
BuildRuntime? build = BuildRuntime.Instance;

if (build is not null)
    Console.WriteLine($"{build.Runtime}: {build.PackagingType}");
```

Use `BuildRuntime.TryLoad(...)` when Fallout is configured with a custom manifest file name:

```csharp
if (BuildRuntime.TryLoad(customManifestPath, out BuildRuntime? build))
    Console.WriteLine(build.BuildVersion);
```

Manifest deserialization uses source-generated `System.Text.Json` metadata and the generic enum converter, so it does
not depend on reflection-based serialization and is compatible with trimming and Native AOT.

## Relaunch

Relaunch the current application when the executable path is known:

```csharp
if (!EntryApplication.LaunchNewInstance("--safe-mode"))
{
    // Relaunch was not available in this environment.
}
```

Use the `params string[]` or enumerable overload when passing user-provided or separately computed arguments:

```csharp
EntryApplication.LaunchNewInstance("--profile", profileName);

var arguments = new List<string> { "--profile", profileName };
EntryApplication.LaunchNewInstance(arguments);
```

The string overload accepts one raw command-line argument string. The structured overloads pass each argument separately
and let the process launcher handle quoting.

## Packaging Detection

`EntryApplication.PackagingType` returns an `ApplicationPackagingType` value:

- `None`
- `Portable`
- `DotNetSingleFile`
- `WindowsInstaller`
- `LinuxAppImage`
- `LinuxFlatpak`
- `LinuxDeb`
- `LinuxRpm`
- `LinuxArchPackage`
- `LinuxSnap`
- `MacOSAppBundle`
- `MacOSDmg`
- `MacOSPkg`

`ApplicationPackagingInfo.KnownPackagingTypes` provides metadata for these formats. Enumerating that dictionary uses
the preferred package-selection order: platform-native packages come first, generic bundles follow, and Portable is
the final fallback. Consumers such as Fallout's generated installer preserve this order.

Runtime detection checks the standard AppImage, Flatpak, Snap, and macOS bundle markers first, then reads the
`build-runtime.json` manifest emitted by Fallout when it is available. This manifest enables detection of DEB, RPM,
Arch Linux, Snap, Windows Installer, DMG, and PKG packaging. Applications without a marker or manifest report
`Portable`; an ordinary macOS `.app` without a manifest reports `MacOSAppBundle`.

Convenience properties are also available:

```csharp
if (EntryApplication.IsAppBundled)
{
    Console.WriteLine(EntryApplication.PackagingType);
}
```

## License

StageKit.Runtime is licensed under the MIT License.
