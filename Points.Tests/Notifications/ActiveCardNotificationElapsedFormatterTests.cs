using Points.Services;
using Xunit;

namespace Points.Tests.Notifications;

public sealed class ActiveCardNotificationElapsedFormatterTests
{
    [Theory]
    [InlineData(0, 0, 0, "00:00:00")]
    [InlineData(2, 3, 4, "02:03:04")]
    [InlineData(27, 5, 6, "27:05:06")]
    public void Format_UsesTotalHours(int hours, int minutes, int seconds, string expected)
    {
        Assert.Equal(expected, ActiveCardNotificationElapsedFormatter.Format(
            TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Format_ClampsNegativeElapsedToZero()
    {
        Assert.Equal("00:00:00", ActiveCardNotificationElapsedFormatter.Format(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void CalculateElapsed_UsesUtcAndClampsFutureStart()
    {
        var nowUtc = new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            TimeSpan.FromHours(26.5),
            ActiveCardNotificationElapsedFormatter.CalculateElapsed(nowUtc.AddHours(-26.5), nowUtc));
        Assert.Equal(
            TimeSpan.Zero,
            ActiveCardNotificationElapsedFormatter.CalculateElapsed(nowUtc.AddMinutes(1), nowUtc));
        Assert.Throws<ArgumentException>(() =>
            ActiveCardNotificationElapsedFormatter.CalculateElapsed(
                DateTime.SpecifyKind(nowUtc, DateTimeKind.Unspecified),
                nowUtc));
    }
}
