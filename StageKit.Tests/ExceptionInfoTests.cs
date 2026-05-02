using StageKit;

namespace StageKit.Tests;

public sealed class ExceptionInfoTests
{
    [Fact]
    public void Constructor_WhenAggregateException_FlattensInnerExceptionsIntoLinkedChain()
    {
        var exception = new AggregateException(
            new InvalidOperationException("first"),
            new AggregateException(new ArgumentException("second")));

        var info = new ExceptionInfo(exception);
        var messages = info.TraverseExceptions().Select(item => item.Message).ToArray();

        Assert.Equal(["first", "second"], messages);
    }

    [Fact]
    public void Constructor_WhenStackTraceDisabled_LeavesStackTraceNull()
    {
        var info = new ExceptionInfo(new InvalidOperationException("message"), includeStackTrace: false);

        Assert.Null(info.StackTrace);
    }
}
