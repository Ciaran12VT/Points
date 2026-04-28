namespace Points.Services.Time;

public sealed class TimeZoneService : ITimeZoneService
{
    private const int MaxInvalidTimeAdjustmentMinutes = 48 * 60;

    public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    public DateTime ToLocal(DateTime utcInstant)
    {
        return ToLocal(utcInstant, LocalTimeZone);
    }

    public DateTime ToLocal(DateTime utcInstant, TimeZoneInfo timeZone)
    {
        if (timeZone == null) throw new ArgumentNullException(nameof(timeZone));

        var utc = StrictTimeSerializer.RequireUtcInstant(utcInstant, nameof(utcInstant));
        return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
    }

    public DateTime ToUtcFromLocal(
        DateTime localDateTime,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        var zone = timeZone ?? LocalTimeZone;
        var local = AsUnspecifiedLocal(localDateTime);

        local = ResolveInvalidLocalTime(local, zone, invalidResolution);

        if (zone.IsAmbiguousTime(local))
        {
            return ResolveAmbiguousLocalTime(local, zone, ambiguousResolution);
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
    }

    public UtcDateTimeRange LocalRangeToUtc(
        DateTime localStartInclusive,
        DateTime localEndExclusive,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        if (localEndExclusive < localStartInclusive)
            throw new ArgumentException("Range end must be greater than or equal to range start.", nameof(localEndExclusive));

        var zone = timeZone ?? LocalTimeZone;
        var startUtc = ToUtcFromLocal(localStartInclusive, zone, invalidResolution, ambiguousResolution);
        var endUtc = ToUtcFromLocal(localEndExclusive, zone, invalidResolution, ambiguousResolution);
        return new UtcDateTimeRange(startUtc, endUtc);
    }

    public UtcDateTimeRange LocalDayRangeToUtc(
        DateTime localDate,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        var start = localDate.Date;
        return LocalRangeToUtc(start, start.AddDays(1), timeZone, invalidResolution, ambiguousResolution);
    }

    public string SerializeUtc(DateTime utcInstant)
    {
        return StrictTimeSerializer.SerializeUtcInstant(utcInstant);
    }

    public DateTime ParseUtc(string value)
    {
        return StrictTimeSerializer.ParseUtcInstant(value);
    }

    public bool TryParseUtc(string? value, out DateTime utcInstant)
    {
        return StrictTimeSerializer.TryParseUtcInstant(value, out utcInstant);
    }

    public bool IsInvalidLocalTime(DateTime localDateTime, TimeZoneInfo? timeZone = null)
    {
        var zone = timeZone ?? LocalTimeZone;
        return zone.IsInvalidTime(AsUnspecifiedLocal(localDateTime));
    }

    public bool IsAmbiguousLocalTime(DateTime localDateTime, TimeZoneInfo? timeZone = null)
    {
        var zone = timeZone ?? LocalTimeZone;
        return zone.IsAmbiguousTime(AsUnspecifiedLocal(localDateTime));
    }

    private static DateTime AsUnspecifiedLocal(DateTime localDateTime)
    {
        if (localDateTime.Kind == DateTimeKind.Utc)
            throw new ArgumentException("Expected a local or unspecified wall-clock DateTime.", nameof(localDateTime));

        return DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
    }

    private static DateTime ResolveInvalidLocalTime(
        DateTime localDateTime,
        TimeZoneInfo timeZone,
        InvalidLocalTimeResolution resolution)
    {
        if (!timeZone.IsInvalidTime(localDateTime))
            return localDateTime;

        return resolution switch
        {
            InvalidLocalTimeResolution.Throw => throw new ArgumentException(
                $"The local time {localDateTime:O} is invalid in time zone '{timeZone.Id}'.",
                nameof(localDateTime)),
            InvalidLocalTimeResolution.ShiftBackward => ShiftInvalidTime(localDateTime, timeZone, -1),
            _ => ShiftInvalidTime(localDateTime, timeZone, 1)
        };
    }

    private static DateTime ShiftInvalidTime(DateTime localDateTime, TimeZoneInfo timeZone, int direction)
    {
        var cursor = localDateTime;

        for (var i = 0; i < MaxInvalidTimeAdjustmentMinutes; i++)
        {
            cursor = cursor.AddMinutes(direction);
            if (!timeZone.IsInvalidTime(cursor))
                return cursor;
        }

        throw new InvalidOperationException(
            $"Could not resolve invalid local time {localDateTime:O} in time zone '{timeZone.Id}'.");
    }

    private static DateTime ResolveAmbiguousLocalTime(
        DateTime localDateTime,
        TimeZoneInfo timeZone,
        AmbiguousLocalTimeResolution resolution)
    {
        if (resolution == AmbiguousLocalTimeResolution.Throw)
        {
            throw new ArgumentException(
                $"The local time {localDateTime:O} is ambiguous in time zone '{timeZone.Id}'.",
                nameof(localDateTime));
        }

        var offsets = timeZone.GetAmbiguousTimeOffsets(localDateTime);
        var selectedOffset = resolution == AmbiguousLocalTimeResolution.EarlierInstant
            ? offsets.Max()
            : offsets.Min();

        return new DateTimeOffset(localDateTime, selectedOffset).UtcDateTime;
    }

}
