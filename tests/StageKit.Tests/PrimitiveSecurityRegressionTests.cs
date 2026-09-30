using System.Diagnostics;
using StageKit.Primitives;
using StageKit.Primitives.Extensions;
using StageKit.Primitives.System;
using StageKit.Runtime;

namespace StageKit.Tests;

public sealed class PrimitiveSecurityRegressionTests
{
    [Theory]
    [InlineData("\" & echo INJECTED & rem \"")]
    [InlineData("a!b")]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    [InlineData("a\0b")]
    public void EscapeWindowsBatchValue_RejectsUnrepresentableValues(string value)
    {
        Assert.Throws<ArgumentException>(() => value.EscapeWindowsBatchValue());
    }

    [Theory]
    [InlineData("EnableDelayedExpansion")]
    [InlineData("DisableDelayedExpansion")]
    public async Task EscapeWindowsBatchValue_RoundTripsInBatchFile(string expansionMode)
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateTempDirectory();
        try
        {
            const string value = "a%b^c&d|e<f>g";
            var script = Path.Combine(directory, "round trip.cmd");
            await File.WriteAllTextAsync(script,
                $"@echo off\r\nsetlocal {expansionMode}\r\nset \"VALUE={value.EscapeWindowsBatchValue()}\"\r\nset VALUE\r\n",
                TestContext.Current.CancellationToken);
            var output = await ProcessHelper.GetProcessOutputAsync(
                new ProcessStartInfo("cmd.exe") { ArgumentList = { "/d", "/c", script } },
                TestContext.Current.CancellationToken);

            Assert.Equal(0, output.ExitCode);
            Assert.Equal($"VALUE={value}", output.StandardOutput.Trim());
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public async Task ShellScriptArgumentList_QuotesMetacharactersAndRejectsQuotes()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Throws<ArgumentException>(() => ProcessHelper.CreateShellScriptProcessStartInfo(
            "test.cmd", ["safe\" & echo INJECTED"]));
        Assert.Throws<ArgumentException>(() => ProcessHelper.CreateShellScriptProcessStartInfo(
            "script%name.cmd", ["safe"]));
        var rawArguments = "\"C:\\My Dir\\file\"";
        var rawStartInfo = ProcessHelper.CreateShellScriptProcessStartInfo("test.cmd", rawArguments);
        Assert.Contains(rawArguments, rawStartInfo.Arguments);

        var directory = CreateTempDirectory();
        try
        {
            var script = Path.Combine(directory, "argument test.cmd");
            await File.WriteAllTextAsync(script, "@echo off\r\nset \"VALUE=%~1\"\r\nset VALUE\r\n",
                TestContext.Current.CancellationToken);
            const string value = "a!b^c& echo INJECTED | more < nul > nul";
            var output = await ProcessHelper.GetProcessOutputAsync(
                ProcessHelper.CreateShellScriptProcessStartInfo(script, [value]),
                TestContext.Current.CancellationToken);

            Assert.Equal(0, output.ExitCode);
            Assert.Equal($"VALUE={value}", output.StandardOutput.Trim());
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public void LinuxTerminalFallback_PassesBashArgumentsSeparately()
    {
        if (!OperatingSystem.IsLinux()) return;

        const string command = "echo 'two words'";
        var startInfo = HostSystem.CreateOpenInTerminalStartInfo(command, keepOpen: false);
        if (startInfo is null) return;

        var terminal = Path.GetFileName(startInfo.FileName);
        if (terminal is "gnome-terminal" or "xfce4-terminal" or "konsole") return;
        Assert.Equal(["-e", "bash", "-c", command], startInfo.ArgumentList);
    }

    [Fact]
    public void IsSamePath_IgnoresEndingSeparatorAndPreservesRoot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "stagekit-path-test");
        Assert.True(PathUtilities.IsSamePath(directory, directory + Path.DirectorySeparatorChar));
        var root = Path.GetPathRoot(Path.GetFullPath(directory))!;
        Assert.True(PathUtilities.IsSamePath(root, root + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void DiskStatus_FindsExistingDirectoryForFileAndMissingChild()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "file.txt");
            File.WriteAllText(file, "test");
            Assert.True(HostSystem.TryGetDiskStatus(directory, out var parent));
            Assert.True(HostSystem.TryGetDiskStatus(file, out var fileStatus));
            Assert.True(HostSystem.TryGetDiskStatus(Path.Combine(directory, "missing", "child"), out var missing));
            Assert.Equal(parent.Value.TotalSizeBytes, fileStatus.Value.TotalSizeBytes);
            Assert.Equal(parent.Value.TotalSizeBytes, missing.Value.TotalSizeBytes);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public async Task PreventSleep_OverlappingTokensCanBeDisposedOnOtherThreads()
    {
        if (!OperatingSystem.IsWindows()) return;

        var system = HostSystem.PreventSleep();
        var display = HostSystem.PreventSleep(keepDisplayOn: true);
        await Task.Run(system.Dispose, TestContext.Current.CancellationToken);
        await Task.Run(display.Dispose, TestContext.Current.CancellationToken);
        using var next = HostSystem.PreventSleep();
    }

    [Fact]
    public void BoundedProcessOutput_DrainsBothStreams()
    {
        if (!OperatingSystem.IsWindows()) return;

        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("[Console]::Error.Write('e' * 131072); [Console]::Out.Write('ok')");
        var result = Utilities.GetBoundedProcessOutput(startInfo);
        Assert.NotNull(result);
        Assert.Equal(0, result.Value.ExitCode);
        Assert.Equal("ok", result.Value.Output);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "stagekit-regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTempDirectory(string directory)
    {
        var fullPath = Path.GetFullPath(directory);
        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        Assert.True(fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && fullPath != tempRoot);
        Directory.Delete(fullPath, recursive: true);
    }
}
