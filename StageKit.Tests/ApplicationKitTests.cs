namespace StageKit.Tests;

public sealed class ApplicationKitTests
{
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
            var invalidArgsCases = new[]
            {
                new[] { "--other" },
                new[] { "--report" },
                new[] { "--report", "not-a-number" },
                new[] { "--report", "0" },
                new[] { "--report", "-1" },
            };

            foreach (var args in invalidArgsCases)
            {
                ApplicationKit.CrashReportFlag = "--report";
                ApplicationKit.ApplicationArgs = ["--report", "42"];
                Assert.Equal(42, ApplicationKit.CrashReportIndex);

                ApplicationKit.ApplicationArgs = args;

                Assert.Equal(0, ApplicationKit.CrashReportIndex);
            }
        }
        finally
        {
            ApplicationKit.CrashReportFlag = originalFlag;
            ApplicationKit.ApplicationArgs = originalArgs;
        }
    }
}
