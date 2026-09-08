using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using StageKit.Primitives;
using StageKit.Primitives.Extensions;
using StageKit.Primitives.System;

namespace StageKit.Tests;

public sealed class PrimitivesTests
{
    [Fact]
    public void HostSystem_OperatingSystemName_DescribesCurrentHost()
    {
        Assert.True(new[]
        {
            "Windows", "Mac Catalyst", "macOS", "Android", "iOS", "tvOS", "watchOS", "Linux", "FreeBSD",
            "Browser", "WASI", "Unknown"
        }.Contains(HostSystem.OperatingSystemName, StringComparer.Ordinal));
        Assert.Equal(
            $"{HostSystem.OperatingSystemName} {RuntimeInformation.OSArchitecture}",
            HostSystem.OperatingSystemNameWithArch);
    }

    [Fact]
    public void HostSystem_TryFindExecutable_FindsHostShell()
    {
        var executable = OperatingSystem.IsWindows() ? "cmd" : "sh";

        Assert.True(HostSystem.TryFindExecutable(executable, out var result));
        Assert.True(Path.IsPathFullyQualified(result));
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void HostSystem_TryFindExecutable_ValidatesExplicitPathForHost()
    {
        var directoryPath = CreateTempDirectory();

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var pathExtensions = Environment.GetEnvironmentVariable("PATHEXT");
                var executableExtension = pathExtensions?.Split(
                        Path.PathSeparator,
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault() ?? ".EXE";

                if (!executableExtension.StartsWith('.'))
                    executableExtension = string.Concat('.', executableExtension);

                var pathWithoutExtension = Path.Combine(directoryPath, "stagekit-tool");
                var executablePath = string.Concat(pathWithoutExtension, executableExtension);
                File.WriteAllText(executablePath, string.Empty);

                Assert.True(HostSystem.TryFindExecutable(pathWithoutExtension, out var result));
                Assert.Equal(Path.GetFullPath(executablePath), result);
            }
            else
            {
                var executablePath = Path.Combine(directoryPath, "stagekit-tool");
                File.WriteAllText(executablePath, "#!/bin/sh\nexit 0\n");
                File.SetUnixFileMode(executablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

                Assert.False(HostSystem.TryFindExecutable(executablePath, out var nonExecutableResult));
                Assert.Null(nonExecutableResult);

                File.SetUnixFileMode(
                    executablePath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                Assert.True(HostSystem.TryFindExecutable(executablePath, out var result));
                Assert.Equal(Path.GetFullPath(executablePath), result);
            }
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void HostSystem_TryFindExecutable_RejectsEmptyName(string executable)
    {
        Assert.False(HostSystem.TryFindExecutable(executable, out var result));
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative-url")]
    [InlineData("file:///tmp/stagekit.txt")]
    public async Task HostSystem_OpenUrl_RejectsInvalidOrFileUrl(string url)
    {
        Assert.False(HostSystem.OpenUrl(url));
        Assert.False(await HostSystem.OpenUrlAsync(url, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(800, 150, 800, 150)]
    [InlineData(20, 150, 37, 150)] // Console.Beep throws below 37 Hz.
    [InlineData(int.MinValue, int.MinValue, 37, 40)]
    [InlineData(int.MaxValue, int.MaxValue, 20_000, int.MaxValue)]
    [InlineData(800, 0, 800, 40)] // Console.Beep throws on a non-positive duration.
    public void HostSystem_NormalizeBeepArguments_ClampsToHostAcceptedRange(
        int frequency,
        int duration,
        int expectedFrequency,
        int expectedDuration)
    {
        var (normalizedFrequency, normalizedDuration) = HostSystem.NormalizeBeepArguments(frequency, duration);

        Assert.Equal(expectedFrequency, normalizedFrequency);
        Assert.Equal(expectedDuration, normalizedDuration);
    }

    [Fact]
    public void HostSystem_CreateBeepShellCommand_FormatsDurationInvariantly()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            // A comma-decimal culture would emit "sleep 0,15", which bash rejects as an invalid time interval.
            CultureInfo.CurrentCulture = new CultureInfo("pt-PT");

            var command = HostSystem.CreateBeepShellCommand(800, 150);

            Assert.Contains("sleep 0.15", command, StringComparison.Ordinal);
            Assert.DoesNotContain(",", command, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void HostSystem_CreateBeepShellCommand_ProbesToneUtilitiesBeforeUse()
    {
        var command = HostSystem.CreateBeepShellCommand(800, 150);

        // The probe must be unquoted, otherwise the test is a non-empty literal and always succeeds.
        Assert.Contains("if command -v speaker-test > /dev/null 2>&1; then", command, StringComparison.Ordinal);
        Assert.DoesNotContain("'$(command -v speaker-test)'", command, StringComparison.Ordinal);
        Assert.Contains("osascript", command, StringComparison.Ordinal);
        Assert.Contains("printf '\\a'", command, StringComparison.Ordinal);
    }

    [Fact]
    public void HostSystem_CreateOpenTargetStartInfo_UsesHostLauncher()
    {
        const string target = "https://example.com/a%20path";

        var startInfo = HostSystem.CreateOpenTargetStartInfo(target);

        Assert.NotNull(startInfo);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(target, startInfo.FileName);
            Assert.True(startInfo.UseShellExecute);
            Assert.Empty(startInfo.ArgumentList);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("/usr/bin/open", startInfo.FileName);
            Assert.Equal([target], startInfo.ArgumentList);
            Assert.False(startInfo.UseShellExecute);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("xdg-open", startInfo.FileName);
            Assert.Equal([target], startInfo.ArgumentList);
            Assert.False(startInfo.UseShellExecute);
        }
    }

    [Fact]
    public void HostSystem_CreateShowFileInFileManagerStartInfo_UsesHostLauncher()
    {
        var filePath = Path.GetFullPath(Path.Combine("folder with spaces", "file.txt"));

        var startInfo = HostSystem.CreateShowFileInFileManagerStartInfo(filePath);

        Assert.NotNull(startInfo);
        Assert.False(startInfo.UseShellExecute);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("explorer.exe", startInfo.FileName);
            Assert.Equal([string.Concat("/select,", filePath)], startInfo.ArgumentList);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("/usr/bin/open", startInfo.FileName);
            Assert.Equal(["-R", filePath], startInfo.ArgumentList);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("xdg-open", startInfo.FileName);
            Assert.Equal([Path.GetDirectoryName(filePath)!], startInfo.ArgumentList);
        }
    }

    [Fact]
    public async Task HostSystem_PathOpenHelpers_RejectMissingPaths()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"stagekit-missing-{Guid.NewGuid():N}");

        Assert.False(HostSystem.OpenDirectory(missingPath));
        Assert.False(HostSystem.OpenFile(missingPath));
        Assert.False(HostSystem.ShowFileInFileManager(missingPath));
        Assert.False(await HostSystem.OpenDirectoryAsync(missingPath, TestContext.Current.CancellationToken));
        Assert.False(await HostSystem.OpenFileAsync(missingPath, TestContext.Current.CancellationToken));
        Assert.False(await HostSystem.ShowFileInFileManagerAsync(
            missingPath,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HostSystem_AsyncOpenHelpers_HonorPreCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            HostSystem.OpenUrlAsync("https://example.com", cancellationTokenSource.Token));
    }

    [Fact]
    public void ProcessHelper_StartProcess_ReturnsExitCodeWhenWaitingForCompletion()
    {
        var (name, arguments) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/d", "/c", "exit 7" })
            : ("/bin/sh", new[] { "-c", "exit 7" });

        var exitCode = ProcessHelper.StartProcess(name, arguments, waitForCompletion: true);

        Assert.Equal(7, exitCode);
    }

    [Fact]
    public async Task ProcessHelper_StartProcessAsync_ReturnsExitCodeWhenWaitingForCompletion()
    {
        var (name, arguments) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/d", "/c", "exit 7" })
            : ("/bin/sh", new[] { "-c", "exit 7" });

        var exitCode = await ProcessHelper.StartProcessAsync(
            name,
            arguments,
            waitForCompletion: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(7, exitCode);
    }

    [Fact]
    public void ProcessHelper_StartProcess_ProcessStartInfo_ReturnsExitCode()
    {
        var startInfo = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh");
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "/d" : "-c");
        if (OperatingSystem.IsWindows()) startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("exit 8");

        var exitCode = ProcessHelper.StartProcess(startInfo, waitForCompletion: true);

        Assert.Equal(8, exitCode);
    }

    [Fact]
    public void ProcessHelper_StartProcessWithShellExecute_OverloadsReturnExitCodes()
    {
        var name = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var rawArguments = OperatingSystem.IsWindows() ? "/d /c \"exit /b 9\"" : "-c \"exit 9\"";
        var argumentList = OperatingSystem.IsWindows()
            ? new[] { "/d", "/c", "exit 10" }
            : ["-c", "exit 10"];
        var startInfo = new ProcessStartInfo(name);
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "/d" : "-c");
        if (OperatingSystem.IsWindows()) startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("exit 11");

        var rawArgumentsExitCode = ProcessHelper.StartProcessWithShellExecute(
            name,
            rawArguments,
            waitForCompletion: true);
        var argumentListExitCode = ProcessHelper.StartProcessWithShellExecute(
            name,
            argumentList,
            waitForCompletion: true);
        var startInfoExitCode = ProcessHelper.StartProcessWithShellExecute(startInfo, waitForCompletion: true);

        Assert.Equal(9, rawArgumentsExitCode);
        Assert.Equal(10, argumentListExitCode);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal(11, startInfoExitCode);
    }

    [Fact]
    public async Task ProcessHelper_StartProcessWithShellExecuteAsync_OverloadsReturnExitCodes()
    {
        var name = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var rawArguments = OperatingSystem.IsWindows() ? "/d /c \"exit /b 12\"" : "-c \"exit 12\"";
        var argumentList = OperatingSystem.IsWindows()
            ? new[] { "/d", "/c", "exit 13" }
            : ["-c", "exit 13"];
        var startInfo = new ProcessStartInfo(name);
        startInfo.ArgumentList.Add(OperatingSystem.IsWindows() ? "/d" : "-c");
        if (OperatingSystem.IsWindows()) startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("exit 14");

        var rawArgumentsExitCode = await ProcessHelper.StartProcessWithShellExecuteAsync(
            name,
            rawArguments,
            waitForCompletion: true,
            cancellationToken: TestContext.Current.CancellationToken);
        var argumentListExitCode = await ProcessHelper.StartProcessWithShellExecuteAsync(
            name,
            argumentList,
            waitForCompletion: true,
            cancellationToken: TestContext.Current.CancellationToken);
        var startInfoExitCode = await ProcessHelper.StartProcessWithShellExecuteAsync(
            startInfo,
            waitForCompletion: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(12, rawArgumentsExitCode);
        Assert.Equal(13, argumentListExitCode);
        Assert.True(startInfo.UseShellExecute);
        Assert.Equal(14, startInfoExitCode);
    }

    [Fact]
    public void ProcessHelper_StartShell_ReturnsShellExitCodeWhenWaitingForCompletion()
    {
        var command = OperatingSystem.IsWindows() ? "exit /b 6" : "exit 6";

        var exitCode = ProcessHelper.StartShell(command, waitForCompletion: true);

        Assert.Equal(6, exitCode);
    }

    [Fact]
    public async Task ProcessHelper_StartShellAsync_ReturnsShellExitCodeWhenWaitingForCompletion()
    {
        var command = OperatingSystem.IsWindows() ? "exit /b 6" : "exit 6";

        var exitCode = await ProcessHelper.StartShellAsync(
            command,
            waitForCompletion: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(6, exitCode);
    }

    [Fact]
    public async Task ProcessHelper_StartShellAsync_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProcessHelper.StartShellAsync(
                "ignored",
                waitForCompletion: true,
                cancellationToken: cancellationSource.Token));
    }

    [Fact]
    public void ProcessHelper_GetShellOutput_CapturesBothStreamsAndExitCode()
    {
        var command = OperatingSystem.IsWindows()
            ? "echo standard&echo error 1>&2&exit /b 5"
            : "printf standard; printf error >&2; exit 5";

        var output = ProcessHelper.GetShellOutput(command);

        Assert.Equal(5, output.ExitCode);
        Assert.Equal("standard", output.StandardOutput.Trim());
        Assert.Equal("error", output.StandardError.Trim());
        Assert.False(output.Succeeded);
    }

    [Fact]
    public async Task ProcessHelper_GetShellOutputAsync_CapturesBothStreamsAndExitCode()
    {
        var command = OperatingSystem.IsWindows()
            ? "echo standard&echo error 1>&2&exit /b 5"
            : "printf standard; printf error >&2; exit 5";

        var output = await ProcessHelper.GetShellOutputAsync(
            command,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(5, output.ExitCode);
        Assert.Equal("standard", output.StandardOutput.Trim());
        Assert.Equal("error", output.StandardError.Trim());
        Assert.False(output.Succeeded);
    }

    [Fact]
    public async Task ProcessHelper_GetProcessOutputAsync_ProcessStartInfo_UsesCustomConfiguration()
    {
        var directoryPath = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directoryPath, "working-directory.txt"), "working directory");

        try
        {
            var command = OperatingSystem.IsWindows()
                ? "echo %STAGEKIT_PROCESS_HELPER_TEST%&type working-directory.txt"
                : "printf '%s\\n' \"$STAGEKIT_PROCESS_HELPER_TEST\"; cat working-directory.txt";
            var startInfo = ProcessHelper.CreateShellProcessStartInfo(command);
            startInfo.WorkingDirectory = directoryPath;
            startInfo.Environment["STAGEKIT_PROCESS_HELPER_TEST"] = "custom environment";

            var output = await ProcessHelper.GetProcessOutputAsync(
                startInfo,
                TestContext.Current.CancellationToken);
            var outputLines = output.StandardOutput.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Assert.Equal(0, output.ExitCode);
            Assert.Equal(["custom environment", "working directory"], outputLines);
            Assert.False(startInfo.UseShellExecute);
            Assert.True(startInfo.RedirectStandardOutput);
            Assert.True(startInfo.RedirectStandardError);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ProcessHelper_CreateShellProcessStartInfo_UsesHostShellWithoutElevation()
    {
        const string command = "echo stagekit";

        var startInfo = ProcessHelper.CreateShellProcessStartInfo(command);

        Assert.Equal(OperatingSystem.IsWindows() ? "cmd.exe" : "bash", startInfo.FileName);
        Assert.Equal(
            OperatingSystem.IsWindows() ? ["/d", "/c", command] : ["-c", command],
            startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.Verb);
    }

    [Fact]
    public void ProcessHelper_CreateShellProcessStartInfo_PrependsHostCommandSwitchesToArgumentList()
    {
        string[] arguments = ["echo stagekit", "argument zero"];

        var startInfo = ProcessHelper.CreateShellProcessStartInfo(arguments);

        Assert.Equal(OperatingSystem.IsWindows() ? "cmd.exe" : "bash", startInfo.FileName);
        Assert.Equal(
            OperatingSystem.IsWindows()
                ? ["/d", "/c", .. arguments]
                : ["-c", .. arguments],
            startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.Verb);
    }

    [Fact]
    public void ProcessHelper_IsExitCodeElevationDenied_AcceptsSuccessAndOrdinaryFailuresAsNotDenied()
    {
        Assert.False(ProcessHelper.IsExitCodeElevationDenied(0));
        Assert.False(ProcessHelper.IsExitCodeElevationDenied(-1));
        Assert.False(ProcessHelper.IsExitCodeElevationDenied(2));
    }

    [Fact]
    public void ProcessHelper_IsExitCodeElevationDenied_MatchesTheHostElevationMechanism()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.True(
                ProcessHelper.IsExitCodeElevationDenied(ProcessHelper.WindowsElevationCancelledExitCode));

            // pkexec codes carry no meaning on Windows.
            Assert.False(ProcessHelper.IsExitCodeElevationDenied(ProcessHelper.LinuxElevationDismissedExitCode));
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.True(ProcessHelper.IsExitCodeElevationDenied(ProcessHelper.LinuxElevationDismissedExitCode));
            Assert.True(ProcessHelper.IsExitCodeElevationDenied(ProcessHelper.LinuxElevationNotAuthorizedExitCode));
            Assert.False(ProcessHelper.IsExitCodeElevationDenied(ProcessHelper.WindowsElevationCancelledExitCode));
        }
        else
        {
            // A cancelled macOS prompt shares its exit code with an ordinary failure.
            Assert.False(ProcessHelper.IsExitCodeElevationDenied(ProcessHelper.MacOSElevationCancelledExitCode));
        }
    }

    [Fact]
    public void ProcessHelper_IsExitCodeElevationDenied_UsesStandardErrorForTheMacOSPrompt()
    {
        const string cancelledError = "execution error: User canceled. (-128)";

        Assert.Equal(
            OperatingSystem.IsMacOS(),
            ProcessHelper.IsExitCodeElevationDenied(
                ProcessHelper.MacOSElevationCancelledExitCode,
                cancelledError));

        // A failing command without the cancellation marker is never a denial.
        Assert.False(
            ProcessHelper.IsExitCodeElevationDenied(
                ProcessHelper.MacOSElevationCancelledExitCode,
                "installer: Error - the package could not be opened."));
    }

    [Fact]
    public async Task ProcessExtensions_IsExitCodeElevationDenied_ReadsTheExitedProcess()
    {
        var startInfo = ProcessHelper.CreateShellProcessStartInfo("exit 0");
        using var process = Process.Start(startInfo);

        Assert.NotNull(process);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.False(process.IsExitCodeElevationDenied());
    }

    [Fact]
    public void ProcessExtensions_IsExitCodeElevationDenied_ClassifiesCapturedOutput()
    {
        var succeeded = new ProcessOutput(0, string.Empty, string.Empty);
        Assert.False(succeeded.IsExitCodeElevationDenied());

        var failed = new ProcessOutput(2, string.Empty, "installer: Error - the package could not be opened.");
        Assert.False(failed.IsExitCodeElevationDenied());

        // The captured standard error resolves the macOS prompt an exit code alone cannot.
        var cancelled = new ProcessOutput(
            ProcessHelper.MacOSElevationCancelledExitCode,
            string.Empty,
            "execution error: User canceled. (-128)");

        Assert.Equal(OperatingSystem.IsMacOS(), cancelled.IsExitCodeElevationDenied());
    }

    [Fact]
    public void ProcessHelper_CreateShellScriptProcessStartInfo_PreservesScriptPathWithSpaces()
    {
        const string scriptFilePath = "/tmp/a directory with spaces/upgrade script.sh";

        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(scriptFilePath);

        Assert.Equal(OperatingSystem.IsWindows() ? "cmd.exe" : "bash", startInfo.FileName);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("/d /s /c \"\"/tmp/a directory with spaces/upgrade script.sh\"\"", startInfo.Arguments);
            Assert.Empty(startInfo.ArgumentList);
        }
        else
        {
            Assert.Equal([scriptFilePath], startInfo.ArgumentList);
        }

        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.Verb);
    }

    [Fact]
    public void ProcessHelper_CreateShellScriptProcessStartInfo_PreservesScriptArgumentBoundaries()
    {
        const string scriptFilePath = "/tmp/a directory with spaces/upgrade script.sh";

        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(
            scriptFilePath,
            ["first", "two words"]);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(
                "/d /s /c \"\"/tmp/a directory with spaces/upgrade script.sh\" \"first\" \"two words\"\"",
                startInfo.Arguments);
            Assert.Empty(startInfo.ArgumentList);
        }
        else
        {
            Assert.Equal([scriptFilePath, "first", "two words"], startInfo.ArgumentList);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessHelper_CreateShellScriptProcessStartInfo_RejectsMissingScriptPath(string? scriptFilePath)
    {
        // Null reports ArgumentNullException, empty and white space report ArgumentException.
        Assert.ThrowsAny<ArgumentException>(() => ProcessHelper.CreateShellScriptProcessStartInfo(scriptFilePath!));
    }

    [Fact]
    public async Task ProcessHelper_CreateShellScriptProcessStartInfo_RunsScriptStoredUnderAPathWithSpaces()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"stagekit script test {Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);

        try
        {
            var scriptFilePath = Path.Combine(
                directoryPath,
                OperatingSystem.IsWindows() ? "upgrade script.bat" : "upgrade script.sh");

            await File.WriteAllTextAsync(
                scriptFilePath,
                OperatingSystem.IsWindows()
                    ? "@echo off\r\necho %~1^|%~2\r\n"
                    : "#!/usr/bin/env bash\nprintf '%s|%s' \"$1\" \"$2\"\n",
                TestContext.Current.CancellationToken);

            string[] arguments = ["first", "two words"];
            var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(scriptFilePath, arguments);
            var output = await ProcessHelper.GetProcessOutputAsync(
                startInfo,
                TestContext.Current.CancellationToken);
            var exitCode = await ProcessHelper.StartShellScriptAsync(
                scriptFilePath,
                arguments,
                waitForCompletion: true,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(0, output.ExitCode);
            Assert.Equal("first|two words", output.StandardOutput.Trim());
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ProcessHelper_CreateShellProcessStartInfo_UsesHostElevationMechanism()
    {
        const string command = "echo stagekit";

        var startInfo = ProcessHelper.CreateShellProcessStartInfo(
            command,
            requireElevation: true,
            isPrivilegedProcess: false);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("cmd.exe", startInfo.FileName);
            Assert.Equal(["/d", "/c", command], startInfo.ArgumentList);
            Assert.Equal("runas", startInfo.Verb);
            Assert.True(startInfo.UseShellExecute);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("pkexec", startInfo.FileName);
            Assert.Equal(["bash", "-c", command], startInfo.ArgumentList);
            Assert.False(startInfo.UseShellExecute);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("osascript", startInfo.FileName);
            Assert.Equal("-e", startInfo.ArgumentList[0]);
            Assert.Equal(
                "do shell script \"'bash' '-c' 'echo stagekit'\" with administrator privileges",
                startInfo.ArgumentList[1]);
            Assert.False(startInfo.UseShellExecute);
        }
    }

    [Fact]
    public void ProcessHelper_CreateProcessStartInfo_PreservesArgumentListWithoutElevation()
    {
        var startInfo = ProcessHelper.CreateProcessStartInfo("tool", ["first", "two words"], requireElevation: false);

        Assert.Equal("tool", startInfo.FileName);
        Assert.Equal(["first", "two words"], startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.Verbs);
    }

    [Fact]
    public void ProcessHelper_CreateHostProcessStartInfo_OutsideFlatpak_UsesCommandDirectly()
    {
        var startInfo = ProcessHelper.CreateHostProcessStartInfo(
            "tool",
            ["first", "two words"],
            requireElevation: false,
            isFlatpakSandbox: false,
            isPrivilegedProcess: false);

        Assert.Equal("tool", startInfo.FileName);
        Assert.Equal(["first", "two words"], startInfo.ArgumentList);
    }

    [Fact]
    public void ProcessHelper_CreateHostProcessStartInfo_InsideFlatpak_WrapsCommandAndArguments()
    {
        var startInfo = ProcessHelper.CreateHostProcessStartInfo(
            "tool",
            ["first", "two words"],
            requireElevation: false,
            isFlatpakSandbox: true,
            isPrivilegedProcess: false);

        Assert.Equal("flatpak-spawn", startInfo.FileName);
        Assert.Equal(["--host", "tool", "first", "two words"], startInfo.ArgumentList);
    }

    [Fact]
    public void ProcessHelper_CreateHostProcessStartInfo_InsideFlatpak_WrapsElevationOnHost()
    {
        if (!OperatingSystem.IsLinux()) return;

        var startInfo = ProcessHelper.CreateHostProcessStartInfo(
            "tool",
            ["first", "two words"],
            requireElevation: true,
            isFlatpakSandbox: true,
            isPrivilegedProcess: false);

        Assert.Equal("flatpak-spawn", startInfo.FileName);
        Assert.Equal(["--host", "pkexec", "tool", "first", "two words"], startInfo.ArgumentList);
    }

    [Fact]
    public void ProcessHelper_CreateProcessStartInfo_UsesHostElevationMechanism()
    {
        var startInfo = ProcessHelper.CreateProcessStartInfo(
            "tool",
            ["first", "two words"],
            requireElevation: true,
            isPrivilegedProcess: false);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("tool", startInfo.FileName);
            Assert.Equal("runas", startInfo.Verb);
            Assert.Equal(["first", "two words"], startInfo.ArgumentList);
            Assert.True(startInfo.UseShellExecute);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("pkexec", startInfo.FileName);
            Assert.Equal(["tool", "first", "two words"], startInfo.ArgumentList);
            Assert.False(startInfo.UseShellExecute);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("osascript", startInfo.FileName);
            Assert.Equal("-e", startInfo.ArgumentList[0]);
            Assert.Equal(
                "do shell script \"'tool' 'first' 'two words'\" with administrator privileges",
                startInfo.ArgumentList[1]);
            Assert.False(startInfo.UseShellExecute);
        }
    }

    [Fact]
    public void ProcessHelper_CreateProcessStartInfo_DisablesElevationForPrivilegedProcess()
    {
        var startInfo = ProcessHelper.CreateProcessStartInfo(
            "tool",
            ["first", "two words"],
            requireElevation: true,
            isPrivilegedProcess: true);

        Assert.Equal("tool", startInfo.FileName);
        Assert.Equal(["first", "two words"], startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.Verb);
    }

    [Fact]
    public void ProcessHelper_ConfigureOutputCapture_HandlesHostElevationMechanism()
    {
        var startInfo = ProcessHelper.CreateProcessStartInfo(
            "tool",
            ["first"],
            requireElevation: true,
            isPrivilegedProcess: false);

        if (OperatingSystem.IsWindows())
        {
            Assert.Throws<InvalidOperationException>(() => ProcessHelper.ConfigureOutputCapture(startInfo));
            return;
        }

        ProcessHelper.ConfigureOutputCapture(startInfo);

        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void ProcessHelper_CreateProcessStartInfo_PreservesRawArgumentsWithHostElevationMechanism()
    {
        var startInfo = ProcessHelper.CreateProcessStartInfo(
            "tool",
            "--first \"two words\"",
            requireElevation: true,
            isPrivilegedProcess: false);

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("tool", startInfo.FileName);
            Assert.Equal("runas", startInfo.Verb);
            Assert.Equal("--first \"two words\"", startInfo.Arguments);
            Assert.True(startInfo.UseShellExecute);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("pkexec", startInfo.FileName);
            Assert.Equal("\"tool\" --first \"two words\"", startInfo.Arguments);
            Assert.False(startInfo.UseShellExecute);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("osascript", startInfo.FileName);
            Assert.Equal("-e", startInfo.ArgumentList[0]);
            Assert.Equal(
                "do shell script \"'tool' --first \\\"two words\\\"\" with administrator privileges",
                startInfo.ArgumentList[1]);
            Assert.False(startInfo.UseShellExecute);
        }
    }

    [Fact]
    public void Dispose_WhenManagedDisposeThrows_StillDisposesUnmanagedResources()
    {
        var disposable = new ThrowingManagedDisposeObject();

        Assert.Throws<InvalidOperationException>(disposable.Dispose);

        Assert.True(disposable.ManagedDisposeCalled);
        Assert.True(disposable.UnmanagedDisposeCalled);
    }

    [Fact]
    public void SafeFileStream_Dispose_WhenCommitOnDisposeTrue_ReplacesDestination()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        File.WriteAllText(filePath, "old");

        using (var stream = new SafeFileStream(filePath))
        {
            stream.Write("new"u8);
            Assert.True(File.Exists(stream.TemporaryPath));
        }

        Assert.Equal("new", File.ReadAllText(filePath));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public void SafeFileStream_Dispose_WhenCommitOnDisposeFalse_DeletesTemporaryFileAndPreservesDestination()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        File.WriteAllText(filePath, "old");

        using (var stream = new SafeFileStream(filePath, commitOnDispose: false))
        {
            stream.Write("new"u8);
            Assert.True(File.Exists(stream.TemporaryPath));
        }

        Assert.Equal("old", File.ReadAllText(filePath));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public async Task SafeFileStream_CommitAsync_ReplacesDestination()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, "settings.json");
        await File.WriteAllTextAsync(filePath, "old", TestContext.Current.CancellationToken);

        await using (var stream = new SafeFileStream(filePath, commitOnDispose: false))
        {
            await stream.WriteAsync("new"u8.ToArray(), TestContext.Current.CancellationToken);
            await stream.CommitAsync(TestContext.Current.CancellationToken);
            Assert.True(stream.IsCommitted);
        }

        Assert.Equal("new", await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directoryPath, "*.tmp.*"));
    }

    [Fact]
    public void TemporaryDirectory_Dispose_DeletesDirectoryRecursively()
    {
        string directoryPath;

        using (var directory = new TemporaryDirectory(CreateTempDirectory(), "stagekit"))
        {
            directoryPath = directory.DirectoryPath;
            Directory.CreateDirectory(Path.Combine(directoryPath, "child"));
            File.WriteAllText(Path.Combine(directoryPath, "child", "file.txt"), "value");

            Assert.True(directory.Exists);
        }

        Assert.False(Directory.Exists(directoryPath));
    }

    [Fact]
    public void TemporaryFile_Dispose_DeletesFileUnlessKept()
    {
        var directoryPath = CreateTempDirectory();
        string deletedPath;
        string keptPath;

        using (var temporaryFile = new TemporaryFile(directoryPath, "txt"))
        {
            deletedPath = temporaryFile.FilePath;
            using var stream = temporaryFile.Create();
            stream.Write("deleted"u8);
        }

        using (var temporaryFile = new TemporaryFile(directoryPath, ".txt"))
        {
            keptPath = temporaryFile.FilePath;
            using var stream = temporaryFile.Create();
            stream.Write("kept"u8);
            temporaryFile.Keep();
        }

        Assert.False(File.Exists(deletedPath));
        Assert.True(File.Exists(keptPath));
    }

    [Fact]
    public void ShellScriptFile_UsesPlatformPreambleAndWritesItOnDispose()
    {
        var directoryPath = CreateTempDirectory();
        var filePath = Path.Combine(directoryPath, $"preamble{ShellScriptFile.ScriptFileExtension}");
        var expected = (OperatingSystem.IsWindows() ? "@echo off" : "#!/usr/bin/env bash") + "\n";

        try
        {
            using (var script = new ShellScriptFile(filePath))
            {
                Assert.Equal(expected, script.GetScript());
                Assert.True(script.IsFlushPending);
            }

            Assert.Equal(expected, File.ReadAllText(filePath));
        }
        finally
        {
            if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ShellScriptFile_DefaultConstructor_DeletesTemporaryScriptOnDispose()
    {
        string filePath;

        using (var script = new ShellScriptFile())
        {
            filePath = script.FilePath;

            Assert.True(script.DeleteOnDispose);
            Assert.EndsWith(ShellScriptFile.ScriptFileExtension, filePath);

            script.Flush();
            Assert.True(script.Exists);
        }

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task ShellScriptFile_ExecuteAsync_CapturesOutputAndDeletesTemporaryFileOnDispose()
    {
        var rootPath = CreateTempDirectory();
        var directoryPath = Path.Combine(rootPath, "shell scripts");
        string filePath;

        try
        {
            using (var script = ShellScriptFile.CreateTemporary(directoryPath))
            {
                filePath = script.FilePath;
                script.Clear();
                script.WriteComment("ShellScriptFile test");
                script.WriteEnvironmentVariable("STAGEKIT_VALUE", "stagekit-shell");
                script.WriteIfMacOS("# macOS");
                script.WriteLineIfMacOS(string.Empty);
                script.WriteIfLinux("# Linux");
                script.WriteLineIfLinux(string.Empty);
                script.WriteLineIfWindows(
                    $"echo {ShellScriptFile.FormatVariable("STAGEKIT_VALUE")}:%~1");
                script.WriteLineIfUnix(
                    $"printf '%s:%s' {ShellScriptFile.FormatVariable("STAGEKIT_VALUE")} \"$1\"");

                Assert.True(script.DeleteOnDispose);
                Assert.True(script.IsFlushPending);
                Assert.False(script.Exists);

                var output = await script.ExecuteAsync(
                    ["argument with spaces"],
                    TestContext.Current.CancellationToken);

                Assert.True(output.Succeeded, output.StandardError);
                Assert.Equal("stagekit-shell:argument with spaces", output.StandardOutput.Trim());
                Assert.True(script.Exists);
                Assert.False(script.IsFlushPending);
                Assert.EndsWith(ShellScriptFile.ScriptFileExtension, filePath);
                Assert.Equal(
                    script.GetScript(),
                    await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken));
            }

            Assert.False(File.Exists(filePath));
            Assert.Throws<ArgumentException>(() => ShellScriptFile.FormatVariable("invalid-name"));
        }
        finally
        {
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void ShellScriptFile_Dispose_WritesPendingContentUnlessDeleted()
    {
        var rootPath = CreateTempDirectory();
        var keptPath = Path.Combine(rootPath, "kept", $"kept{ShellScriptFile.ScriptFileExtension}");
        var deletedPath = Path.Combine(rootPath, $"deleted{ShellScriptFile.ScriptFileExtension}");

        try
        {
            using (var script = new ShellScriptFile(keptPath))
            {
                Assert.False(script.DeleteOnDispose);

                // Exercise the inherited TextWriter surface.
                TextWriter writer = script;
                writer.Write("echo ");
                writer.Write(42);
                writer.WriteLine();
            }

            Assert.True(File.Exists(keptPath));
            Assert.Contains("echo 42", File.ReadAllText(keptPath), StringComparison.Ordinal);

            using (var script = new ShellScriptFile(deletedPath) { DeleteOnDispose = true })
            {
                script.WriteLine("echo deleted");
                script.Flush();

                Assert.True(script.Exists);
                Assert.False(script.IsFlushPending);
            }

            Assert.False(File.Exists(deletedPath));
        }
        finally
        {
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void ShellScriptFile_WriteLines_AppendsEveryLine()
    {
        using var script = ShellScriptFile.CreateTemporary(CreateTempDirectory());
        var preamble = script.GetScript();
        var newLine = "\n";

        script.WriteLines("one", "two");
        script.WriteLines(new List<string?> { "three" });
        script.WriteLinesIf(true, "four", "five");
        script.WriteLinesIf(false, "skipped", "skipped");
        script.WriteLinesIf(true, new List<string?> { "six" });
        script.WriteLines([]);
        script.WriteLine();

        Assert.Equal(
            $"{preamble}one{newLine}two{newLine}three{newLine}four{newLine}five{newLine}six{newLine}{newLine}",
            script.GetScript());
    }

    [Fact]
    public async Task ShellScriptFile_WriteLinesAsyncAndComments_AppendEveryLine()
    {
        using var script = ShellScriptFile.CreateTemporary(CreateTempDirectory());
        var newLine = "\n";
        var commentPrefix = OperatingSystem.IsWindows() ? "rem " : "# ";
        script.Clear();
        var preamble = script.GetScript();

        await script.WriteLinesAsync("one", "two");
        await script.WriteLinesAsync(new List<string?> { "three" });
        await script.WriteLineAsync();
        script.WriteComments("first", "second");
        script.WriteComments(new List<string?> { $"third{newLine}fourth" });

        Assert.Equal(
            $"{preamble}one{newLine}two{newLine}three{newLine}{newLine}" +
            $"{commentPrefix}first{newLine}{commentPrefix}second{newLine}" +
            $"{commentPrefix}third{newLine}{commentPrefix}fourth{newLine}",
            script.GetScript());
    }

    [Fact]
    public void ShellScriptFile_WriteLinesIfPlatform_AppendsEveryLineOnMatchingHost()
    {
        using var script = ShellScriptFile.CreateTemporary(CreateTempDirectory());
        var newLine = "\n";
        script.Clear();
        var preamble = script.GetScript();

        script.WriteLinesIfWindows("windows-one", "windows-two");
        script.WriteLinesIfMacOS("macos-one", "macos-two");
        script.WriteLinesIfLinux(new List<string?> { "linux-one", "linux-two" });
        script.WriteLinesIfUnix("unix-one", "unix-two");

        var expected = preamble;
        if (OperatingSystem.IsWindows()) expected += $"windows-one{newLine}windows-two{newLine}";
        if (OperatingSystem.IsMacOS()) expected += $"macos-one{newLine}macos-two{newLine}";
        if (OperatingSystem.IsLinux()) expected += $"linux-one{newLine}linux-two{newLine}";
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            expected += $"unix-one{newLine}unix-two{newLine}";

        Assert.Equal(expected, script.GetScript());
    }

    [Fact]
    public void ShellScriptFile_Dispose_RejectsFurtherWrites()
    {
        var directoryPath = CreateTempDirectory();
        var script = ShellScriptFile.CreateTemporary(directoryPath);
        script.Dispose();

        Assert.True(script.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => script.WriteLine("echo disposed"));
        Assert.Throws<ObjectDisposedException>(() => script.Execute());
    }

    [Fact]
    public void PathUtilities_IsSubPathOf_RequiresDirectoryBoundary()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", "root");
        var childPath = Path.Combine(rootPath, "child", "file.txt");
        var siblingWithPrefixPath = rootPath + "-other";

        Assert.True(PathUtilities.IsSubPathOf(childPath, rootPath));
        Assert.False(PathUtilities.IsSubPathOf(siblingWithPrefixPath, rootPath));
    }

    [Fact]
    public void PathUtilities_IsSamePath_ReturnsFalseWhenOnlyOnePathIsNull()
    {
        Assert.False(PathUtilities.IsSamePath("path", null));
        Assert.False(PathUtilities.IsSamePath(null, "path"));
        Assert.True(PathUtilities.IsSamePath(null, null));
    }

    [Fact]
    public void PathUtilities_NormalizeArchiveEntryName_NormalizesWindowsSeparatorsOnUnix()
    {
        Assert.Equal("folder/file.txt", PathUtilities.NormalizeArchiveEntryName(@"folder\file.txt"));
    }

    [Theory]
    [InlineData("asset.zip", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("folder/asset.zip", false)]
    [InlineData("folder\\asset.zip", false)]
    public void FileUtilities_IsPathLeafName_ReturnsExpected(string value, bool expected)
    {
        Assert.Equal(expected, FileUtilities.IsPathLeafName(value));
    }

    [Fact]
    public void QuoteBashAnsiCString_EscapesSpecialAndControlCharacters()
    {
        const string value = "a'b\\c\n\r\t\b\f\u0001\u007F";

        var result = value.QuoteBashAnsiCString();

        Assert.Equal("$'a\\'b\\\\c\\n\\r\\t\\b\\f\\x01\\x7F'", result);
    }

    [Fact]
    public void QuoteBashAnsiCString_RejectsNullCharacter()
    {
        Assert.Throws<ArgumentException>(() => "value\0suffix".QuoteBashAnsiCString());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("plain", "plain")]
    [InlineData("a\r\n^%!\"b", "a\\n^^%%^!^\"b")]
    public void EscapeWindowsBatchValue_EscapesSetValueMetacharacters(string value, string expected)
    {
        Assert.Equal(expected, value.EscapeWindowsBatchValue());
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("word", "word")]
    [InlineData("CamelCase", "Camel Case")]
    [InlineData("HTMLParser", "HTMLParser")]
    [InlineData("Item42", "Item 42")]
    [InlineData("Item42", "Item42", false)]
    [InlineData("Item4", "Item4")]
    [InlineData("A", "A")]
    [InlineData("aB", "a B")]
    public void InsertCharBetweenCamelCase_InsertsAtCaseAndDigitTransitions(string value, string expected,
        bool splitNumbers = true)
    {
        Assert.Equal(expected, value.InsertCharBetweenCamelCase(splitNumbers: splitNumbers));
    }

    [Fact]
    public void PathUtilities_IsSubPathOf_RejectsReparsePointEscape()
    {
        var rootPath = CreateTempDirectory();
        var outsidePath = CreateTempDirectory();
        var linkPath = Path.Combine(rootPath, "link");

        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, outsidePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                                  PlatformNotSupportedException)
            {
                Assert.Skip($"Symbolic links are unavailable in this environment: {exception.Message}");
            }

            Assert.False(PathUtilities.IsSubPathOf(Path.Combine(linkPath, "file.txt"), rootPath));
        }
        finally
        {
            if (Directory.Exists(rootPath)) Directory.Delete(rootPath, recursive: true);
            if (Directory.Exists(outsidePath)) Directory.Delete(outsidePath, recursive: true);
        }
    }

    [Fact]
    public void DisposableObject_DoesNotDeclareFinalizer()
    {
        Assert.Null(typeof(DisposableObject).GetMethod(
            "Finalize",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void UnmanagedMemoryManager_GetSpan_ReflectsUnderlyingMemory()
    {
        var pointer = Marshal.AllocHGlobal(sizeof(int) * 3);

        try
        {
            Marshal.WriteInt32(pointer, 0, 10);
            Marshal.WriteInt32(pointer, sizeof(int), 20);
            Marshal.WriteInt32(pointer, sizeof(int) * 2, 30);

            using var manager = new UnmanagedMemoryManager<int>(pointer, 3);
            var span = manager.GetSpan();

            Assert.Equal([10, 20, 30], span.ToArray());

            span[1] = 42;

            Assert.Equal(42, Marshal.ReadInt32(pointer, sizeof(int)));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [Fact]
    public void UnmanagedMemoryManager_AllowsNullPointerForEmptyBlock()
    {
        using var manager = new UnmanagedMemoryManager<byte>(nint.Zero, 0);

        Assert.True(manager.GetSpan().IsEmpty);

        using var handle = manager.Pin();
    }

    [Fact]
    public void UnmanagedMemoryManager_Constructor_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnmanagedMemoryManager<byte>(nint.Zero, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UnmanagedMemoryManager<byte>(1, -1));
    }

    [Fact]
    public void UnmanagedMemoryManager_Pin_ValidatesElementIndex()
    {
        var pointer = Marshal.AllocHGlobal(1);

        try
        {
            using var manager = new UnmanagedMemoryManager<byte>(pointer, 1);

            using var endHandle = manager.Pin(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => manager.Pin(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => manager.Pin(2));
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static string CreateTempDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "StageKit.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private sealed class ThrowingManagedDisposeObject : DisposableObject
    {
        public bool ManagedDisposeCalled { get; private set; }

        public bool UnmanagedDisposeCalled { get; private set; }

        protected override void DisposeManaged()
        {
            ManagedDisposeCalled = true;
            throw new InvalidOperationException();
        }

        protected override void DisposeUnmanaged()
        {
            UnmanagedDisposeCalled = true;
        }
    }
}
