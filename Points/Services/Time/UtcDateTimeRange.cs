namespace Points.Services.Time;

public readonly record struct UtcDateTimeRange
{
    public UtcDateTimeRange(DateTime startUtc, DateTime endUtc)
    {
        if (startUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Range start must be UTC.", nameof(startUtc));

        if (endUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Range end must be UTC.", nameof(endUtc));

        if (endUtc < startUtc)
            throw new ArgumentException("Range end must be greater than or equal to range start.", nameof(endUtc));

        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    public DateTime StartUtc { get; }

    public DateTime EndUtc { get; }
}
