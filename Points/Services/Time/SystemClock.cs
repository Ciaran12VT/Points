namespace Points.Services.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => DateTime.Now;

    public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow;
}
