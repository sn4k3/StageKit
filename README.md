# StageKit

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/master/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit?style=for-the-badge)](https://www.nuget.org/packages/StageKit)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

StageKit is a lightweight .NET application infrastructure library for JSON settings files, observable settings objects, crash report capture, application runtime metadata, and unhandled exception handling.

## Features

- Singleton JSON settings files with lazy load, manual save, AutoSave, and debounced save support
- Observable settings base classes powered by `CommunityToolkit.Mvvm`
- Collection-backed settings files using `ObservableCollection<T>`
- Save hooks through `BeforeSave()` and `AfterSave()`
- Pending debounce tracking with timeout-aware wait support
- Serializable crash reports with exception chains, stack traces, runtime information, and process stats
- AppDomain and task scheduler unhandled exception helpers
- Panic-save support for registered `ISavable` settings before forced process exit
- Configurable profile, config, and log paths
- Small application "birthday" helpers for version/about screens

## Install

```bash
dotnet add package StageKit
```

## Requirements

- .NET 8 or newer
- C# latest language version
- `CommunityToolkit.Mvvm` is used by StageKit for observable settings support

## Quick Start

Configure StageKit during application startup:

```csharp
using Microsoft.Extensions.Logging;
using StageKit;

ApplicationKit.ApplicationName = "MyApp";
ApplicationKit.ApplicationArgs = args;
ApplicationKit.Logger = logger;
ApplicationKit.UiFrameworkInfo = $"Avalonia {typeof(AvaloniaObject).Assembly.GetName().Version!.ToString(3)}";
ApplicationKit.Birth = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);

UnhandledExceptions.RegisterAppDomainUnhandledException();
UnhandledExceptions.RegisterTaskSchedulerUnobservedTaskException();

CrashReportFile.IsEnabled = true;
```

## Settings Files

Create a root settings file by inheriting from `RootSettingsFile<T>`:

```csharp
using StageKit;

public sealed class AppSettings : RootSettingsFile<AppSettings>
{
    private string _theme = "System";

    public override string FileName => "appsettings.json";

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }
}
```

Enable `AutoSave` when property changes should save automatically:

```csharp
var settings = AppSettings.Instance;
settings.AutoSave = true;
settings.Theme = "Dark";
```

`AutoSave` defaults to `false`. It starts reacting only after the settings object is loaded, so property changes caused by JSON hydration or `OnLoaded(...)` do not rewrite the file during startup.

Keep `AutoSave` disabled when you prefer manual control:

```csharp
AppSettings.Instance.AutoSave = false;

AppSettings.SaveInstance();
```

Use debounced save APIs when you want to batch rapid changes:

```csharp
settings.DebouncedSave();

var saved = await settings.WaitForDebouncedSaveAsync(
    TimeSpan.FromSeconds(5),
    cancellationToken);

if (!saved)
{
    // timeout elapsed while the save was still pending
}
```

`Save()` cancels any pending debounced save before writing. `CancelDebouncedSave()` cancels a scheduled save without writing.

By default, settings are stored under:

```csharp
ApplicationKit.ConfigPath
```

Override `DirectoryPath` or set `ApplicationKit.ProfilePath` to customize storage.

## Collection Settings

Use `RootCollectionFile<T, TO>` when a settings file is mainly a list:

```csharp
using StageKit;

public sealed class RecentFiles : RootCollectionFile<RecentFiles, string>
{
    public override string FileName => "recent-files.json";

    public override int TrimCollectionWhenExceeding => 20;

    public override CollectionSide TrimCollectionSide => CollectionSide.Head;
}
```

```csharp
RecentFiles.Instance.Add(@"C:\work\project.txt");
RecentFiles.SaveInstance();
```

## Observable Objects

`SubSettings` is based on `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`:

```csharp
using StageKit;

public sealed class ViewState : SubSettings
{
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
}
```

Classes deriving from `SubSettings`, `RootSettingsFile<T>`, or `RootCollectionFile<T, TO>` can use CommunityToolkit's `[ObservableProperty]` generator:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using StageKit;

public sealed partial class AppSettings : RootSettingsFile<AppSettings>
{
    public override string FileName => "appsettings.json";

    [ObservableProperty]
    private string _theme = "System";
}
```

StageKit references `CommunityToolkit.Mvvm`, but if your app uses generator attributes such as `[ObservableProperty]`, reference `CommunityToolkit.Mvvm` directly in that app too so the analyzer/source generator runs in the consuming project.

## Crash Reports

Create crash reports directly:

```csharp
try
{
    RunApplication();
}
catch (Exception ex)
{
    var report = new CrashReport(ex, "Startup");
    Console.WriteLine(report.FormattedMessage);
}
```

StageKit can also capture unhandled exceptions:

```csharp
UnhandledExceptions.HandleCrashReport = report =>
{
    Console.WriteLine(report.FormattedMessage);
    return true;
};
```

If `CrashReportFile.IsEnabled` is `true`, fatal unhandled exceptions are added to `CrashReportFile.Instance`.

Register settings that should be saved before StageKit forces process exit after a fatal exception:

```csharp
UnhandledExceptions.SettingsFilesToSaveBeforeCrash.Add(AppSettings.Instance);
```

`SettingsFilesToSaveBeforeCrash` stores `StageKit.Interfaces.ISavable`, so any object with a public `Save()` implementation can participate.

## Ignoring Known Safe Exceptions

Ignore by exception type:

```csharp
UnhandledExceptions.IgnoredExceptionList.Add(typeof(OperationCanceledException));
```

Ignore by message fragment:

```csharp
UnhandledExceptions.IgnoredExceptionMessages.Add("known benign message");
```

Avalonia DBus noise can be ignored with:

```csharp
UnhandledExceptions.IgnoreAvaloniaSafeExceptions();
```

## Application Birthday Helpers

```csharp
ApplicationKit.Birth = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);

Console.WriteLine(ApplicationKit.YearsOld);
Console.WriteLine(ApplicationKit.AgeShortStr);
Console.WriteLine(ApplicationKit.IsBirthday);
```

Runtime duration since library initialization is available through:

```csharp
Console.WriteLine(ApplicationKit.RuntimeElapsed);
```

## Demo

See [StageKit.Demo/Program.cs](StageKit.Demo/Program.cs) for a runnable console demo covering startup configuration, Serilog integration, AutoSave settings, collection settings, panic-save registration, crash report launch handling, and debounced save waiting.

Run it with:

```bash
dotnet run --project StageKit.Demo/StageKit.Demo.csproj
```

## Development

Restore, build, and test:

```bash
dotnet restore
dotnet build
dotnet test
```

## Security

Please report vulnerabilities privately. See [SECURITY.md](SECURITY.md).

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

StageKit is licensed under the [MIT License](LICENSE).
