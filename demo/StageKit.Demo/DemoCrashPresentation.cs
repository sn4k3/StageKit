namespace StageKit.Demo;

public sealed record DemoCrashPresentation(long ReportId, string ReportText)
{
    public static DemoCrashPresentation? Create(
        bool hasCrashReportFlag,
        long crashReportIndex,
        string? crashReport)
    {
        if (!hasCrashReportFlag || crashReportIndex <= 0 || string.IsNullOrWhiteSpace(crashReport))
            return null;

        return new DemoCrashPresentation(crashReportIndex, crashReport);
    }
}
