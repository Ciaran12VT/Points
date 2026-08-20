using System.Globalization;
using Points.Services.Time;

namespace Points.Services;

public static class ActiveCardNotificationElapsedFormatter
{
    public static TimeSpan CalculateElapsed(DateTime startedAtUtc, DateTime nowUtc)
    {
        startedAtUtc = StrictTimeSerializer.RequireUtcInstant(startedAtUtc, nameof(startedAtUtc));
        nowUtc = StrictTimeSerializer.RequireUtcInstant(nowUtc, nameof(nowUtc));

        return nowUtc <= startedAtUtc
            ? TimeSpan.Zero
            : nowUtc - startedAtUtc;
    }

    public static string Format(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        var totalHours = (long)elapsed.TotalHours;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}:{1:00}:{2:00}",
            totalHours,
            elapsed.Minutes,
            elapsed.Seconds);
    }
}
