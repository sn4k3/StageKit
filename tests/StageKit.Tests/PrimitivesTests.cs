using StageKit.Primitives;
using StageKit.Primitives.Extensions;
using StageKit.Primitives.System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace StageKit.Tests;

public sealed class PrimitivesTests
{
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
    public void ProcessHelper_CreateShellScriptProcessStartInfo_PassesScriptPathAsDiscreteArgument()
    {
        const string scriptFilePath = "/tmp/a directory with spaces/upgrade script.sh";

        var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(scriptFilePath);

        Assert.Equal(OperatingSystem.IsWindows() ? "cmd.exe" : "bash", startInfo.FileName);

        // Unix drops "-c" so the shell never word-splits the path back apart.
        Assert.Equal(
            OperatingSystem.IsWindows() ? ["/d", "/c", scriptFilePath] : [scriptFilePath],
            startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.Verb);
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
                    ? "@echo off\r\necho stagekit script\r\n"
                    : "#!/usr/bin/env bash\necho \"stagekit script\"\n",
                TestContext.Current.CancellationToken);

            var startInfo = ProcessHelper.CreateShellScriptProcessStartInfo(scriptFilePath);
            var output = await ProcessHelper.GetProcessOutputAsync(
                startInfo,
                TestContext.Current.CancellationToken);

            Assert.Equal(0, output.ExitCode);
            Assert.Equal("stagekit script", output.StandardOutput.Trim());
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
