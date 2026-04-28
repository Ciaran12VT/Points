using Points.Services.Time;
using Xunit;

namespace Points.Tests.Time;

public sealed class TimeSerializationAndDisplayTests
{
    private readonly TimeZoneService _service = new();
    private readonly TimeZoneInfo _dublin = TestTimeZones.Dublin;

    [Fact]
    public void StrictUtcInstantSerialization_RequiresUtcKindAndRoundTrips()
    {
        var utc = new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc);

        var serialized = StrictTimeSerializer.SerializeUtcInstant(utc);
        var parsed = StrictTimeSerializer.ParseUtcInstant(serialized);

        Assert.Equal(utc, parsed);
        Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        Assert.Throws<ArgumentException>(() =>
            StrictTimeSerializer.SerializeUtcInstant(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void StrictLocalDateTimeSerialization_RejectsUtcOrOffsetValues()
    {
        var local = new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Unspecified);

        var serialized = StrictTimeSerializer.SerializeLocalDateTime(local);
        var parsed = StrictTimeSerializer.ParseLocalDateTime(serialized);

        Assert.Equal(local, parsed);
        Assert.Equal(DateTimeKind.Unspecified, parsed.Kind);
        Assert.Throws<ArgumentException>(() => StrictTimeSerializer.SerializeLocalDateTime(DateTime.SpecifyKind(local, DateTimeKind.Utc)));
        Assert.Throws<FormatException>(() => StrictTimeSerializer.ParseLocalDateTime("2026-10-25T01:30:00+01:00"));
    }

    [Fact]
    public void LegacyInstantReader_AssumesUnspecifiedLegacyValuesAreLocalWallClock()
    {
        var result = LegacyTimeReader.ReadInstantUtc(
            "2026-10-25 01:30:00",
            _service,
            _dublin,
            ambiguousResolution: AmbiguousLocalTimeResolution.LaterInstant);

        Assert.Equal(LegacyInstantReadKind.LegacyUnspecifiedLocal, result.Kind);
        Assert.True(result.WasAmbiguousLocalTime);
        Assert.Equal(_dublin.Id, result.TimeZoneId);
        Assert.Equal(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc), result.UtcInstant);
    }

    [Fact]
    public void LegacyInstantReader_PreservesExplicitOffsetsAsInstants()
    {
        var result = LegacyTimeReader.ReadInstantUtc("2026-10-25T01:30:00+01:00", _service, _dublin);

        Assert.Equal(LegacyInstantReadKind.ExplicitOffset, result.Kind);
        Assert.False(result.WasAmbiguousLocalTime);
        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), result.UtcInstant);
    }

    [Fact]
    public void LegacyLocalReader_IgnoresOffsetsForWallClockFields()
    {
        var result = LegacyTimeReader.ReadLocalDateTime("2026-10-25T01:30:00+01:00");

        Assert.True(result.IgnoredOffset);
        Assert.Equal(LegacyLocalDateTimeReadKind.WallClockWithIgnoredOffset, result.Kind);
        Assert.Equal(new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Unspecified), result.LocalDateTime);
    }

    [Fact]
    public void TimeDisplayFormatter_FormatsUtcInstantsInRequestedLocalZone()
    {
        var utc = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc);

        var text = TimeDisplayFormatter.FormatInstant(
            utc,
            "yyyy-MM-dd HH:mm",
            new FixedZoneTimeZoneService(_dublin));

        Assert.Equal("2026-03-29 02:30", text);
    }

    [Fact]
    public void TimeDisplayFormatter_PreservesWallClockLocalValues()
    {
        var wallClock = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Unspecified);

        var text = TimeDisplayFormatter.FormatLocal(wallClock, "yyyy-MM-dd HH:mm");

        Assert.Equal("2026-03-29 01:30", text);
    }

}
