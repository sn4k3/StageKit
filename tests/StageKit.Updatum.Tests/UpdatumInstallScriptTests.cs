using System.Diagnostics;
using System.Text;

namespace StageKit.Updatum.Tests;

public sealed class UpdatumInstallScriptTests
{
    [Fact]
    public void WriteWindowsFileReplacement_StagesBeforeBackup_AndIncludesRollback()
    {
        using var writer = new StringWriter();

        UpdatumInstallScript.WriteWindowsFileReplacement(writer);
        var script = writer.ToString();

        Assert.True(
            script.IndexOf("copy /Y \"!SOURCE_FILEPATH!\" \"!STAGED_FILEPATH!\"", StringComparison.Ordinal)
            < script.IndexOf("move /Y \"!CURRENT_FILEPATH!\" \"!BACKUP_FILEPATH!\"", StringComparison.Ordinal));
        Assert.Contains(":RestoreFileBackup", script);
        Assert.Contains("if errorlevel 1", script);
    }

    [Fact]
    public void WriteUnixFileReplacement_StagesBeforeBackup_AndIncludesRollback()
    {
        using var writer = new StringWriter();

        UpdatumInstallScript.WriteUnixFileReplacement(writer);
        var script = writer.ToString();

        Assert.True(
            script.IndexOf("cp -f -- \"$SOURCE_FILEPATH\" \"$STAGED_FILEPATH\"", StringComparison.Ordinal)
            < script.IndexOf("mv -f -- \"$CURRENT_FILEPATH\" \"$BACKUP_FILEPATH\"", StringComparison.Ordinal));
        Assert.Contains("restore_file_backup", script);
        Assert.Contains("exit 1", script);
    }

    [Fact]
    public void PortableReplacementScripts_StageAndRollbackDirectories()
    {
        using var windowsWriter = new StringWriter();
        using var unixWriter = new StringWriter();

        UpdatumInstallScript.WriteWindowsDirectoryReplacement(windowsWriter);
        UpdatumInstallScript.WriteUnixDirectoryReplacement(unixWriter);

        Assert.Contains("STAGED_PATH", windowsWriter.ToString());
        Assert.Contains(":RestoreDirectoryBackup", windowsWriter.ToString());
        Assert.Contains("STAGED_PATH", unixWriter.ToString());
        Assert.Contains("restore_directory_backup", unixWriter.ToString());
    }

    [Fact]
    public void WriteMacOSPkgInstallation_UsesNativeInstaller()
    {
        using var writer = new StringWriter();

        UpdatumInstallScript.WriteMacOSPkgInstallation(writer);
        var script = writer.ToString();

        Assert.Contains("[[ ! -f \"$FILEPATH\" ]]", script);
        Assert.Contains("/usr/sbin/installer -pkg \"$FILEPATH\" -target /", script);
    }

    [Fact]
    public void WriteMacOSDmgPreparation_MountsReadOnlyAndAlwaysDetaches()
    {
        using var writer = new StringWriter();

        UpdatumInstallScript.WriteMacOSDmgPreparation(writer);
        var script = writer.ToString();

        Assert.Contains("trap cleanup_macos_dmg EXIT", script);
        Assert.Contains("hdiutil attach \"$FILEPATH\" -mountpoint \"$MOUNT_POINT\" -nobrowse -readonly -quiet", script);
        Assert.Contains("hdiutil detach \"$MOUNT_POINT\" -quiet", script);
    }

    [Fact]
    public void WriteMacOSDmgPreparation_ResolvesTheReplacementTarget()
    {
        using var writer = new StringWriter();

        UpdatumInstallScript.WriteMacOSDmgPreparation(writer);
        var script = writer.ToString();

        Assert.Contains("DEST_PATH=\"$CURRENT_APP_BUNDLE_PATH\"", script);
        Assert.Contains("DEST_PATH=\"/Applications/$(/usr/bin/basename \"$APP_PATH\")\"", script);
        Assert.Contains("App bundle replacement paths are not safe", script);
    }

    [Fact]
    public void WriteMacOSDmgPreparation_RequestsElevationOnlyWhenTheTargetNeedsIt()
    {
        using var writer = new StringWriter();

        UpdatumInstallScript.WriteMacOSDmgPreparation(writer);
        var script = writer.ToString();

        var elevationExit = $"exit {UpdatumInstallScript.PrivilegeEscalationRequiredExitCode}";

        // A PKG always installs into the system domain.
        Assert.Contains($"if [[ $EUID -ne 0 ]]; then {elevationExit}; fi", script);

        // An app bundle only when its parent directory denies the current user.
        Assert.Contains($"if [[ ! -w \"$DEST_PARENT\" && $EUID -ne 0 ]]; then {elevationExit}; fi", script);

        // The probe must precede anything destructive so an elevated retry never repeats side effects.
        Assert.DoesNotContain("/usr/bin/ditto", script);
        Assert.DoesNotContain("/usr/sbin/installer", script);
    }

