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
        }
        finally
        {
            ApplicationKit.CrashReportFlag = originalFlag;
            ApplicationKit.ApplicationArgs = originalArgs;
        }
    }
}
