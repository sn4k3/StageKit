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
            script.IndexOf("copy /Y \"%SOURCE_FILEPATH%\" \"%STAGED_FILEPATH%\"", StringComparison.Ordinal)
            < script.IndexOf("move /Y \"%CURRENT_FILEPATH%\" \"%BACKUP_FILEPATH%\"", StringComparison.Ordinal));
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
            "--updated true");

        Assert.Equal("/usr/bin/nohup", startInfo.FileName);
        Assert.Equal("/bin/bash", startInfo.ArgumentList[0]);
        Assert.Equal("-c", startInfo.ArgumentList[1]);
        Assert.Equal("stagekit-updatum-relaunch", startInfo.ArgumentList[3]);
        Assert.Equal("1234", startInfo.ArgumentList[4]);
        Assert.Equal("/Applications/Test App.app", startInfo.ArgumentList[5]);
        Assert.Equal("--updated true", startInfo.ArgumentList[6]);

        var script = startInfo.ArgumentList[2];
        var waitIndex = script.IndexOf("/bin/kill -0 \"$1\"", StringComparison.Ordinal);
        var openIndex = script.IndexOf("/usr/bin/open \"$2\"", StringComparison.Ordinal);
        Assert.True(waitIndex >= 0);
        Assert.True(openIndex > waitIndex);
        Assert.Contains("--args $3", script, StringComparison.Ordinal);
    }
}
