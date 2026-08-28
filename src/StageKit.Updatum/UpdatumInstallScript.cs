namespace StageKit.Updatum;

internal static class UpdatumInstallScript
{
    public static void WriteWindowsFileReplacement(TextWriter writer)
    {
        writer.WriteLine("echo - Staging replacement file");
        writer.WriteLine("if exist \"%STAGED_FILEPATH%\" exit /b 1");
        writer.WriteLine("if exist \"%BACKUP_FILEPATH%\" exit /b 1");
        writer.WriteLine("copy /Y \"%SOURCE_FILEPATH%\" \"%STAGED_FILEPATH%\" >nul");
        writer.WriteLine("if errorlevel 1 exit /b 1");
        writer.WriteLine("if not exist \"%STAGED_FILEPATH%\" exit /b 1");
        writer.WriteLine();
        writer.WriteLine("if not \"%CURRENT_FILEPATH%\"==\"%TARGET_FILEPATH%\" if exist \"%TARGET_FILEPATH%\" (");
        writer.WriteLine("  echo - Error: Target file already exists");
        writer.WriteLine("  call :DeleteIfSafe \"%STAGED_FILEPATH%\"");
        writer.WriteLine("  exit /b 1");
        writer.WriteLine(")");
        writer.WriteLine("if exist \"%CURRENT_FILEPATH%\" (");
        writer.WriteLine("  move /Y \"%CURRENT_FILEPATH%\" \"%BACKUP_FILEPATH%\" >nul");
        writer.WriteLine("  if errorlevel 1 (");
        writer.WriteLine("    call :DeleteIfSafe \"%STAGED_FILEPATH%\"");
        writer.WriteLine("    exit /b 1");
        writer.WriteLine("  )");
        writer.WriteLine(")");
        writer.WriteLine("move /Y \"%STAGED_FILEPATH%\" \"%TARGET_FILEPATH%\" >nul");
        writer.WriteLine("if errorlevel 1 goto RestoreFileBackup");
        writer.WriteLine("call :DeleteIfSafe \"%BACKUP_FILEPATH%\"");
        writer.WriteLine("goto FileReplacementComplete");
        writer.WriteLine();
        writer.WriteLine(":RestoreFileBackup");
        writer.WriteLine("echo - Replacement failed, restoring previous executable");
        writer.WriteLine("call :DeleteIfSafe \"%TARGET_FILEPATH%\"");
        writer.WriteLine("if exist \"%BACKUP_FILEPATH%\" move /Y \"%BACKUP_FILEPATH%\" \"%CURRENT_FILEPATH%\" >nul");
        writer.WriteLine("call :DeleteIfSafe \"%STAGED_FILEPATH%\"");
        writer.WriteLine("exit /b 1");
        writer.WriteLine();
        writer.WriteLine(":FileReplacementComplete");
    }

    public static void WriteUnixFileReplacement(TextWriter writer)
    {
        writer.WriteLine("restore_file_backup() {");
        writer.WriteLine("  echo \"- Replacement failed, restoring previous executable\"");
        writer.WriteLine("  if [[ -f \"$BACKUP_FILEPATH\" ]]; then mv -f -- \"$BACKUP_FILEPATH\" \"$CURRENT_FILEPATH\"; fi");
        writer.WriteLine("  rm -f -- \"$STAGED_FILEPATH\"");
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("echo \"- Staging replacement file\"");
        writer.WriteLine("[[ ! -e \"$STAGED_FILEPATH\" && ! -e \"$BACKUP_FILEPATH\" ]] || exit 1");
        writer.WriteLine("cp -f -- \"$SOURCE_FILEPATH\" \"$STAGED_FILEPATH\" || exit 1");
        writer.WriteLine("[[ -f \"$STAGED_FILEPATH\" ]] || exit 1");
        writer.WriteLine("chmod +x \"$STAGED_FILEPATH\" || exit 1");
        writer.WriteLine();
        writer.WriteLine("if [[ \"$CURRENT_FILEPATH\" != \"$TARGET_FILEPATH\" && -e \"$TARGET_FILEPATH\" ]]; then");
        writer.WriteLine("  echo \"- Error: Target file already exists\"");
        writer.WriteLine("  rm -f -- \"$STAGED_FILEPATH\"");
        writer.WriteLine("  exit 1");
        writer.WriteLine("fi");
        writer.WriteLine("if [[ -f \"$CURRENT_FILEPATH\" ]]; then");
        writer.WriteLine("  mv -f -- \"$CURRENT_FILEPATH\" \"$BACKUP_FILEPATH\" || { rm -f -- \"$STAGED_FILEPATH\"; exit 1; }");
        writer.WriteLine("fi");
        writer.WriteLine("if ! mv -f -- \"$STAGED_FILEPATH\" \"$TARGET_FILEPATH\"; then");
        writer.WriteLine("  rm -f -- \"$TARGET_FILEPATH\"");
        writer.WriteLine("  restore_file_backup");
        writer.WriteLine("  exit 1");
        writer.WriteLine("fi");
        writer.WriteLine("rm -f -- \"$BACKUP_FILEPATH\"");
    }

