using System.Collections.ObjectModel;
using Points.Models;
using Points.Services.Schedules;
using Xunit;

namespace Points.Tests.Schedules;

public sealed class CardScheduleCollectionMergerTests
{
    [Fact]
    public void ApplySavedSchedule_UpdatesOriginalUnsavedScheduleWhenEditableFieldsChange()
    {
        var original = new CardSchedule
        {
            ScheduleId = 0,
            CardId = 101,
            FrequencyType = FrequencyType.Once,
            FrequencyValue = 0,
            FromDateTime = new DateTime(2026, 5, 13, 9, 0, 0),
            IsEnabled = true,
            Note = "Original"
        };

        var schedules = new ObservableCollection<CardSchedule> { original };
        var edited = original.Clone();
        edited.FrequencyType = FrequencyType.EveryDays;
        edited.FrequencyValue = 3;
        edited.FromDateTime = new DateTime(2026, 5, 14, 10, 30, 0);
        edited.ToDateTime = new DateTime(2026, 5, 30, 18, 0, 0);
        edited.IsEnabled = false;
        edited.Note = "Edited";

        CardScheduleCollectionMerger.ApplySavedSchedule(schedules, edited, original);

        var saved = Assert.Single(schedules);
        Assert.Same(original, saved);
        Assert.Equal(FrequencyType.EveryDays, saved.FrequencyType);
        Assert.Equal(3, saved.FrequencyValue);
        Assert.Equal(new DateTime(2026, 5, 14, 10, 30, 0), saved.FromDateTime);
        Assert.Equal(new DateTime(2026, 5, 30, 18, 0, 0), saved.ToDateTime);
        Assert.False(saved.IsEnabled);
        Assert.Equal("Edited", saved.Note);
    }

    [Fact]
    public void ApplySavedSchedule_AddsNewUnsavedScheduleEvenWhenFieldsMatchExistingUnsavedSchedule()
    {
        var existing = new CardSchedule
        {
            ScheduleId = 0,
            CardId = 101,
            FrequencyType = FrequencyType.Once,
            FromDateTime = new DateTime(2026, 5, 13, 9, 0, 0),
            IsEnabled = true
        };

        var schedules = new ObservableCollection<CardSchedule> { existing };
        var added = existing.Clone();

        CardScheduleCollectionMerger.ApplySavedSchedule(schedules, added);

        Assert.Equal(2, schedules.Count);
        Assert.Same(existing, schedules[0]);
        Assert.Same(added, schedules[1]);
    }
}
