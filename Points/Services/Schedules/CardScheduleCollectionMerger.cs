using Points.Models;

namespace Points.Services.Schedules;

internal static class CardScheduleCollectionMerger
{
    public static void ApplySavedSchedule(
        ICollection<CardSchedule> schedules,
        CardSchedule savedSchedule,
        CardSchedule? originalSchedule = null)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        ArgumentNullException.ThrowIfNull(savedSchedule);

        var existing = FindExistingSchedule(schedules, savedSchedule, originalSchedule);

        if (existing is null)
        {
            schedules.Add(savedSchedule);
            return;
        }

        CopyValues(savedSchedule, existing);
    }

    private static CardSchedule? FindExistingSchedule(
        ICollection<CardSchedule> schedules,
        CardSchedule savedSchedule,
        CardSchedule? originalSchedule)
    {
        if (originalSchedule is not null && schedules.Contains(originalSchedule))
            return originalSchedule;

        if (schedules.Contains(savedSchedule))
            return savedSchedule;

        return savedSchedule.ScheduleId > 0
            ? schedules.FirstOrDefault(s => s.ScheduleId == savedSchedule.ScheduleId)
            : null;
    }

    private static void CopyValues(CardSchedule source, CardSchedule target)
    {
        target.FrequencyType = source.FrequencyType;
        target.FrequencyValue = source.FrequencyValue;
        target.FromDateTime = source.FromDateTime;
        target.ToDateTime = source.ToDateTime;
        target.IsEnabled = source.IsEnabled;
        target.Note = source.Note;
        target.ScheduleId = source.ScheduleId;
        target.CardId = source.CardId;
    }
}
