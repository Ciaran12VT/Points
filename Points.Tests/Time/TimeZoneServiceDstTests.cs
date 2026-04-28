using Points.Services.Scheduling;
using Points.Services.Time;
using Xunit;

namespace Points.Tests.Time;

public sealed class TimeZoneServiceDstTests
{
    private readonly TimeZoneService _service = new();
    private readonly TimeZoneInfo _dublin = TestTimeZones.Dublin;

    [Fact]
    public void LocalDayRangeToUtc_UsesTwentyThreeHourRangeOnSpringForwardDay()
    {
        var range = _service.LocalDayRangeToUtc(new DateTime(2026, 3, 29), _dublin);

        AssertUtc(2026, 3, 29, 0, 0, range.StartUtc);
        AssertUtc(2026, 3, 29, 23, 0, range.EndUtc);
        Assert.Equal(TimeSpan.FromHours(23), range.EndUtc - range.StartUtc);
    }

    [Fact]
    public void LocalDayRangeToUtc_UsesTwentyFiveHourRangeOnFallBackDay()
    {
        var range = _service.LocalDayRangeToUtc(new DateTime(2026, 10, 25), _dublin);

        AssertUtc(2026, 10, 24, 23, 0, range.StartUtc);
        AssertUtc(2026, 10, 26, 0, 0, range.EndUtc);
        Assert.Equal(TimeSpan.FromHours(25), range.EndUtc - range.StartUtc);
    }

    [Fact]
    public void ToUtcFromLocal_ShiftForwardResolvesInvalidSpringForwardTime()
    {
        var invalidLocal = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Unspecified);

        Assert.True(_service.IsInvalidLocalTime(invalidLocal, _dublin));

        var utc = _service.ToUtcFromLocal(
            invalidLocal,
            _dublin,
            InvalidLocalTimeResolution.ShiftForward);

        AssertUtc(2026, 3, 29, 1, 0, utc);
    }

    [Fact]
    public void ToUtcFromLocal_ShiftBackwardResolvesInvalidSpringForwardTime()
    {
        var invalidLocal = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Unspecified);

        var utc = _service.ToUtcFromLocal(
            invalidLocal,
            _dublin,
            InvalidLocalTimeResolution.ShiftBackward);

        AssertUtc(2026, 3, 29, 0, 59, utc);
    }

    [Fact]
    public void ToUtcFromLocal_CanRejectInvalidSpringForwardTime()
    {
        var invalidLocal = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Unspecified);

        Assert.Throws<ArgumentException>(() =>
            _service.ToUtcFromLocal(invalidLocal, _dublin, InvalidLocalTimeResolution.Throw));
    }

    [Fact]
    public void ToUtcFromLocal_DistinguishesAmbiguousFallBackTime()
    {
        var ambiguousLocal = new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Unspecified);

        Assert.True(_service.IsAmbiguousLocalTime(ambiguousLocal, _dublin));

        var earlier = _service.ToUtcFromLocal(
            ambiguousLocal,
            _dublin,
            ambiguousResolution: AmbiguousLocalTimeResolution.EarlierInstant);

        var later = _service.ToUtcFromLocal(
            ambiguousLocal,
            _dublin,
            ambiguousResolution: AmbiguousLocalTimeResolution.LaterInstant);

        AssertUtc(2026, 10, 25, 0, 30, earlier);
        AssertUtc(2026, 10, 25, 1, 30, later);
    }

    [Fact]
    public void ToUtcFromLocal_CanRejectAmbiguousFallBackTime()
    {
        var ambiguousLocal = new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Unspecified);

        Assert.Throws<ArgumentException>(() =>
            _service.ToUtcFromLocal(
                ambiguousLocal,
                _dublin,
                ambiguousResolution: AmbiguousLocalTimeResolution.Throw));
    }

    [Fact]
    public void ToLocal_ConvertsUtcInstantAcrossSpringForwardBoundary()
    {
        var utc = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc);

        var local = _service.ToLocal(utc, _dublin);

        Assert.Equal(new DateTime(2026, 3, 29, 2, 30, 0), local);
        Assert.NotEqual(DateTimeKind.Utc, local.Kind);
    }

    [Fact]
    public void WallClockScheduleTime_ConvertsAmbiguousScheduleUsingDefaultEarlierInstant()
    {
        var localSchedule = new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Unspecified);
        var expectedUtc = new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc);

        var actualMilliseconds = WallClockScheduleTime.ToUnixTimeMilliseconds(
            localSchedule,
            new FixedZoneTimeZoneService(_dublin));

        Assert.Equal(new DateTimeOffset(expectedUtc).ToUnixTimeMilliseconds(), actualMilliseconds);
    }

    [Fact]
    public void WallClockScheduleTime_CombineProducesUnspecifiedWallClockValue()
    {
        var combined = WallClockScheduleTime.Combine(
            new DateTime(2026, 10, 25, 12, 45, 0, DateTimeKind.Local),
            new TimeSpan(1, 30, 0));

        Assert.Equal(new DateTime(2026, 10, 25, 1, 30, 0), combined);
        Assert.Equal(DateTimeKind.Unspecified, combined.Kind);
    }

    private static void AssertUtc(int year, int month, int day, int hour, int minute, DateTime actual)
    {
        Assert.Equal(DateTimeKind.Utc, actual.Kind);
        Assert.Equal(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc), actual);
    }

}
