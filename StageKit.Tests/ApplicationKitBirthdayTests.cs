using StageKit;

namespace StageKit.Tests;

public sealed class ApplicationKitBirthdayTests
{
    [Fact]
    public void IsBirthday_WhenBornToday_ReturnsTrue()
    {
        var originalBorn = ApplicationKit.Born;

        try
        {
            ApplicationKit.Born = DateTime.UtcNow.Date;

            Assert.True(ApplicationKit.IsBirthday);
        }
        finally
        {
            ApplicationKit.Born = originalBorn;
        }
    }

    [Fact]
    public void IsBirthdayWithOffset_WhenWithinWindow_ReturnsTrue()
    {
        var originalBorn = ApplicationKit.Born;

        try
        {
            ApplicationKit.Born = DateTime.UtcNow.Date.AddDays(-3);

            Assert.True(ApplicationKit.IsBirthdayWithOffset(7));
        }
        finally
        {
            ApplicationKit.Born = originalBorn;
        }
    }

    [Fact]
    public void IsBirthdayWithOffset_WhenBornInFuture_ReturnsFalse()
    {
        var originalBorn = ApplicationKit.Born;

        try
        {
            ApplicationKit.Born = DateTime.UtcNow.Date.AddDays(1);

            Assert.False(ApplicationKit.IsBirthdayWithOffset(7));
        }
        finally
        {
            ApplicationKit.Born = originalBorn;
        }
    }

    [Fact]
    public void AgeShortStr_WhenBornToday_ReturnsZeroYears()
    {
        var originalBorn = ApplicationKit.Born;

        try
        {
            ApplicationKit.Born = DateTime.UtcNow.Date;

            Assert.Equal("0 year", ApplicationKit.AgeShortStr);
        }
        finally
        {
            ApplicationKit.Born = originalBorn;
        }
    }
}
