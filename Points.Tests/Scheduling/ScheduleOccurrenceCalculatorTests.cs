using Points.Models;
using Points.Services.Backup;
using Points.Services.Scheduling;
using Xunit;

namespace Points.Tests.Scheduling;

public sealed class ScheduleOccurrenceCalculatorTests
{
    [Fact]
    public void GetNextOccurrence_UsesSharedScheduleContract()
    {
        var schedule = new ScheduledBackupSchedule
        {
            FrequencyType = FrequencyType.EveryDays,
            FrequencyValue = 2,
            FromDateTime = Local(2026, 5, 1, 2),
            IsEnabled = true
        };

        var next = CardScheduleOccurrenceCalculator.GetNextOccurrence(
            schedule,
            Local(2026, 5, 2, 12));

        Assert.Equal(Local(2026, 5, 3, 2), next);
    }

    [Fact]
    public void GetNextOccurrence_DisabledSharedSchedule_ReturnsNull()
    {
        var schedule = new ScheduledBackupSchedule
        {
            FrequencyType = FrequencyType.EveryDays,
            FrequencyValue = 1,
            FromDateTime = Local(2026, 5, 1, 2),
            IsEnabled = false
        };

        var next = CardScheduleOccurrenceCalculator.GetNextOccurrence(
            schedule,
            Local(2026, 5, 1, 1));

        Assert.Null(next);
    }

    private static DateTime Local(int year, int month, int day, int hour)
    {
        return new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
    }
}
