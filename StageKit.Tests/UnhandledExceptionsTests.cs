using StageKit.Extensions;

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
    public void EnumerateExceptions_WhenUsingDefaultTraversal_ReturnsExceptionTreeInDepthFirstPreOrder()
    {
        var exception = new AggregateException(
            new InvalidOperationException("first", new IOException("first child")),
            new AggregateException(new ArgumentException("second")));

        var exceptionTypes = exception
            .EnumerateExceptions()
            .Select(item => item.GetType())
            .ToArray();

        Assert.Equal(
            [
                typeof(AggregateException),
                typeof(InvalidOperationException),
                typeof(IOException),
                typeof(AggregateException),
                typeof(ArgumentException)
            ],
            exceptionTypes);
    }

    [Fact]
    public void EnumerateExceptions_WhenUsingInnerExceptionChain_DoesNotExpandAggregateBranches()
    {
        var exception = new AggregateException(
            new InvalidOperationException("first", new IOException("first child")),
            new ArgumentException("second"));

        var messages = exception
            .EnumerateExceptions(ExceptionTraversalType.InnerExceptionChain)
            .Select(item => item.Message)
            .ToArray();

        Assert.Equal([exception.Message, "first", "first child"], messages);
    }

    [Fact]
    public void EnumerateExceptions_WhenTraversalTypeIsUnsupported_ThrowsArgumentOutOfRangeException()
    {
        var exceptions = new InvalidOperationException()
            .EnumerateExceptions((ExceptionTraversalType)(-1));

        Assert.Throws<ArgumentOutOfRangeException>(() => exceptions.ToArray());
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
    public void CanIgnoreException_WhenAggregateChildInnerExceptionMatches_ReturnsTrue()
    {
        try
        {
            UnhandledExceptions.IgnoredExceptionMessages.Add("nested match");
            var aggregate = new AggregateException(
                new InvalidOperationException("outer", new ArgumentException("nested match")));

            var result = UnhandledExceptions.CanIgnoreException(aggregate);

            Assert.True(result);
        }
        finally
        {
            UnhandledExceptions.IgnoredExceptionMessages.Remove("nested match");
        }
    }

    [Fact]
    public void CanIgnoreException_WhenNoMatch_ReturnsFalse()
    {
        var result = UnhandledExceptions.CanIgnoreException(new InvalidOperationException("unexpected"));
        Assert.False(result);
    }

    [Fact]
    public void CurrentDomainOnUnhandledException_WhenIgnored_RaisesExceptionThrownOnce()
    {
        var exception = new InvalidOperationException("ignored");
        var invocationCount = 0;
        EventHandler<StageKitExceptionEventArgs> handler = (_, _) => invocationCount++;

        try
        {
            UnhandledExceptions.IgnoredExceptionMessages.Add(exception.Message);
            UnhandledExceptions.ExceptionThrown += handler;

            UnhandledExceptions.CurrentDomainOnUnhandledException(
                null,
                new UnhandledExceptionEventArgs(exception, false));

            Assert.Equal(1, invocationCount);
        }
        finally
        {
            UnhandledExceptions.ExceptionThrown -= handler;
            UnhandledExceptions.IgnoredExceptionMessages.Remove(exception.Message);
        }
    }

    [Fact]
    public void CurrentDomainOnUnhandledException_WhenSubscriberThrows_ContinuesInvokingSubscribers()
    {
        var invocationCount = 0;
        EventHandler<StageKitExceptionEventArgs> throwingHandler =
            (_, _) => throw new InvalidOperationException("subscriber failed");
        EventHandler<StageKitExceptionEventArgs> countingHandler = (_, _) => invocationCount++;

        try
        {
            UnhandledExceptions.ExceptionThrown += throwingHandler;
            UnhandledExceptions.ExceptionThrown += countingHandler;

            UnhandledExceptions.CurrentDomainOnUnhandledException(
                null,
                new UnhandledExceptionEventArgs(new InvalidOperationException("unhandled"), false));

            Assert.Equal(1, invocationCount);
        }
        finally
        {
            UnhandledExceptions.ExceptionThrown -= throwingHandler;
            UnhandledExceptions.ExceptionThrown -= countingHandler;
        }
    }

    [Fact]
    public void TaskSchedulerOnUnobservedTaskException_WhenIgnored_RaisesExceptionThrownOnce()
    {
        var exception = new AggregateException(new InvalidOperationException("ignored task"));
        var invocationCount = 0;
        EventHandler<StageKitExceptionEventArgs> handler = (_, _) => invocationCount++;

        try
        {
            UnhandledExceptions.IgnoredExceptionMessages.Add("ignored task");
            UnhandledExceptions.ExceptionThrown += handler;

            UnhandledExceptions.TaskSchedulerOnUnobservedTaskException(
                null,
                new UnobservedTaskExceptionEventArgs(exception));

            Assert.Equal(1, invocationCount);
        }
        finally
        {
            UnhandledExceptions.ExceptionThrown -= handler;
            UnhandledExceptions.IgnoredExceptionMessages.Remove("ignored task");
        }
    }

    [Fact]
    public void HandleUnhandledException_WhenIgnored_RaisesExceptionThrownWithIgnoredFlag()
    {
        var exception = new InvalidOperationException("direct ignored");
        StageKitExceptionEventArgs? receivedArgs = null;
        EventHandler<StageKitExceptionEventArgs> handler = (_, args) => receivedArgs = args;

        try
        {
            UnhandledExceptions.IgnoredExceptionMessages.Add(exception.Message);
            UnhandledExceptions.ExceptionThrown += handler;

            UnhandledExceptions.HandleUnhandledException(exception);

            Assert.NotNull(receivedArgs);
            Assert.True(receivedArgs.IsIgnored);
        }
        finally
        {
            UnhandledExceptions.ExceptionThrown -= handler;
            UnhandledExceptions.IgnoredExceptionMessages.Remove(exception.Message);
        }
    }

    [Fact]
    public void HandleUnhandledException_WhenEventArgsAreIgnored_DoesNotForceExit()
    {
        var invocationCount = 0;
        EventHandler<StageKitExceptionEventArgs> handler = (_, _) => invocationCount++;

        try
        {
            UnhandledExceptions.ExceptionThrown += handler;
            var args = new StageKitExceptionEventArgs(new InvalidOperationException("ignored"), false)
            {
                IsIgnored = true
            };

            UnhandledExceptions.HandleUnhandledException(args, false);

            Assert.Equal(1, invocationCount);
        }
        finally
        {
            UnhandledExceptions.ExceptionThrown -= handler;
        }
    }
}
