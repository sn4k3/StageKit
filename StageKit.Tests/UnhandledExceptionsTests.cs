namespace StageKit.Tests;

public sealed class UnhandledExceptionsTests
{
    [Fact]
    public void CanIgnoreException_WhenRegisteredBaseTypeMatches_ReturnsTrue()
    {
        try
        {
            UnhandledExceptions.IgnoredExceptionList.Add(typeof(IOException));

            var result = UnhandledExceptions.CanIgnoreException(new DirectoryNotFoundException("missing"));

            Assert.True(result);
        }
        finally
        {
            UnhandledExceptions.IgnoredExceptionList.Remove(typeof(IOException));
        }
    }

    [Fact]
    public void CanIgnoreException_WhenMessageFragmentMatchesInnerException_ReturnsTrue()
    {
        const string fragment = "known benign";

        try
        {
            UnhandledExceptions.IgnoredExceptionMessages.Add(fragment);
            var exception = new InvalidOperationException(
                "outer",
                new ArgumentException("this is known benign"));

            var result = UnhandledExceptions.CanIgnoreException(exception);

            Assert.True(result);
        }
        finally
        {
            UnhandledExceptions.IgnoredExceptionMessages.Remove(fragment);
        }
    }

    [Fact]
    public void TraverseExceptions_WhenAggregateException_ReturnsFlattenedExceptions()
    {
        var exception = new AggregateException(
            new InvalidOperationException("first"),
            new AggregateException(new ArgumentException("second")));

        var messages = UnhandledExceptions
            .TraverseExceptions(exception)
            .Select(item => item.Message)
            .ToArray();

        Assert.Equal(["first", "second"], messages);
    }

    [Fact]
    public void CanIgnoreException_WhenAggregateInnerMatchesBaseType_ReturnsTrue()
    {
        try
        {
            UnhandledExceptions.IgnoredExceptionList.Add(typeof(IOException));

            var aggregate = new AggregateException(new FileNotFoundException("missing"));
            var result = UnhandledExceptions.CanIgnoreException(aggregate);

            Assert.True(result);
        }
        finally
        {
            UnhandledExceptions.IgnoredExceptionList.Remove(typeof(IOException));
        }
    }

    [Fact]
    public void CanIgnoreException_WhenNoMatch_ReturnsFalse()
    {
        var result = UnhandledExceptions.CanIgnoreException(new InvalidOperationException("unexpected"));
        Assert.False(result);
    }
}
