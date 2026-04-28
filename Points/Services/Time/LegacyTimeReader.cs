using System.Globalization;

namespace Points.Services.Time;

public enum LegacyInstantReadKind
{
    ExplicitUtc = 0,
    ExplicitOffset = 1,
    LegacyUnspecifiedLocal = 2
}

public enum LegacyLocalDateTimeReadKind
{
    WallClockUnspecified = 0,
    WallClockWithIgnoredOffset = 1
}

public sealed class LegacyInstantReadResult
{
    public LegacyInstantReadResult(
        DateTime utcInstant,
        LegacyInstantReadKind kind,
        string? timeZoneId,
        bool wasInvalidLocalTime,
        bool wasAmbiguousLocalTime)
    {
        UtcInstant = StrictTimeSerializer.RequireUtcInstant(utcInstant, nameof(utcInstant));
        Kind = kind;
        TimeZoneId = timeZoneId;
        WasInvalidLocalTime = wasInvalidLocalTime;
        WasAmbiguousLocalTime = wasAmbiguousLocalTime;
    }

    public DateTime UtcInstant { get; }

    public LegacyInstantReadKind Kind { get; }

    public string? TimeZoneId { get; }

    public bool WasInvalidLocalTime { get; }

    public bool WasAmbiguousLocalTime { get; }

    public bool UsedLegacyLocalAssumption => Kind == LegacyInstantReadKind.LegacyUnspecifiedLocal;
}

public sealed class LegacyLocalDateTimeReadResult
{
    public LegacyLocalDateTimeReadResult(DateTime localDateTime, LegacyLocalDateTimeReadKind kind)
    {
        LocalDateTime = StrictTimeSerializer.RequireWallClockDateTime(localDateTime, nameof(localDateTime));
        Kind = kind;
    }

    public DateTime LocalDateTime { get; }

    public LegacyLocalDateTimeReadKind Kind { get; }

    public bool IgnoredOffset => Kind == LegacyLocalDateTimeReadKind.WallClockWithIgnoredOffset;
}

public static class LegacyTimeReader
{
    private static readonly string[] FlexibleLocalDateTimeFormats =
    {
        StrictTimeSerializer.LocalDateTimeFormat,
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd'T'HH:mm",
        "yyyy-MM-dd HH:mm:ss.fffffff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        StrictTimeSerializer.LocalDateFormat
    };

    public static LegacyInstantReadResult ReadInstantUtc(
        string value,
        ITimeZoneService timeZoneService,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        if (timeZoneService == null) throw new ArgumentNullException(nameof(timeZoneService));

        var trimmed = RequireValue(value);

        if (StrictTimeSerializer.HasExplicitUtcOrOffset(trimmed))
        {
            var dto = DateTimeOffset.Parse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces);
            var kind = trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                ? LegacyInstantReadKind.ExplicitUtc
                : LegacyInstantReadKind.ExplicitOffset;

            return new LegacyInstantReadResult(
                dto.UtcDateTime,
                kind,
                timeZoneId: null,
                wasInvalidLocalTime: false,
                wasAmbiguousLocalTime: false);
        }

        var zone = timeZone ?? timeZoneService.LocalTimeZone;
        var local = ParseLegacyUnspecifiedLocalDateTime(trimmed);
        var wasInvalid = timeZoneService.IsInvalidLocalTime(local, zone);
        var wasAmbiguous = !wasInvalid && timeZoneService.IsAmbiguousLocalTime(local, zone);
        var utc = timeZoneService.ToUtcFromLocal(local, zone, invalidResolution, ambiguousResolution);

        return new LegacyInstantReadResult(
            utc,
            LegacyInstantReadKind.LegacyUnspecifiedLocal,
            zone.Id,
            wasInvalid,
            wasAmbiguous);
    }

    public static bool TryReadInstantUtc(
        string? value,
        ITimeZoneService timeZoneService,
        out LegacyInstantReadResult? result,
        TimeZoneInfo? timeZone = null,
        InvalidLocalTimeResolution invalidResolution = InvalidLocalTimeResolution.ShiftForward,
        AmbiguousLocalTimeResolution ambiguousResolution = AmbiguousLocalTimeResolution.EarlierInstant)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            result = ReadInstantUtc(value, timeZoneService, timeZone, invalidResolution, ambiguousResolution);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static LegacyLocalDateTimeReadResult ReadLocalDateTime(string value)
    {
        var trimmed = RequireValue(value);

        if (StrictTimeSerializer.HasExplicitUtcOrOffset(trimmed))
        {
            var dto = DateTimeOffset.Parse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces);
            var wallClock = DateTime.SpecifyKind(dto.DateTime, DateTimeKind.Unspecified);
            return new LegacyLocalDateTimeReadResult(
                wallClock,
                LegacyLocalDateTimeReadKind.WallClockWithIgnoredOffset);
        }

        return new LegacyLocalDateTimeReadResult(
            ParseLegacyUnspecifiedLocalDateTime(trimmed),
            LegacyLocalDateTimeReadKind.WallClockUnspecified);
    }

    public static bool TryReadLocalDateTime(string? value, out LegacyLocalDateTimeReadResult? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            result = ReadLocalDateTime(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string RequireValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A legacy time value is required.", nameof(value));

        return value.Trim();
    }

    private static DateTime ParseLegacyUnspecifiedLocalDateTime(string value)
    {
        if (StrictTimeSerializer.TryParseLocalDateTime(value, out var strictLocal))
            return strictLocal;

        if (DateTime.TryParseExact(
                value,
                FlexibleLocalDateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var exactParsed))
        {
            return DateTime.SpecifyKind(exactParsed, DateTimeKind.Unspecified);
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var invariantParsed))
        {
            return DateTime.SpecifyKind(invariantParsed, DateTimeKind.Unspecified);
        }

        if (DateTime.TryParse(
                value,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var currentCultureParsed))
        {
            return DateTime.SpecifyKind(currentCultureParsed, DateTimeKind.Unspecified);
        }

        throw new FormatException($"Could not parse legacy local date-time value '{value}'.");
    }
}
