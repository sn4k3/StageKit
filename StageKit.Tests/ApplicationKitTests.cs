namespace StageKit.Tests;

public sealed class ApplicationKitTests
{
    [Fact]
    public void RuntimeElapsed_ApproximatelyMatchesCurrentProcessRuntime()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var expectedRuntime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

        var actualRuntime = ApplicationKit.RuntimeElapsed;

        Assert.InRange(actualRuntime, expectedRuntime - TimeSpan.FromSeconds(1),
            expectedRuntime + TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ApplicationArgs_WhenCrashReportFlagExists_ParsesCrashReportIndex()
    {
        var originalFlag = ApplicationKit.CrashReportFlag;
        var originalArgs = ApplicationKit.ApplicationArgs;

        try
        {
            ApplicationKit.CrashReportFlag = "--report";

            ApplicationKit.ApplicationArgs = ["--other", "--report", "123"];

            Assert.True(ApplicationKit.HasCrashReportFlag);
            Assert.Equal(123, ApplicationKit.CrashReportIndex);
        }
        finally
        {
            ApplicationKit.CrashReportFlag = originalFlag;
            ApplicationKit.ApplicationArgs = originalArgs;
        }
    }

    [Fact]
    public void ApplicationArgs_WhenCrashReportFlagMissing_ClearsFlag()
    {
        var originalArgs = ApplicationKit.ApplicationArgs;

        try
        {
            ApplicationKit.ApplicationArgs = [];

            Assert.False(ApplicationKit.HasCrashReportFlag);
            Assert.Equal(0, ApplicationKit.CrashReportIndex);
        }
        finally
        {
            ApplicationKit.ApplicationArgs = originalArgs;
        }
    }

    [Fact]
    public void ApplicationArgs_WhenSetToNull_ClearsFlagWithoutThrowing()
    {
        var originalArgs = ApplicationKit.ApplicationArgs;
        var originalFlag = ApplicationKit.CrashReportFlag;

        try
        {
            ApplicationKit.CrashReportFlag = "--report";
            ApplicationKit.ApplicationArgs = ["--report", "42"];
            Assert.True(ApplicationKit.HasCrashReportFlag);

            ApplicationKit.ApplicationArgs = null;

            Assert.Null(ApplicationKit.ApplicationArgs);
            Assert.False(ApplicationKit.HasCrashReportFlag);
            Assert.Equal(0, ApplicationKit.CrashReportIndex);
        }
        finally
        {
            ApplicationKit.CrashReportFlag = originalFlag;
            ApplicationKit.ApplicationArgs = originalArgs;
        }
    }

    [Fact]
    public void ApplicationArgs_WhenCrashReportIndexIsMissingOrInvalid_ClearsPreviousIndex()
    {
        var originalArgs = ApplicationKit.ApplicationArgs;
        var originalFlag = ApplicationKit.CrashReportFlag;

        try
        {
            var invalidArgsCases = new (string[] Args, bool HasFlag)[]
            {
                (["--other"], false),
                (["--report"], true),
                (["--report", "not-a-number"], true),
                (["--report", "0"], true),
                (["--report", "-1"], true)
            };

            foreach (var (args, hasFlag) in invalidArgsCases)
            {
                ApplicationKit.CrashReportFlag = "--report";
                ApplicationKit.ApplicationArgs = ["--report", "42"];
                Assert.Equal(42, ApplicationKit.CrashReportIndex);

                ApplicationKit.ApplicationArgs = args;

                Assert.Equal(hasFlag, ApplicationKit.HasCrashReportFlag);
                Assert.Equal(0, ApplicationKit.CrashReportIndex);
            }
        }
        finally
        {
            ApplicationKit.CrashReportFlag = originalFlag;
            ApplicationKit.ApplicationArgs = originalArgs;
        }
    }

    [Fact]
    public void ApplicationArgs_WhenSourceOrReturnedArrayIsMutated_KeepsParsedState()
    {
        var originalArgs = ApplicationKit.ApplicationArgs;
        var originalFlag = ApplicationKit.CrashReportFlag;

        try
        {
            ApplicationKit.CrashReportFlag = "--report";
            string[] args = ["--report", "42"];

            ApplicationKit.ApplicationArgs = args;
            args[0] = "--other";
            ApplicationKit.ApplicationArgs[1] = "99";

            Assert.Equal(["--other", "99"], ApplicationKit.ApplicationArgs);
            Assert.True(ApplicationKit.HasCrashReportFlag);
            Assert.Equal(42, ApplicationKit.CrashReportIndex);
        }
        finally
        {
            ApplicationKit.CrashReportFlag = originalFlag;
            ApplicationKit.ApplicationArgs = originalArgs;
        }
    }

    [Fact]
    public void CrashReportFlag_WhenChanged_ReparsesApplicationArgs()
    {
        var originalArgs = ApplicationKit.ApplicationArgs;
        var originalFlag = ApplicationKit.CrashReportFlag;

        try
        {
            ApplicationKit.ApplicationArgs = ["--first", "42", "--second", "99"];

            ApplicationKit.CrashReportFlag = "--first";
            Assert.True(ApplicationKit.HasCrashReportFlag);
            Assert.Equal(42, ApplicationKit.CrashReportIndex);

            ApplicationKit.CrashReportFlag = "--second";
            Assert.True(ApplicationKit.HasCrashReportFlag);
            Assert.Equal(99, ApplicationKit.CrashReportIndex);

            ApplicationKit.CrashReportFlag = "--missing";
            Assert.False(ApplicationKit.HasCrashReportFlag);
            Assert.Equal(0, ApplicationKit.CrashReportIndex);
        }
        finally
        {
            ApplicationKit.CrashReportFlag = originalFlag;
            ApplicationKit.ApplicationArgs = originalArgs;
        }
    }

    [Fact]
    public void ApplicationName_WhenProfilePathUsesDefault_UpdatesProfilePath()
    {
        var originalApplicationName = ApplicationKit.ApplicationName;
        var originalProfilePath = ApplicationKit.ProfilePath;

        try
        {
            ApplicationKit.ProfilePath = ApplicationKit.GetDefaultProfilePath();

            ApplicationKit.ApplicationName = $"{originalApplicationName}-Changed";

            Assert.Equal(ApplicationKit.GetDefaultProfilePath(), ApplicationKit.ProfilePath);
        }
        finally
        {
            ApplicationKit.ApplicationName = originalApplicationName;
            ApplicationKit.ProfilePath = originalProfilePath;
        }
    }

    [Fact]
    public void ApplicationName_WhenProfilePathIsCustom_PreservesProfilePath()
    {
        var originalApplicationName = ApplicationKit.ApplicationName;
        var originalProfilePath = ApplicationKit.ProfilePath;
        var customProfilePath = Path.Combine(Path.GetTempPath(), "StageKit-CustomProfile");

        try
        {
            ApplicationKit.ProfilePath = customProfilePath;

            ApplicationKit.ApplicationName = $"{originalApplicationName}-Changed";

            Assert.Equal(customProfilePath, ApplicationKit.ProfilePath);
        }
        finally
        {
            ApplicationKit.ApplicationName = originalApplicationName;
            ApplicationKit.ProfilePath = originalProfilePath;
        }
    }

    [Fact]
    public void ParseProfilePathFromArgs_WhenProfilePathDoesNotExistAndParentIsWritable_SetsProfilePath()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var existingProfilePath = CreateTempDirectory();
        var missingProfilePath = Path.Combine(existingProfilePath, "missing");

        try
        {
            ApplicationKit.ProfilePath = existingProfilePath;

            ApplicationKit.ParseProfilePathFromArgs(["--profile-path", missingProfilePath]);

            Assert.Equal(missingProfilePath, ApplicationKit.ProfilePath);
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
            Directory.Delete(existingProfilePath, true);
        }
    }

