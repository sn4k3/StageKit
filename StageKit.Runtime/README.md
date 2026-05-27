# StageKit.Runtime

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/master/LICENSE)
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
Console.WriteLine(EntryApplication.BundleType);
```

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

## Relaunch

Relaunch the current application when the executable path is known:

```csharp
if (!EntryApplication.LaunchNewInstance("--safe-mode"))
{
    // Relaunch was not available in this environment.
}
```

Prefer the enumerable overload when passing user-provided or separately computed arguments:

```csharp
EntryApplication.LaunchNewInstance("--profile", profileName);

var arguments = new List<string> { "--profile", profileName };
EntryApplication.LaunchNewInstance(arguments);
```

## Bundle Detection

`EntryApplication.BundleType` returns an `ApplicationBundleType` value:

- `None`
- `DotNetSingleFile`
- `LinuxAppImage`
- `LinuxFlatpak`
- `MacOSAppBundle`
- `Unknown`

Convenience properties are also available:

```csharp
if (EntryApplication.IsAppBundled)
{
    Console.WriteLine(EntryApplication.BundleType);
}
```

## License

StageKit.Runtime is licensed under the MIT License.
