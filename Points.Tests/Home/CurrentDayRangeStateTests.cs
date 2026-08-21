using Points.Global;
using Xunit;

namespace Points.Tests.Home;

public sealed class CurrentDayRangeStateTests
{
    [Fact]
    public void EnsureCurrentDay_BeforeMidnight_DoesNotChangeRange()
    {
        var subject = new CurrentDayRangeState(new DateTime(2026, 8, 21, 8, 0, 0));

        var result = subject.EnsureCurrentDay(
            new DateTime(2026, 8, 21, 23, 59, 59, 999));

        Assert.False(result.Changed);
        Assert.Equal(new DateTime(2026, 8, 21), result.Start);
        Assert.Equal(new DateTime(2026, 8, 22).AddTicks(-1), result.End);
    }

    [Fact]
    public void EnsureCurrentDay_AtMidnight_RollsToNewDailyRangeOnce()
    {
        var subject = new CurrentDayRangeState(new DateTime(2026, 8, 21, 23, 59, 59));

        var rollover = subject.EnsureCurrentDay(new DateTime(2026, 8, 22));
        var repeated = subject.EnsureCurrentDay(new DateTime(2026, 8, 22, 0, 0, 1));

        Assert.True(rollover.Changed);
        Assert.Equal(new DateTime(2026, 8, 22), rollover.Start);
        Assert.Equal(new DateTime(2026, 8, 23).AddTicks(-1), rollover.End);
        Assert.True(rollover.FollowsCurrentDay);
        Assert.False(repeated.Changed);
        Assert.Equal(rollover.Start, repeated.Start);
        Assert.Equal(rollover.End, repeated.End);
    }

    [Fact]
    public void EnsureCurrentDay_AfterSeveralMissedDays_JumpsStraightToToday()
    {
        var subject = new CurrentDayRangeState(new DateTime(2026, 8, 21, 18, 0, 0));

        var result = subject.EnsureCurrentDay(new DateTime(2026, 8, 25, 8, 30, 0));

        Assert.True(result.Changed);
        Assert.Equal(new DateTime(2026, 8, 25), result.Start);
        Assert.Equal(new DateTime(2026, 8, 26).AddTicks(-1), result.End);
    }

    [Fact]
    public void SetRange_HistoricalRange_DoesNotFollowCurrentDay()
    {
        var subject = new CurrentDayRangeState(new DateTime(2026, 8, 21, 18, 0, 0));
        var historicalStart = new DateTime(2026, 8, 10);
        var historicalEnd = new DateTime(2026, 8, 11).AddTicks(-1);

        subject.SetRange(
            historicalStart,
            historicalEnd,
            new DateTime(2026, 8, 21, 18, 0, 0),
            followsCurrentDay: false);
        var result = subject.EnsureCurrentDay(new DateTime(2026, 8, 22, 8, 0, 0));

        Assert.False(result.Changed);
        Assert.False(result.FollowsCurrentDay);
        Assert.Equal(historicalStart, result.Start);
        Assert.Equal(historicalEnd, result.End);
    }

    [Fact]
    public void SetRange_TodayRange_ContinuesFollowingCurrentDay()
    {
        var subject = new CurrentDayRangeState(new DateTime(2026, 8, 21, 18, 0, 0));

        var selected = subject.SetRange(
            new DateTime(2026, 8, 21),
            new DateTime(2026, 8, 22).AddTicks(-1),
            new DateTime(2026, 8, 21, 18, 0, 0),
            followsCurrentDay: true);
        var result = subject.EnsureCurrentDay(new DateTime(2026, 8, 22));

        Assert.True(selected.FollowsCurrentDay);
        Assert.True(result.Changed);
        Assert.Equal(new DateTime(2026, 8, 22), result.Start);
        Assert.Equal(new DateTime(2026, 8, 23).AddTicks(-1), result.End);
    }

    [Fact]
    public void SetRange_CustomTodayRange_RemainsFixedAfterMidnight()
    {
        var subject = new CurrentDayRangeState(new DateTime(2026, 8, 21, 18, 0, 0));
        var selectedStart = new DateTime(2026, 8, 21);
        var selectedEnd = new DateTime(2026, 8, 22).AddTicks(-1);

        subject.SetRange(
            selectedStart,
            selectedEnd,
            new DateTime(2026, 8, 21, 18, 0, 0),
            followsCurrentDay: false);
        var result = subject.EnsureCurrentDay(new DateTime(2026, 8, 22));

        Assert.False(result.Changed);
        Assert.False(result.FollowsCurrentDay);
        Assert.Equal(selectedStart, result.Start);
        Assert.Equal(selectedEnd, result.End);
    }

    [Fact]
    public void SetRange_FollowingToday_UsesSaveTimeDate()
    {
        var subject = new CurrentDayRangeState(new DateTime(2026, 8, 21, 23, 59, 0));

        var result = subject.SetRange(
            new DateTime(2026, 8, 21),
            new DateTime(2026, 8, 22).AddTicks(-1),
            new DateTime(2026, 8, 22, 0, 0, 0),
            followsCurrentDay: true);

        Assert.True(result.FollowsCurrentDay);
        Assert.Equal(new DateTime(2026, 8, 22), result.Start);
        Assert.Equal(new DateTime(2026, 8, 23).AddTicks(-1), result.End);
    }
}