    [Fact]
    public void ParseProfilePathFromArgs_WhenProfilePathIsFile_PreservesProfilePath()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var existingProfilePath = CreateTempDirectory();
        var filePath = Path.Combine(existingProfilePath, "profile-path.txt");

        try
        {
            File.WriteAllText(filePath, "not a directory");
            ApplicationKit.ProfilePath = existingProfilePath;

            ApplicationKit.ParseProfilePathFromArgs(["--profile-path", filePath]);

            Assert.Equal(existingProfilePath, ApplicationKit.ProfilePath);
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
            Directory.Delete(existingProfilePath, true);
        }
    }

    [Fact]
    public void ParseProfilePathFromArgs_WhenProfilePathIsCurrentDirectory_SetsFullProfilePath()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;

        try
        {
            ApplicationKit.ParseProfilePathFromArgs(["--profile-path", "."]);

            Assert.Equal(Path.GetFullPath("."), ApplicationKit.ProfilePath);
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
        }
    }

    [Fact]
    public void ParseProfilePathFromArgs_WhenProfilePathHasInvalidCharacters_PreservesProfilePath()
    {
        if (!OperatingSystem.IsWindows()) return;

        var originalProfilePath = ApplicationKit.ProfilePath;
        var existingProfilePath = CreateTempDirectory();
        var invalidProfilePath = Path.Combine(existingProfilePath, "bad<name");

        try
        {
            ApplicationKit.ProfilePath = existingProfilePath;

            ApplicationKit.ParseProfilePathFromArgs(["--profile-path", invalidProfilePath]);

            Assert.Equal(existingProfilePath, ApplicationKit.ProfilePath);
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
            Directory.Delete(existingProfilePath, true);
        }
    }

    [Fact]
    public void ParseProfilePathFromArgs_WhenPortableArgHasNegativeLevel_PreservesProfilePath()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var originalIsPortable = ApplicationKit.IsPortable;
        var existingProfilePath = CreateTempDirectory();

        try
        {
            ApplicationKit.IsPortable = false;
            ApplicationKit.ProfilePath = existingProfilePath;

            ApplicationKit.ParseProfilePathFromArgs(["--portable", "-1"]);

            Assert.Equal(existingProfilePath, ApplicationKit.ProfilePath);
            Assert.False(ApplicationKit.IsPortable);
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
            ApplicationKit.IsPortable = originalIsPortable;
            Directory.Delete(existingProfilePath, true);
        }
    }

    [Fact]
    public void ParseProfilePathFromArgs_WhenProfilePathOverridesPortableArg_ClearsIsPortable()
    {
        var originalProfilePath = ApplicationKit.ProfilePath;
        var originalIsPortable = ApplicationKit.IsPortable;
        var profilePath = CreateTempDirectory();
        var portableProfilePath = GetPortableProfilePath(0);
        var portablePathExistedBefore = Directory.Exists(portableProfilePath);

        try
        {
            ApplicationKit.IsPortable = false;

            ApplicationKit.ParseProfilePathFromArgs(["--portable", "--profile-path", profilePath]);

            Assert.Equal(profilePath, ApplicationKit.ProfilePath);
            Assert.False(ApplicationKit.IsPortable);
        }
        finally
        {
            ApplicationKit.ProfilePath = originalProfilePath;
            ApplicationKit.IsPortable = originalIsPortable;
            Directory.Delete(profilePath, true);
            DeleteDirectoryIfCreated(portableProfilePath, portablePathExistedBefore);
        }
    }

    private static string CreateTempDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"StageKit-Test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static string GetPortableProfilePath(int portableLevel)
    {
        var portableRootPath =
            Runtime.EntryApplication.BaseDirectory ?? Runtime.EntryApplication.AppContextBaseDirectory;
        for (var i = 0; i < portableLevel; i++) portableRootPath = Directory.GetParent(portableRootPath)!.FullName;

        var portableDirectoryName = portableRootPath == Runtime.EntryApplication.ProcessPath
            ? ApplicationKit.PortableProfileDirectoryName
            : $"{ApplicationKit.ApplicationName}_{ApplicationKit.PortableProfileDirectoryName}";

        return Path.GetFullPath(Path.Combine(portableRootPath, portableDirectoryName));
    }

    private static void DeleteDirectoryIfCreated(string directoryPath, bool existedBefore)
    {
        if (!existedBefore && Directory.Exists(directoryPath)) Directory.Delete(directoryPath, true);
    }
}