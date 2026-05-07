namespace StageKit.Tests;

public sealed class ApplicationInstanceGuardTests
{
    [Fact]
    public async Task Acquire_WhenNameAlreadyOwned_ReturnsSecondary()
    {
        var instanceName = CreateInstanceName();
        using var primary = ApplicationInstanceGuard.Acquire(instanceName);

        using var secondary = await Task.Run(() => ApplicationInstanceGuard.Acquire(instanceName));

        Assert.True(primary.IsPrimary);
        Assert.False(primary.IsSecondary);
        Assert.False(secondary.IsPrimary);
        Assert.True(secondary.IsSecondary);
    }

    [Fact]
    public void Acquire_AfterPrimaryDisposed_ReturnsPrimary()
    {
        var instanceName = CreateInstanceName();
        using (var primary = ApplicationInstanceGuard.Acquire(instanceName))
        {
            Assert.True(primary.IsPrimary);
        }

        using var next = ApplicationInstanceGuard.Acquire(instanceName);

        Assert.True(next.IsPrimary);
        Assert.False(next.IsSecondary);
    }

    [Fact]
    public async Task Acquire_AfterPrimaryDisposedOnDifferentThread_ReturnsPrimary()
    {
        var instanceName = CreateInstanceName();
        ApplicationInstanceGuard? primary = null;
        var acquireThreadId = 0;
        await Task.Run(
            () =>
            {
                acquireThreadId = Environment.CurrentManagedThreadId;
                primary = ApplicationInstanceGuard.Acquire(instanceName);
            },
            TestContext.Current.CancellationToken);

        Assert.NotNull(primary);
        Assert.True(primary.IsPrimary);

        if (Environment.CurrentManagedThreadId == acquireThreadId)
        {
            await Task.Run(primary.Dispose, TestContext.Current.CancellationToken);
        }
        else
        {
            primary.Dispose();
        }

        using var next = ApplicationInstanceGuard.Acquire(instanceName);

        Assert.True(next.IsPrimary);
        Assert.False(next.IsSecondary);
    }

    [Fact]
    public void Acquire_WhenNamesDiffer_ReturnsPrimaryForBoth()
    {
        using var first = ApplicationInstanceGuard.Acquire(CreateInstanceName());
        using var second = ApplicationInstanceGuard.Acquire(CreateInstanceName());

        Assert.True(first.IsPrimary);
        Assert.True(second.IsPrimary);
    }

    [Fact]
    public void Acquire_WhenNameIsEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => ApplicationInstanceGuard.Acquire(" "));
    }

    private static string CreateInstanceName()
    {
        return $"StageKit.Tests.{Guid.NewGuid():N}";
    }
}
