using System.Globalization;

namespace Points.Services.Time;

public static class StrictTimeSerializer
{
    public const string UtcInstantFormat = "O";
    public const string LocalDateFormat = "yyyy-MM-dd";
    public const string LocalTimeFormat = "HH:mm:ss";
    public const string LocalDateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff";

    private static readonly string[] LocalDateTimeFormats =
    {
        LocalDateTimeFormat,
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fffffff",
        "yyyy-MM-dd HH:mm:ss"
    };

    public static string SerializeUtcInstant(DateTime utcInstant)
    {
        return RequireUtcInstant(utcInstant).ToString(UtcInstantFormat, CultureInfo.InvariantCulture);
    }

    public static string SerializeUtcInstant(DateTimeOffset instant)
    {
        return instant.ToUniversalTime().ToString(UtcInstantFormat, CultureInfo.InvariantCulture);
    }

    public static string? SerializeNullableUtcInstant(DateTime? utcInstant)
    {
        return utcInstant.HasValue ? SerializeUtcInstant(utcInstant.Value) : null;
    }

    public static string? SerializeNullableUtcInstant(DateTimeOffset? instant)
    {
        return instant.HasValue ? SerializeUtcInstant(instant.Value) : null;
    }

    public static string SerializeUtcInstantFromLocal(
        DateTime localDateTime,
        ITimeZoneService timeZoneService,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        if (timeZoneService == null) throw new ArgumentNullException(nameof(timeZoneService));

        var utc = timeZoneService.ToUtcFromLocal(localDateTime, timeZone, invalidResolution, ambiguousResolution);
        return SerializeUtcInstant(utc);
    }

    public static string? SerializeNullableUtcInstantFromLocal(
        DateTime? localDateTime,
        ITimeZoneService timeZoneService,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        return localDateTime.HasValue
            ? SerializeUtcInstantFromLocal(localDateTime.Value, timeZoneService, timeZone, invalidResolution, ambiguousResolution)
            : null;
    }

    public static DateTime ParseUtcInstant(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("UTC instant text is required.", nameof(value));

        if (!HasExplicitUtcOrOffset(value))
            throw new FormatException("UTC instant text must include a UTC designator or offset.");

        var dto = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces);
        return dto.UtcDateTime;
    }

    public static bool TryParseUtcInstant(string? value, out DateTime utcInstant)
    {
        utcInstant = default;

        if (string.IsNullOrWhiteSpace(value) || !HasExplicitUtcOrOffset(value))
            return false;

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dto))
            return false;

        utcInstant = dto.UtcDateTime;
        return true;
    }

    public static string SerializeLocalDate(DateTime localDate)
    {
        return RequireWallClockDateTime(localDate).Date.ToString(LocalDateFormat, CultureInfo.InvariantCulture);
    }

    public static string SerializeLocalDate(DateOnly localDate)
    {
        return localDate.ToString(LocalDateFormat, CultureInfo.InvariantCulture);
    }

    public static DateTime ParseLocalDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Local date text is required.", nameof(value));

        var parsed = DateTime.ParseExact(
            value.Trim(),
            LocalDateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified);
    }

    public static DateOnly ParseLocalDateOnly(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Local date text is required.", nameof(value));

        return DateOnly.ParseExact(value.Trim(), LocalDateFormat, CultureInfo.InvariantCulture);
    }

    public static bool TryParseLocalDate(string? value, out DateTime localDate)
    {
        localDate = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!DateTime.TryParseExact(
                value.Trim(),
                LocalDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        localDate = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified);
        return true;
    }

    public static string SerializeLocalTime(TimeOnly localTime)
    {
        return localTime.ToString(LocalTimeFormat, CultureInfo.InvariantCulture);
    }

    public static TimeOnly ParseLocalTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Local time text is required.", nameof(value));

        return TimeOnly.ParseExact(value.Trim(), LocalTimeFormat, CultureInfo.InvariantCulture);
    }

    public static bool TryParseLocalTime(string? value, out TimeOnly localTime)
    {
        localTime = default;

        return !string.IsNullOrWhiteSpace(value)
               && TimeOnly.TryParseExact(value.Trim(), LocalTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out localTime);
    }

    public static string SerializeLocalDateTime(DateTime localDateTime)
    {
        return RequireWallClockDateTime(localDateTime).ToString(LocalDateTimeFormat, CultureInfo.InvariantCulture);
    }

    public static string? SerializeNullableLocalDateTime(DateTime? localDateTime)
    {
        return localDateTime.HasValue ? SerializeLocalDateTime(localDateTime.Value) : null;
    }

    public static DateTime ParseLocalDateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Local date-time text is required.", nameof(value));

        if (HasExplicitUtcOrOffset(value))
            throw new FormatException("Local date-time text must not include a UTC designator or offset.");

        var parsed = DateTime.ParseExact(
            value.Trim(),
            LocalDateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);

        return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
    }

    public static bool TryParseLocalDateTime(string? value, out DateTime localDateTime)
    {
        localDateTime = default;

        if (string.IsNullOrWhiteSpace(value) || HasExplicitUtcOrOffset(value))
            return false;

        if (!DateTime.TryParseExact(
                value.Trim(),
                LocalDateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        localDateTime = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        return true;
    }

    public static DateTime RequireUtcInstant(DateTime value, string parameterName = "value")
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("UTC instant values must have DateTimeKind.Utc.", parameterName);

        return value;
    }

    public static DateTime RequireWallClockDateTime(DateTime value, string parameterName = "value")
    {
        if (value.Kind == DateTimeKind.Utc)
            throw new ArgumentException("Wall-clock values must not have DateTimeKind.Utc.", parameterName);

        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    public static bool HasExplicitUtcOrOffset(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            return true;

        var separatorIndex = Math.Max(trimmed.LastIndexOf('T'), trimmed.LastIndexOf(' '));
        if (separatorIndex < 0)
            return false;

        var plusIndex = trimmed.LastIndexOf('+');
        var minusIndex = trimmed.LastIndexOf('-');
        var offsetIndex = Math.Max(plusIndex, minusIndex);

        if (offsetIndex <= separatorIndex)
            return false;

        if (trimmed.Length - offsetIndex != 6)
            return false;

        return trimmed[offsetIndex + 3] == ':'
               && char.IsDigit(trimmed[offsetIndex + 1])
               && char.IsDigit(trimmed[offsetIndex + 2])
               && char.IsDigit(trimmed[offsetIndex + 4])
               && char.IsDigit(trimmed[offsetIndex + 5]);
    }
}
