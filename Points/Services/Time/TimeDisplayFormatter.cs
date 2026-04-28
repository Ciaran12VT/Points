using System.Globalization;

namespace Points.Services.Time;

public static class TimeDisplayFormatter
{
    public static DateTime ToLocalInstant(DateTime value, ITimeZoneService? timeZoneService = null)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        if (value.Kind != DateTimeKind.Utc)
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        var local = timeZoneService is null
            ? TimeZoneInfo.ConvertTimeFromUtc(value, TimeZoneInfo.Local)
            : timeZoneService.ToLocal(value);

        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public static DateTime NormalizeLocal(DateTime value)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        return StrictTimeSerializer.RequireWallClockDateTime(value);
    }

    public static string FormatInstant(
        DateTime value,
        string format,
        ITimeZoneService? timeZoneService = null,
        IFormatProvider? provider = null)
    {
        return ToLocalInstant(value, timeZoneService)
            .ToString(format, provider ?? CultureInfo.CurrentCulture);
    }

    public static string FormatNullableInstant(
        DateTime? value,
        string format,
        string nullText = "N/A",
        ITimeZoneService? timeZoneService = null,
        IFormatProvider? provider = null)
    {
        return value.HasValue
            ? FormatInstant(value.Value, format, timeZoneService, provider)
            : nullText;
    }

    public static string FormatLocal(
        DateTime value,
        string format,
        IFormatProvider? provider = null)
    {
        return NormalizeLocal(value)
            .ToString(format, provider ?? CultureInfo.CurrentCulture);
    }
}
