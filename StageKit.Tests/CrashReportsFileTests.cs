namespace StageKit.Tests;

public sealed class CrashReportsFileTests
{
    [Fact]
    public void GetActual_WhenIdZero_ReturnsNull()
    {
        var file = new CrashReportsFile();

        Assert.Null(file.GetActual(0));
    }

    [Fact]
    public void GetActual_WhenIdNotFound_ReturnsNull()
    {
        var file = new CrashReportsFile();

        Assert.Null(file.GetActual(123456));
    }

    [Fact]
    public void GetActual_WhenIdMatches_ReturnsReport()
    {
        var file = new CrashReportsFile();
        var report = new CrashReport(new InvalidOperationException("test"), "category");

        file.Add(report);

        Assert.Same(report, file.GetActual(report.Id));
    }
}
