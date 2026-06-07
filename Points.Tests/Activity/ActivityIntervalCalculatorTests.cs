using Points.Models;
using Points.Services.Activity;
using Xunit;

namespace Points.Tests.Activity;

public sealed class ActivityIntervalCalculatorTests
{
    [Fact]
    public void GetActiveTimeInRange_CountsOnlyTheRequestedOverlap()
    {
        var activity = new[]
        {
            new ActivityModel(
                start: new DateTime(2026, 4, 29, 9, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2026, 4, 29, 11, 0, 0, DateTimeKind.Utc),
                rate: "Base",
                value: 1)
        };

        var result = ActivityIntervalCalculator.GetActiveTimeInRange(
            activity,
            new DateTime(2026, 4, 29, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 29, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 29, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(TimeSpan.FromHours(1), result);
    }

    [Fact]
    public void GetActiveTimeInRange_IgnoresActivityOutsideTheRequestedWindow()
    {
        var activity = new[]
        {
            new ActivityModel(
                start: new DateTime(2026, 4, 29, 7, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2026, 4, 29, 8, 0, 0, DateTimeKind.Utc),
                rate: "Base",
                value: 1)
        };

        var result = ActivityIntervalCalculator.GetActiveTimeInRange(
            activity,
            new DateTime(2026, 4, 29, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 29, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 29, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void GetActiveTimeInRange_ClipsOpenActivityToNow()
    {
        var activity = new[]
        {
            new ActivityModel(
                start: new DateTime(2026, 4, 29, 9, 0, 0, DateTimeKind.Utc),
                end: null,
                rate: "Base",
                value: 1)
        };

        var result = ActivityIntervalCalculator.GetActiveTimeInRange(
            activity,
            new DateTime(2026, 4, 29, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 29, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 29, 10, 30, 0, DateTimeKind.Utc));

        Assert.Equal(TimeSpan.FromMinutes(30), result);
    }
}
