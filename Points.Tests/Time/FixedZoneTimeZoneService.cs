using Points.Services.Time;

namespace Points.Tests.Time;

internal sealed class FixedZoneTimeZoneService : ITimeZoneService
{
    private readonly TimeZoneService _inner = new();

    public FixedZoneTimeZoneService(TimeZoneInfo localTimeZone)
    {
        LocalTimeZone = localTimeZone;
    }

    public TimeZoneInfo LocalTimeZone { get; }

    public DateTime ToLocal(DateTime utcInstant) => _inner.ToLocal(utcInstant, LocalTimeZone);

    public DateTime ToLocal(DateTime utcInstant, TimeZoneInfo timeZone) => _inner.ToLocal(utcInstant, timeZone);

    public DateTime ToUtcFromLocal(
        DateTime localDateTime,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        return _inner.ToUtcFromLocal(localDateTime, timeZone ?? LocalTimeZone, invalidResolution, ambiguousResolution);
    }

    public UtcDateTimeRange LocalRangeToUtc(
        DateTime localStartInclusive,
        DateTime localEndExclusive,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        return _inner.LocalRangeToUtc(localStartInclusive, localEndExclusive, timeZone ?? LocalTimeZone, invalidResolution, ambiguousResolution);
    }

    public UtcDateTimeRange LocalDayRangeToUtc(
        DateTime localDate,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        return _inner.LocalDayRangeToUtc(localDate, timeZone ?? LocalTimeZone, invalidResolution, ambiguousResolution);
    }

    public string SerializeUtc(DateTime utcInstant) => _inner.SerializeUtc(utcInstant);

    public DateTime ParseUtc(string value) => _inner.ParseUtc(value);

    public bool TryParseUtc(string? value, out DateTime utcInstant) => _inner.TryParseUtc(value, out utcInstant);

    public bool IsInvalidLocalTime(DateTime localDateTime, TimeZoneInfo? timeZone = null)
    {
        return _inner.IsInvalidLocalTime(localDateTime, timeZone ?? LocalTimeZone);
    }

    public bool IsAmbiguousLocalTime(DateTime localDateTime, TimeZoneInfo? timeZone = null)
    {
        return _inner.IsAmbiguousLocalTime(localDateTime, timeZone ?? LocalTimeZone);
    }
}
