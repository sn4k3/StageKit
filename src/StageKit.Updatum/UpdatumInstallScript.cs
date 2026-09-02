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

    /// <summary>
    /// The exit code a generated macOS script uses to report that it must be re-run with administrator privileges.
    /// </summary>
    /// <remarks>Matches the <c>EX_NOPERM</c> convention from <c>sysexits.h</c>.</remarks>
    public const int PrivilegeEscalationRequiredExitCode = 77;

    public static void WriteMacOSPkgInstallation(TextWriter writer)
    {
        writer.WriteLine("echo \"- Installing macOS package\"");
        writer.WriteLine("if [[ ! -f \"$FILEPATH\" ]]; then");
        writer.WriteLine("  echo \"- Error: Package does not exist: $FILEPATH\"");
        writer.WriteLine("  exit 1");
        writer.WriteLine("fi");
        writer.WriteLine("/usr/sbin/installer -pkg \"$FILEPATH\" -target /");
    }

    /// <summary>
    /// Writes the disk image mount and target resolution stage that precedes any destructive or author-supplied step.
    /// </summary>
    /// <param name="writer">The script writer.</param>
    /// <remarks>
    /// The stage exits with <see cref="PrivilegeEscalationRequiredExitCode"/> when the resolved work needs
    /// administrator privileges the current run does not have, so the caller can retry elevated without repeating
    /// instance termination or a custom script.
    /// </remarks>
    public static void WriteMacOSDmgPreparation(TextWriter writer)
    {
        writer.WriteLine("MOUNT_POINT=\"\"");
        writer.WriteLine("DMG_ATTACHED=False");
        writer.WriteLine("cleanup_macos_dmg() {");
        writer.WriteLine("  local exit_code=$?");
        writer.WriteLine("  trap - EXIT");
        writer.WriteLine("  if [[ \"$DMG_ATTACHED\" = \"True\" ]]; then");
        writer.WriteLine("    /usr/bin/hdiutil detach \"$MOUNT_POINT\" -quiet || true");
        writer.WriteLine("  fi");
        writer.WriteLine("  if [[ -n \"$MOUNT_POINT\" && \"$MOUNT_POINT\" != \"/\" ]]; then");
        writer.WriteLine("    /bin/rmdir \"$MOUNT_POINT\" 2>/dev/null || true");
        writer.WriteLine("  fi");
        writer.WriteLine("  exit \"$exit_code\"");
        writer.WriteLine("}");
        writer.WriteLine("trap cleanup_macos_dmg EXIT");
        writer.WriteLine();
        writer.WriteLine("if [[ ! -f \"$FILEPATH\" ]]; then");
        writer.WriteLine("  echo \"- Error: Disk image does not exist: $FILEPATH\"");
        writer.WriteLine("  exit 1");
        writer.WriteLine("fi");
        writer.WriteLine("MOUNT_POINT=$(/usr/bin/mktemp -d \"${TMPDIR:-/tmp}/StageKit.Updatum.Dmg.XXXXXX\") || exit 1");
        writer.WriteLine("echo \"- Mounting macOS disk image\"");
        writer.WriteLine("/usr/bin/hdiutil attach \"$FILEPATH\" -mountpoint \"$MOUNT_POINT\" -nobrowse -readonly -quiet || exit 1");
        writer.WriteLine("DMG_ATTACHED=True");
        writer.WriteLine();
        writer.WriteLine("PKG_PATH=\"\"");
        writer.WriteLine("while IFS= read -r -d '' candidate; do");
        writer.WriteLine("  PKG_PATH=\"$candidate\"");
        writer.WriteLine("  break");
        writer.WriteLine(
            "done < <(/usr/bin/find \"$MOUNT_POINT\" -type d -name \"*.app\" -prune -o -name \"*.pkg\" -print0)");
        writer.WriteLine("APP_PATH=\"\"");
        writer.WriteLine("if [[ -z \"$PKG_PATH\" ]]; then");
        writer.WriteLine("  while IFS= read -r -d '' candidate; do");
        writer.WriteLine("    APP_PATH=\"$candidate\"");
        writer.WriteLine("    break");
        writer.WriteLine(
            "  done < <(/usr/bin/find \"$MOUNT_POINT\" -type d -name \"*.app\" -prune -print0)");
        writer.WriteLine("  if [[ -z \"$APP_PATH\" ]]; then");
        writer.WriteLine("    echo \"- Error: Disk image contains neither a PKG installer nor an app bundle\"");
        writer.WriteLine("    exit 1");
        writer.WriteLine("  fi");
        writer.WriteLine("fi");
        writer.WriteLine();
        writer.WriteLine("if [[ -n \"$PKG_PATH\" ]]; then");
        writer.WriteLine("  # A PKG always installs into the system domain, which requires root.");
        writer.WriteLine($"  if [[ $EUID -ne 0 ]]; then exit {PrivilegeEscalationRequiredExitCode}; fi");
        writer.WriteLine("else");
        writer.WriteLine("  if [[ \"$CURRENT_APP_BUNDLE_PATH\" = *.app ]]; then");
        writer.WriteLine("    DEST_PATH=\"$CURRENT_APP_BUNDLE_PATH\"");
        writer.WriteLine("  else");
        writer.WriteLine("    DEST_PATH=\"/Applications/$(/usr/bin/basename \"$APP_PATH\")\"");
        writer.WriteLine("  fi");
        writer.WriteLine("  DEST_PARENT=$(/usr/bin/dirname \"$DEST_PATH\")");
        writer.WriteLine("  APP_NAME=$(/usr/bin/basename \"$DEST_PATH\")");
        writer.WriteLine("  STAGED_PATH=\"${DEST_PARENT}/.${APP_NAME}.updatum-new-$$\"");
        writer.WriteLine("  BACKUP_PATH=\"${DEST_PARENT}/.${APP_NAME}.updatum-backup-$$\"");
        writer.WriteLine("  if [[ ! -d \"$DEST_PARENT\" || -e \"$STAGED_PATH\" || -e \"$BACKUP_PATH\" ]]; then");
        writer.WriteLine("    echo \"- Error: App bundle replacement paths are not safe\"");
        writer.WriteLine("    exit 1");
        writer.WriteLine("  fi");
        writer.WriteLine("  # An app bundle only needs root when its parent directory denies the current user.");
        writer.WriteLine(
            $"  if [[ ! -w \"$DEST_PARENT\" && $EUID -ne 0 ]]; then exit {PrivilegeEscalationRequiredExitCode}; fi");
        writer.WriteLine("fi");
    }

    /// <summary>
    /// Writes the disk image installation stage, which requires <see cref="WriteMacOSDmgPreparation"/> to have run.
    /// </summary>
    /// <param name="writer">The script writer.</param>
    public static void WriteMacOSDmgInstallation(TextWriter writer)
    {
        writer.WriteLine("if [[ -n \"$PKG_PATH\" ]]; then");
        writer.WriteLine("  echo \"- Installing package from disk image\"");
        writer.WriteLine("  /usr/sbin/installer -pkg \"$PKG_PATH\" -target /");
        writer.WriteLine("  exit $?");
        writer.WriteLine("fi");
        writer.WriteLine();
        writer.WriteLine("echo \"- Staging app bundle at $DEST_PATH\"");
        writer.WriteLine("/usr/bin/ditto \"$APP_PATH\" \"$STAGED_PATH\" || { /bin/rm -rf -- \"$STAGED_PATH\"; exit 1; }");
        writer.WriteLine("/usr/bin/xattr -dr com.apple.quarantine \"$STAGED_PATH\" 2>/dev/null || true");
        writer.WriteLine("if [[ \"$MACOS_CODESIGN_APP\" = \"True\" ]]; then");
        writer.WriteLine("  /usr/bin/codesign --force --deep --sign - \"$STAGED_PATH\" || { /bin/rm -rf -- \"$STAGED_PATH\"; exit 1; }");
        writer.WriteLine("fi");
        writer.WriteLine();
        writer.WriteLine("if [[ -e \"$DEST_PATH\" ]]; then");
        writer.WriteLine("  /bin/mv -f -- \"$DEST_PATH\" \"$BACKUP_PATH\" || { /bin/rm -rf -- \"$STAGED_PATH\"; exit 1; }");
        writer.WriteLine("fi");
        writer.WriteLine("if ! /bin/mv -f -- \"$STAGED_PATH\" \"$DEST_PATH\"; then");
        writer.WriteLine("  /bin/rm -rf -- \"$DEST_PATH\"");
        writer.WriteLine("  if [[ -e \"$BACKUP_PATH\" ]]; then /bin/mv -f -- \"$BACKUP_PATH\" \"$DEST_PATH\"; fi");
        writer.WriteLine("  /bin/rm -rf -- \"$STAGED_PATH\"");
        writer.WriteLine("  exit 1");
        writer.WriteLine("fi");
        writer.WriteLine("/bin/rm -rf -- \"$BACKUP_PATH\"");
    }
}
