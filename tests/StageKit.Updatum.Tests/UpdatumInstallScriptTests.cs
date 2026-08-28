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
}
