using Points.Services.Time;

namespace Points.Services.Scheduling;

public static class WallClockScheduleTime
{
    public static DateTime NormalizeLocal(DateTime value)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        var local = value.Kind == DateTimeKind.Utc
            ? value.ToLocalTime()
            : value;

        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public static DateTime? NormalizeLocal(DateTime? value)
    {
        return value.HasValue ? NormalizeLocal(value.Value) : null;
    }

    public static DateTime Combine(DateTime localDate, TimeSpan localTime)
    {
        return DateTime.SpecifyKind(localDate.Date.Add(localTime), DateTimeKind.Unspecified);
    }

    public static long ToUnixTimeMilliseconds(DateTime localWallClock, ITimeZoneService timeZoneService)
    {
        if (timeZoneService == null) throw new ArgumentNullException(nameof(timeZoneService));

        var utcInstant = timeZoneService.ToUtcFromLocal(NormalizeLocal(localWallClock));
        return new DateTimeOffset(utcInstant).ToUnixTimeMilliseconds();
    }
}
