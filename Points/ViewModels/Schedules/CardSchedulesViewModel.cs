using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Time;
using Points.Views.Schedules;

namespace Points.ViewModels.Schedules;

public sealed class CardSchedulesViewModel
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly IClock _clock;
    private readonly long _cardId;
    private readonly ObservableCollection<CardSchedule> _schedules;
    private readonly Action? _onChanged;

    public ObservableCollection<ScheduleListItem> Items { get; } = new();

    public Command AddScheduleCommand { get; }
    public Command<ScheduleListItem> EditScheduleCommand { get; }
    public Command<ScheduleListItem> DeleteScheduleCommand { get; }
    public Command DoneCommand { get; }

    public CardSchedulesViewModel(
        long cardId,
        ObservableCollection<CardSchedule> schedules,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock,
        Action? onChanged = null)
    {
        _cardId = cardId;
        _schedules = schedules ?? throw new ArgumentNullException(nameof(schedules));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _onChanged = onChanged;

        AddScheduleCommand = new Command(async () => await AddScheduleAsync());
        EditScheduleCommand = new Command<ScheduleListItem>(async item => await EditScheduleAsync(item));
        DeleteScheduleCommand = new Command<ScheduleListItem>(async item => await DeleteScheduleAsync(item));
        DoneCommand = new Command(async () => await _navigation.PopAsync());

        RebuildItemsFromSchedules();
        _schedules.CollectionChanged += (_, __) => RebuildItemsFromSchedules();
    }

    private void RebuildItemsFromSchedules()
    {
        Items.Clear();
        foreach (var schedule in _schedules)
            Items.Add(ScheduleListItem.FromSchedule(schedule));
    }

    private async Task AddScheduleAsync()
    {
        var draft = new CardSchedule
        {
            ScheduleId = 0,
            CardId = _cardId,
            FrequencyType = FrequencyType.Once,
            FrequencyValue = 0,
            FromDateTime = _clock.LocalNow,
            ToDateTime = null,
            IsEnabled = true,
            Note = ""
        };

        await OpenEditorAsync(draft);
    }

    private async Task EditScheduleAsync(ScheduleListItem? item)
    {
        if (item == null)
            return;

        await OpenEditorAsync(item.Schedule.Clone());
    }

    private async Task DeleteScheduleAsync(ScheduleListItem? item)
    {
        if (item == null)
            return;

        var ok = await _dialogs.DisplayAlertAsync("Delete schedule?", item.Summary, "Delete", "Cancel");
        if (!ok)
            return;

        var toRemove = _schedules.FirstOrDefault(s => s.ScheduleId == item.Schedule.ScheduleId && s.ScheduleId > 0)
                       ?? _schedules.FirstOrDefault(s => ReferenceEquals(s, item.Schedule))
                       ?? _schedules.FirstOrDefault(s =>
                           s.ScheduleId == 0 &&
                           item.Schedule.ScheduleId == 0 &&
                           s.FrequencyType == item.Schedule.FrequencyType &&
                           s.FromDateTime == item.Schedule.FromDateTime);

        if (toRemove is not null)
            _schedules.Remove(toRemove);

        _onChanged?.Invoke();
    }

    private async Task OpenEditorAsync(CardSchedule schedule)
    {
        async Task OnSaved(IScheduleModel saved)
        {
            if (saved is not CardSchedule savedCardSchedule)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var existing = savedCardSchedule.ScheduleId > 0
                    ? _schedules.FirstOrDefault(s => s.ScheduleId == savedCardSchedule.ScheduleId)
                    : null;

                if (existing is null && savedCardSchedule.ScheduleId == 0)
                {
                    existing = _schedules.FirstOrDefault(s =>
                        s.ScheduleId == 0 &&
                        s.FrequencyType == saved.FrequencyType &&
                        s.FromDateTime == saved.FromDateTime);
                }

                if (existing is null)
                {
                    _schedules.Add(savedCardSchedule);
                }
                else
                {
                    existing.FrequencyType = savedCardSchedule.FrequencyType;
                    existing.FrequencyValue = savedCardSchedule.FrequencyValue;
                    existing.FromDateTime = savedCardSchedule.FromDateTime;
                    existing.ToDateTime = savedCardSchedule.ToDateTime;
                    existing.IsEnabled = savedCardSchedule.IsEnabled;
                    existing.Note = savedCardSchedule.Note;
                    existing.ScheduleId = savedCardSchedule.ScheduleId;
                    existing.CardId = savedCardSchedule.CardId;
                }

                RebuildItemsFromSchedules();
            });

            _onChanged?.Invoke();
        }

        await _navigation.PushModalAsync(new ScheduleEditPage(schedule, OnSaved, _navigation, _clock));
    }
}

public sealed class ScheduleListItem
{
    public CardSchedule Schedule { get; }
    public string Summary { get; }
    public string RangeText { get; }

    private ScheduleListItem(CardSchedule schedule, string summary, string rangeText)
    {
        Schedule = schedule;
        Summary = summary;
        RangeText = rangeText;
    }

    public static ScheduleListItem FromSchedule(CardSchedule schedule)
    {
        var summary = ScheduleFormatter.ToSummary(schedule);
        var range = ScheduleFormatter.ToRangeText(schedule);
        return new ScheduleListItem(schedule, summary, range);
    }
}

internal static class ScheduleFormatter
{
    public static string ToSummary(CardSchedule schedule)
    {
        var time = schedule.FromDateTime.ToString("HH:mm");
        var enabled = schedule.IsEnabled ? "" : " (disabled)";

        var core = schedule.FrequencyType switch
        {
            FrequencyType.Once => $"Once at {time}",
            FrequencyType.EveryDays => $"Every {Math.Max(1, schedule.FrequencyValue)} day(s) at {time}",
            FrequencyType.EveryWeekday => $"Every weekday at {time}",
            FrequencyType.EveryMonday => $"Every Monday at {time}",
            FrequencyType.EveryTuesday => $"Every Tuesday at {time}",
            FrequencyType.EveryWednesday => $"Every Wednesday at {time}",
            FrequencyType.EveryThursday => $"Every Thursday at {time}",
            FrequencyType.EveryFriday => $"Every Friday at {time}",
            FrequencyType.EverySaturday => $"Every Saturday at {time}",
            FrequencyType.EverySunday => $"Every Sunday at {time}",
            FrequencyType.EveryWeeks => $"Every week at {time}",
            FrequencyType.EveryMonths => $"Every month at {time}",
            FrequencyType.EveryYears => $"Every year at {time}",
            _ => schedule.FrequencyType.ToString()
        };

        if (!string.IsNullOrWhiteSpace(schedule.Note))
            return $"{core}{enabled} - {schedule.Note}";

        return $"{core}{enabled}";
    }

    public static string ToRangeText(CardSchedule schedule)
    {
        var from = schedule.FromDateTime.ToString("yyyy-MM-dd");
        var to = schedule.ToDateTime.HasValue ? schedule.ToDateTime.Value.ToString("yyyy-MM-dd") : "Never";
        return $"From: {from}  -  Ends: {to}";
    }
}