    [Fact]
    public void WriteMacOSDmgInstallation_InstallsPkgOrAtomicallyReplacesAppBundle()
    {
        using var writer = new StringWriter();

        UpdatumInstallScript.WriteMacOSDmgInstallation(writer);
        var script = writer.ToString();

        Assert.Contains("/usr/sbin/installer -pkg \"$PKG_PATH\" -target /", script);
        Assert.Contains("/usr/bin/ditto \"$APP_PATH\" \"$STAGED_PATH\"", script);
        Assert.True(
            script.IndexOf("\"$DEST_PATH\" \"$BACKUP_PATH\"", StringComparison.Ordinal)
            < script.IndexOf("\"$STAGED_PATH\" \"$DEST_PATH\"", StringComparison.Ordinal));
        Assert.Contains("\"$BACKUP_PATH\" \"$DEST_PATH\"", script);
    }

    [Fact]
    public void CreateMacOSRelaunchProcessStartInfo_WaitsForCurrentProcessThenOpensInstalledBundle()
    {
        var startInfo = UpdatumInstallScript.CreateMacOSRelaunchProcessStartInfo(
            1234,
            "/Applications/Test App.app",
            "--updated \"a b\" true");

        Assert.Equal("/usr/bin/nohup", startInfo.FileName);
        Assert.Equal("/bin/bash", startInfo.ArgumentList[0]);
        Assert.Equal("-c", startInfo.ArgumentList[1]);
        Assert.Equal("stagekit-updatum-relaunch", startInfo.ArgumentList[3]);
        Assert.Equal("1234", startInfo.ArgumentList[4]);
        Assert.Equal("/Applications/Test App.app", startInfo.ArgumentList[5]);
        Assert.EndsWith("StageKit.Updatum.Relaunch-1234.log", startInfo.ArgumentList[6],
            StringComparison.Ordinal);
        Assert.Equal("--updated", startInfo.ArgumentList[7]);
        Assert.Equal("a b", startInfo.ArgumentList[8]);
        Assert.Equal("true", startInfo.ArgumentList[9]);

        var script = startInfo.ArgumentList[2];
        var waitIndex = script.IndexOf("/bin/kill -0 \"$1\"", StringComparison.Ordinal);
        var openIndex = script.IndexOf("/usr/bin/open -n -W \"$2\"", StringComparison.Ordinal);
        Assert.True(waitIndex >= 0);
        Assert.True(openIndex > waitIndex);
        Assert.Contains("exec </dev/null >\"$4\" 2>&1", script, StringComparison.Ordinal);
        Assert.Contains("if [[ ! -d \"$2\" ]]", script, StringComparison.Ordinal);
        Assert.Contains("RUN_ARGUMENTS=(\"${@:5}\")", script, StringComparison.Ordinal);
        Assert.Contains("--args \"${RUN_ARGUMENTS[@]}\"", script, StringComparison.Ordinal);
        Assert.Contains("open_wait_pid=$!", script, StringComparison.Ordinal);
        Assert.Contains("open_exit_code=$?", script, StringComparison.Ordinal);
        Assert.Contains("exit \"$open_exit_code\"", script, StringComparison.Ordinal);
    }
    [Fact]
    public async Task BashUpdaterMessage_QuotesHostileTag()
    {
        var directory = Directory.CreateTempSubdirectory("StageKit.Updatum.Bash-").FullName;
        try
        {
            var markerPath = Path.Combine(directory, "injected");
            var tag = $"v2'$(touch \"{markerPath}\") & !";
            using var writer = new StringWriter();
            UpdatumInstallScript.WriteBashUpdaterMessage(writer, "App", new Version(1, 0), tag);
            var script = writer.ToString();
            Assert.StartsWith("echo $'", script, StringComparison.Ordinal);
            Assert.Contains("\\'", script, StringComparison.Ordinal);

            if (OperatingSystem.IsWindows()) return;
            var startInfo = new ProcessStartInfo("bash")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(script);
            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, process.ExitCode);
            Assert.Contains(tag, output, StringComparison.Ordinal);
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    [Fact]
    public async Task WindowsValueLoader_RoundTripsMoreThanFortyValuesWithoutNestingOverflow()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = Directory.CreateTempSubdirectory("StageKit.Updatum.Batch-").FullName;
        try
        {
            var scriptPath = Path.Combine(directory, "load.cmd");
            var markerPath = Path.Combine(directory, "injected.txt");
            var outputPath = Path.Combine(directory, "result.txt");
            var values = Enumerable.Range(0, 45)
                .Select(index => new KeyValuePair<string, string?>($"VALUE_{index}", $"text-{index}"))
                .ToArray();
            values[0] = new KeyValuePair<string, string?>("VALUE_0",
                $"! \" & %PATH% ^ ã & echo INJECTED > \"{markerPath}\"");
            values[1] = new KeyValuePair<string, string?>("VALUE_1", string.Empty);
            values[31] = new KeyValuePair<string, string?>("VALUE_31", "after-limit ! & \" %PATH% ^ ã");
            values[41] = new KeyValuePair<string, string?>("VALUE_41", string.Empty);
            values[44] = new KeyValuePair<string, string?>("VALUE_44", "last ! & \" %PATH% ^ ã");

            using var writer = new StringWriter();
            writer.WriteLine("@echo off");
            writer.WriteLine("setlocal disabledelayedexpansion");
            writer.WriteLine("chcp 65001 >nul");
            UpdatumInstallScript.WriteWindowsValues(writer, scriptPath, values);
            writer.WriteLine("call :ProbeScope");
            for (var index = 0; index < values.Length; index++)
                writer.WriteLine($"{(index == 0 ? ">" : ">>")} \"%~dp0result.txt\" echo(!VALUE_{index}!");
            writer.WriteLine("exit /b 0");
            writer.WriteLine(":ProbeScope");
            writer.WriteLine("setlocal enabledelayedexpansion");
            writer.WriteLine("if not defined VALUE_44 exit /b 3");
            writer.WriteLine("endlocal");
            writer.WriteLine("exit /b 0");
            File.WriteAllText(scriptPath, writer.ToString(), new UTF8Encoding(false));

            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(scriptPath);
            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);

            var diagnostics = await standardOutput + await standardError;
            Assert.Equal(0, process.ExitCode);
            Assert.DoesNotContain("Maximum setlocal recursion", diagnostics, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(values.Select(pair => pair.Value ?? string.Empty), File.ReadAllLines(outputPath));
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    [Fact]
    public async Task WindowsInstallerExecution_FailedChildLeavesArtifactsAndSkipsRelaunch()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = Directory.CreateTempSubdirectory("StageKit.Updatum.Installer-").FullName;
        try
        {
            var scriptPath = Path.Combine(directory, "install.cmd");
            var markerPath = Path.Combine(directory, "relaunched.txt");
            var artifactPath = Path.Combine(directory, "artifact.bin");
            File.WriteAllText(artifactPath, "diagnostic");
            var sidecarPath = scriptPath + ".data-SECRET";
            File.WriteAllText(sidecarPath, "sensitive");
            using var writer = new StringWriter();
            writer.WriteLine("@echo off");
            writer.WriteLine("setlocal enabledelayedexpansion");
            writer.WriteLine("set \"FILEPATH=%ComSpec%\"");
            writer.WriteLine("set \"INSTALLER_ARGUMENTS=/d /c exit 7\"");
            UpdatumInstallScript.WriteWindowsInstallerExecution(writer);
            writer.WriteLine("> \"%~dp0relaunched.txt\" echo relaunched");
            writer.WriteLine("exit /b 0");
            writer.WriteLine(":ScriptFailed");
            writer.WriteLine("del /F /Q \"%~f0.data-*\" >nul 2>&1");
            writer.WriteLine("exit /b 1");
            File.WriteAllText(scriptPath, writer.ToString(), new UTF8Encoding(false));

            var startInfo = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(scriptPath);
            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);

            Assert.NotEqual(0, process.ExitCode);
            Assert.False(File.Exists(markerPath));
            Assert.True(File.Exists(artifactPath));
            Assert.False(File.Exists(sidecarPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    [Fact]
    public void WindowsInstallerExecution_StopsOnFailure()
    {
        using var writer = new StringWriter();
        UpdatumInstallScript.WriteWindowsInstallerExecution(writer);
        var script = writer.ToString();

        Assert.Contains("start \"\" /WAIT \"!FILEPATH!\" !INSTALLER_ARGUMENTS!", script);
        Assert.Contains("if not \"!errorlevel!\"==\"0\" goto :ScriptFailed", script);
    }
}
