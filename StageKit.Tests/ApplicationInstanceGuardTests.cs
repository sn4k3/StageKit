namespace StageKit.Tests;

public sealed class ApplicationInstanceGuardTests
{
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