    public static void WriteWindowsDirectoryReplacement(TextWriter writer)
    {
        writer.WriteLine("echo - Staging application directory");
        writer.WriteLine("if exist \"%STAGED_PATH%\" exit /b 1");
        writer.WriteLine("if exist \"%BACKUP_PATH%\" exit /b 1");
        writer.WriteLine("mkdir \"%STAGED_PATH%\"");
        writer.WriteLine("if errorlevel 1 exit /b 1");
        writer.WriteLine("if exist \"%DEST_PATH%\" (");
        writer.WriteLine("  robocopy \"%DEST_PATH%\" \"%STAGED_PATH%\" /E /COPY:DAT /R:3 /W:1 >nul");
        writer.WriteLine("  if errorlevel 8 goto DirectoryPreparationFailed");
        writer.WriteLine(")");
        writer.WriteLine("robocopy \"%SOURCE_PATH%\" \"%STAGED_PATH%\" /E /COPY:DAT /R:3 /W:1 >nul");
        writer.WriteLine("if errorlevel 8 goto DirectoryPreparationFailed");
        writer.WriteLine("if not exist \"%STAGED_PATH%\" goto DirectoryPreparationFailed");
        writer.WriteLine("if exist \"%DEST_PATH%\" (");
        writer.WriteLine("  move /Y \"%DEST_PATH%\" \"%BACKUP_PATH%\" >nul");
        writer.WriteLine("  if errorlevel 1 goto DirectoryPreparationFailed");
        writer.WriteLine(")");
        writer.WriteLine("move /Y \"%STAGED_PATH%\" \"%DEST_PATH%\" >nul");
        writer.WriteLine("if errorlevel 1 goto RestoreDirectoryBackup");
        writer.WriteLine("if exist \"%BACKUP_PATH%\" rmdir /S /Q \"%BACKUP_PATH%\"");
        writer.WriteLine("goto DirectoryReplacementComplete");
        writer.WriteLine();
        writer.WriteLine(":DirectoryPreparationFailed");
        writer.WriteLine("if exist \"%STAGED_PATH%\" rmdir /S /Q \"%STAGED_PATH%\"");
        writer.WriteLine("exit /b 1");
        writer.WriteLine();
        writer.WriteLine(":RestoreDirectoryBackup");
        writer.WriteLine("echo - Replacement failed, restoring previous application directory");
        writer.WriteLine("if exist \"%DEST_PATH%\" rmdir /S /Q \"%DEST_PATH%\"");
        writer.WriteLine("if exist \"%BACKUP_PATH%\" move /Y \"%BACKUP_PATH%\" \"%DEST_PATH%\" >nul");
        writer.WriteLine("if exist \"%STAGED_PATH%\" rmdir /S /Q \"%STAGED_PATH%\"");
        writer.WriteLine("exit /b 1");
        writer.WriteLine();
        writer.WriteLine(":DirectoryReplacementComplete");
    }

    public static void WriteUnixDirectoryReplacement(TextWriter writer)
    {
        writer.WriteLine("restore_directory_backup() {");
        writer.WriteLine("  echo \"- Replacement failed, restoring previous application directory\"");
        writer.WriteLine("  if [[ -d \"$BACKUP_PATH\" ]]; then mv -f -- \"$BACKUP_PATH\" \"$DEST_PATH\"; fi");
        writer.WriteLine("  rm -rf -- \"$STAGED_PATH\"");
        writer.WriteLine("}");
        writer.WriteLine();
        writer.WriteLine("echo \"- Staging application directory\"");
        writer.WriteLine("[[ ! -e \"$STAGED_PATH\" && ! -e \"$BACKUP_PATH\" ]] || exit 1");
        writer.WriteLine("mkdir \"$STAGED_PATH\" || exit 1");
        writer.WriteLine("if [[ -d \"$DEST_PATH\" ]]; then cp -a \"${DEST_PATH}/.\" \"${STAGED_PATH}/\" || { rm -rf -- \"$STAGED_PATH\"; exit 1; }; fi");
        writer.WriteLine("cp -a \"${SOURCE_PATH}/.\" \"${STAGED_PATH}/\" || { rm -rf -- \"$STAGED_PATH\"; exit 1; }");
        writer.WriteLine("if [[ -d \"$DEST_PATH\" ]]; then");
        writer.WriteLine("  mv -f -- \"$DEST_PATH\" \"$BACKUP_PATH\" || { rm -rf -- \"$STAGED_PATH\"; exit 1; }");
        writer.WriteLine("fi");
        writer.WriteLine("if ! mv -f -- \"$STAGED_PATH\" \"$DEST_PATH\"; then");
        writer.WriteLine("  rm -rf -- \"$DEST_PATH\"");
        writer.WriteLine("  restore_directory_backup");
        writer.WriteLine("  exit 1");
        writer.WriteLine("fi");
        writer.WriteLine("rm -rf -- \"$BACKUP_PATH\"");
    }
}
