namespace Points.Services.Time;

public interface ITimeZoneService
{
    TimeZoneInfo LocalTimeZone { get; }

    DateTime ToLocal(DateTime utcInstant);

    DateTime ToLocal(DateTime utcInstant, TimeZoneInfo timeZone);

    DateTime ToUtcFromLocal(
        DateTime localDateTime,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant);

    UtcDateTimeRange LocalRangeToUtc(
        DateTime localStartInclusive,
        DateTime localEndExclusive,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant);

    UtcDateTimeRange LocalDayRangeToUtc(
        DateTime localDate,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant);

    string SerializeUtc(DateTime utcInstant);

    DateTime ParseUtc(string value);

    bool TryParseUtc(string? value, out DateTime utcInstant);

    bool IsInvalidLocalTime(DateTime localDateTime, TimeZoneInfo? timeZone = null);

    bool IsAmbiguousLocalTime(DateTime localDateTime, TimeZoneInfo? timeZone = null);
}
