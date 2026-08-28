using System.Text.Json;

namespace StageKit.Tests;

public sealed class ExceptionInfoTests
{
    [Fact]
    public void Constructor_WhenAggregateException_CapturesTreeInDepthFirstPreOrder()
    {
        var exception = new AggregateException(
            new InvalidOperationException("first", new IOException("first child")),
            new AggregateException(new ArgumentException("second")));

        var info = new ExceptionInfo(exception);
        var messages = info.EnumerateExceptions().Select(item => item.Message).ToArray();

        Assert.Equal(
            [
                exception.Message,
                "first",
                "first child",
                exception.InnerExceptions[1].Message,
                "second"
            ],
            messages);
    }

    [Fact]
    public void Constructor_WhenAggregateException_PreservesAggregateMetadata()
    {
        var exception = new AggregateException("aggregate message", new InvalidOperationException("inner"));

        var info = new ExceptionInfo(exception);

        Assert.Equal(typeof(AggregateException).FullName, info.Type);
        Assert.Equal(exception.Message, info.Message);
    }

    [Fact]
    public void Constructor_WhenInnerExceptionsDisabled_CapturesOnlySuppliedAggregate()
    {
        var exception = new AggregateException("aggregate message", new InvalidOperationException("inner"));

        var info = new ExceptionInfo(exception, includeInnerException: false);

        Assert.Equal(typeof(AggregateException).FullName, info.Type);
        Assert.Equal(exception.Message, info.Message);
        Assert.Null(info.InnerException);
    }

    [Fact]
    public void Constructor_WhenAggregateContainsManyExceptions_CreatesLinkedChainWithoutRecursiveConstruction()
    {
        const int innerExceptionCount = 10_000;
        var innerExceptions = Enumerable
            .Range(0, innerExceptionCount)
            .Select(index => new InvalidOperationException(index.ToString()));
        var exception = new AggregateException(innerExceptions);

        var info = new ExceptionInfo(exception, includeStackTrace: false);

        Assert.Equal(innerExceptionCount + 1, info.EnumerateExceptions().Count());
    }

    [Fact]
    public void Constructor_WhenRegularExceptionContainsManyInnerExceptions_CreatesLinkedChainWithoutRecursiveConstruction()
    {
        const int innerExceptionCount = 10_000;
        Exception exception = new InvalidOperationException("leaf");
        for (var i = 0; i < innerExceptionCount; i++)
        {
            exception = new InvalidOperationException(i.ToString(), exception);
        }

        var info = new ExceptionInfo(exception, includeStackTrace: false);

        Assert.Equal(innerExceptionCount + 1, info.EnumerateExceptions().Count());
    }

    [Fact]
    public void Constructor_WhenUsingInnerExceptionChainTraversal_FollowsDirectInnerExceptionChain()
    {
        var first = new InvalidOperationException("first");
        var second = new ArgumentException("second");
        var exception = new AggregateException(first, second);

        var info = new ExceptionInfo(exception, traversalType: ExceptionTraversalType.InnerExceptionChain);
        var messages = info.EnumerateExceptions().Select(item => item.Message).ToArray();

        Assert.Equal([exception.Message, "first"], messages);
    }

    [Fact]
    public void Constructor_WhenStackTraceDisabled_LeavesStackTraceNull()
    {
        var info = new ExceptionInfo(new InvalidOperationException("message"), includeStackTrace: false);

        Assert.Null(info.StackTrace);
    }

    [Fact]
    public void Serialize_WhenOptionalValuesAreNull_OmitsOptionalProperties()
    {
        var info = new ExceptionInfo(new InvalidOperationException("message"), includeStackTrace: false);

        var json = JsonSerializer.Serialize(info);

        Assert.DoesNotContain("\"Source\"", json);
        Assert.DoesNotContain("\"StackTrace\"", json);
        Assert.DoesNotContain("\"InnerException\"", json);
    }
}
