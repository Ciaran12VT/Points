namespace Points.Services.Time;

public interface IClock
{
    DateTime UtcNow { get; }

    DateTime LocalNow { get; }

    DateTimeOffset UtcNowOffset { get; }
}
