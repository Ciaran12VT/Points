using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Points.Models;

namespace Points.Views.Schedules;

public partial class CardSchedulesPage : ContentPage
{
    private readonly long _cardId;
    private readonly ObservableCollection<CardSchedule> _schedules;

    // Optional callback so caller (details page) can refresh its summary label
    private readonly Action? _onChanged;

    private readonly ObservableCollection<ScheduleListItem> _items = new();

    public CardSchedulesPage(
        long cardId,
        ObservableCollection<CardSchedule> schedules,
        Action? onChanged = null)
    {
        InitializeComponent();

        _cardId = cardId;
        _schedules = schedules;
        _onChanged = onChanged;

        SchedulesView.ItemsSource = _items;

        // Load initial view-model items from the model collection
        RebuildItemsFromSchedules();

        // Keep the UI list in sync if schedules change externally
        _schedules.CollectionChanged += (_, __) => RebuildItemsFromSchedules();
    }

    private void RebuildItemsFromSchedules()
    {
        _items.Clear();
        foreach (var s in _schedules)
            _items.Add(ScheduleListItem.FromSchedule(s));
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        var draft = new CardSchedule
        {
            ScheduleId = 0, // not persisted yet
            CardId = _cardId,
            FrequencyType = FrequencyType.Once,
            FrequencyValue = 0,
            FromDateTime = DateTime.Now,
            ToDateTime = null,
            IsEnabled = true,
            Note = ""
        };

        await OpenEditorAsync(draft, isNew: true);
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is not ScheduleListItem item)
            return;

        await OpenEditorAsync(item.Schedule.Clone(), isNew: false);
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (((Button)sender).CommandParameter is not ScheduleListItem item)
            return;

        await DeleteItemAsync(item);
    }

    private async void OnEditInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItem si && si.CommandParameter is ScheduleListItem item)
            await OpenEditorAsync(item.Schedule.Clone(), isNew: false);
    }

    private async void OnDeleteInvoked(object sender, EventArgs e)
    {
        if (sender is SwipeItem si && si.CommandParameter is ScheduleListItem item)
            await DeleteItemAsync(item);
    }

    private async Task DeleteItemAsync(ScheduleListItem item)
    {
        var ok = await DisplayAlert("Delete schedule?", item.Summary, "Delete", "Cancel");
        if (!ok) return;

        // Remove from model collection (source of truth)
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

    private async Task OpenEditorAsync(CardSchedule schedule, bool isNew)
    {
        async Task OnSaved(IScheduleModel saved)
        {
            if(saved is not CardSchedule savedCardSchedule) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Update model collection (source of truth)
                var existing = savedCardSchedule.ScheduleId > 0
                    ? _schedules.FirstOrDefault(s => s.ScheduleId == savedCardSchedule.ScheduleId)
                    : null;

                if (existing is null && savedCardSchedule.ScheduleId == 0)
                {
                    // heuristic for non-persisted schedules
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
                    // Copy fields into existing so bindings stay stable
                    existing.FrequencyType = savedCardSchedule.FrequencyType;
                    existing.FrequencyValue = savedCardSchedule.FrequencyValue;
                    existing.FromDateTime = savedCardSchedule.FromDateTime;
                    existing.ToDateTime = savedCardSchedule.ToDateTime;
                    existing.IsEnabled = savedCardSchedule.IsEnabled;
                    existing.Note = savedCardSchedule.Note;

                    // If you later assign ScheduleId after DB insert:
                    existing.ScheduleId = savedCardSchedule.ScheduleId;
                    existing.CardId = savedCardSchedule.CardId;
                }

                // Refresh the UI list wrapper
                RebuildItemsFromSchedules();
            });

            _onChanged?.Invoke();
        }

        await Navigation.PushModalAsync(new ScheduleEditPage(schedule, OnSaved));
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        await Shell.Current.Navigation.PopAsync();
    }

    // Display model for the list
    private sealed class ScheduleListItem
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

        public static ScheduleListItem FromSchedule(CardSchedule s)
        {
            var summary = ScheduleFormatter.ToSummary(s);
            var range = ScheduleFormatter.ToRangeText(s);
            return new ScheduleListItem(s, summary, range);
        }
    }

    private static class ScheduleFormatter
    {
        public static string ToSummary(CardSchedule s)
        {
            var t = s.FromDateTime.ToString("HH:mm");
            var enabled = s.IsEnabled ? "" : " (disabled)";

            var core = s.FrequencyType switch
            {
                FrequencyType.Once => $"Once at {t}",
                FrequencyType.EveryDays => $"Every {Math.Max(1, s.FrequencyValue)} day(s) at {t}",
                FrequencyType.EveryWeekday => $"Every weekday at {t}",
                FrequencyType.EveryMonday => $"Every Monday at {t}",
                FrequencyType.EveryTuesday => $"Every Tuesday at {t}",
                FrequencyType.EveryWednesday => $"Every Wednesday at {t}",
                FrequencyType.EveryThursday => $"Every Thursday at {t}",
                FrequencyType.EveryFriday => $"Every Friday at {t}",
                FrequencyType.EverySaturday => $"Every Saturday at {t}",
                FrequencyType.EverySunday => $"Every Sunday at {t}",
                FrequencyType.EveryWeeks => $"Every week at {t}",
                FrequencyType.EveryMonths => $"Every month at {t}",
                FrequencyType.EveryYears => $"Every year at {t}",
                _ => s.FrequencyType.ToString()
            };

            if (!string.IsNullOrWhiteSpace(s.Note))
                return $"{core}{enabled} — {s.Note}";

            return $"{core}{enabled}";
        }

        public static string ToRangeText(CardSchedule s)
        {
            var from = s.FromDateTime.ToString("yyyy-MM-dd");
            var to = s.ToDateTime.HasValue ? s.ToDateTime.Value.ToString("yyyy-MM-dd") : "Never";
            return $"From: {from}  ·  Ends: {to}";
        }
    }
}
