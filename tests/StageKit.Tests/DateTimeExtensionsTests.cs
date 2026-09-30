using StageKit.Extensions;

namespace StageKit.Tests;

public sealed class DateTimeExtensionsTests
{
    [Fact]
    public void ToTimeString_MinValue_DoesNotOverflow()
    {
        var result = TimeSpan.MinValue.ToTimeString();

        Assert.StartsWith("-10675199days", result);
    }
}