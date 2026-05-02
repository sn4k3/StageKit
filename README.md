# StageKit

[![Logo](https://raw.githubusercontent.com/sn4k3/StageKit/main/media/StageKit_landscape.svg)](#)

[![License](https://img.shields.io/github/license/sn4k3/StageKit?style=for-the-badge)](https://github.com/sn4k3/StageKit/blob/master/LICENSE)
[![GitHub repo size](https://img.shields.io/github/repo-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Code size](https://img.shields.io/github/languages/code-size/sn4k3/StageKit?style=for-the-badge)](#)
[![Nuget](https://img.shields.io/nuget/v/StageKit?style=for-the-badge)](https://www.nuget.org/packages/StageKit)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/sn4k3?color=red&style=for-the-badge)](https://github.com/sponsors/sn4k3)

StageKit is a lightweight .NET application infrastructure library for JSON settings files, observable settings objects, crash report capture, and unhandled exception handling.

## Features

- Singleton JSON settings files with lazy load and debounced save support
- Observable settings base classes with `INotifyPropertyChanged` and `INotifyPropertyChanging`
- Collection-backed settings files using `ObservableCollection<T>`
- Serializable crash reports with exception chains, stack traces, runtime information, and process stats
- AppDomain and task scheduler unhandled exception helpers
- Configurable profile, config, and log paths
- Small application "birthday" helpers for version/about screens

## Install

```bash
dotnet add package StageKit
```

## Requirements

- .NET 8 or newer
- C# latest language version

## Quick Start

Configure StageKit during application startup:

```csharp
using Microsoft.Extensions.Logging;
using StageKit;

ApplicationKit.ApplicationName = "MyApp";
ApplicationKit.ApplicationArgs = args;
ApplicationKit.Logger = logger;
ApplicationKit.UiFrameworkInfo = "Avalonia";

UnhandledExceptions.RegisterAppDomainUnhandledException();
UnhandledExceptions.RegisterTaskSchedulerUnobservedTaskException();
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
        set
        {
            if (SetProperty(ref _theme, value))
            {
                DebouncedSave();
            }
        }
    }
}
```

Use it as a lazy singleton:

```csharp
var settings = AppSettings.Instance;
settings.Theme = "Dark";

AppSettings.SaveInstance();
```

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

`ObservableObjectKit` provides property notification helpers without requiring `CommunityToolkit.Mvvm`:

```csharp
using StageKit;

public sealed class ViewState : ObservableObjectKit
{
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }
}
```

Projects that reference `CommunityToolkit.Mvvm` can still use `[ObservableProperty]` in classes derived from `ObservableObjectKit`, because the base class exposes compatible `OnPropertyChanged`, `OnPropertyChanging`, and `SetProperty` helpers.

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
ApplicationKit.Born = DateTime.SpecifyKind(new DateTime(2026, 1, 1), DateTimeKind.Utc);

Console.WriteLine(ApplicationKit.YearsOld);
Console.WriteLine(ApplicationKit.AgeShortStr);
Console.WriteLine(ApplicationKit.IsBirthday);
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
