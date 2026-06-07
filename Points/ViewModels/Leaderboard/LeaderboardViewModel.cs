using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Leaderboard;

public sealed partial class LeaderboardViewModel : INotifyPropertyChanged
{
    private readonly IClock _clock;
    private readonly ITimeZoneService _timeZoneService;
    private readonly LeaderboardController _leaderboardController;

    private bool _isLeaderboardSelected = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LeaderboardRowModel> Rows => _leaderboardController.Rows;

    public LeaderboardRowModel? DeadAirRow => _leaderboardController.DeadAirRow;

    public ICommand SelectLeaderboardTabCommand { get; }
    public ICommand SelectPlannerTabCommand { get; }
    public ICommand SortByHoursCommand { get; }
    public ICommand SortByPercentOfTrackedCommand { get; }
    public ICommand SortByPercentOfDayCommand { get; }
    public ICommand SortByPointsCommand { get; }

    public LeaderboardViewModel(
        ICardReadService cardReader,
        IPlannerService plannerService,
        IClock clock,
        ITimeZoneService timeZoneService)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _leaderboardController = new LeaderboardController(
            cardReader ?? throw new ArgumentNullException(nameof(cardReader)),
            () => LocalNow,
            GetClippedActiveTime,
            OnPropertyChanged);
        _plannerController = new LeaderboardPlannerController(
            plannerService ?? throw new ArgumentNullException(nameof(plannerService)),
            () => LocalNow,
            ToLocalWallClock,
            OnPropertyChanged);

        SelectLeaderboardTabCommand = new Command(() => IsLeaderboardSelected = true);
        SelectPlannerTabCommand = new Command(async () =>
        {
            IsLeaderboardSelected = false;
            await LoadPlannerAsync();
        });

        SortByHoursCommand = new Command(() => _leaderboardController.SortBy(LeaderboardSortColumn.TotalHours));
        SortByPercentOfTrackedCommand = new Command(() => _leaderboardController.SortBy(LeaderboardSortColumn.PercentOfTrackedTime));
        SortByPercentOfDayCommand = new Command(() => _leaderboardController.SortBy(LeaderboardSortColumn.PercentOfDay));
        SortByPointsCommand = new Command(() => _leaderboardController.SortBy(LeaderboardSortColumn.Points));

        InitializePlannerCommands();
    }

    public bool IsBusy => _leaderboardController.IsBusy;

    public string ErrorMessage => _leaderboardController.ErrorMessage;

    public bool HasError => _leaderboardController.HasError;

    public bool HasNoRows => _leaderboardController.HasNoRows;

    public bool IsDeadAirVisible => _leaderboardController.IsDeadAirVisible;

    public bool IsLeaderboardSelected
    {
        get => _isLeaderboardSelected;
        private set
        {
            if (_isLeaderboardSelected == value) return;
            _isLeaderboardSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPlannerSelected));
            OnPropertyChanged(nameof(LeaderboardTabBackground));
            OnPropertyChanged(nameof(PlannerTabBackground));
            OnPropertyChanged(nameof(LeaderboardTabTextColor));
            OnPropertyChanged(nameof(PlannerTabTextColor));
        }
    }

    public bool IsPlannerSelected => !IsLeaderboardSelected;

    public Color LeaderboardTabBackground => IsLeaderboardSelected ? Colors.Green : Colors.Black;
    public Color PlannerTabBackground => IsPlannerSelected ? Colors.Green : Colors.Black;
    public Color LeaderboardTabTextColor => Colors.White;
    public Color PlannerTabTextColor => Colors.White;

    public string HoursHeaderText => _leaderboardController.HoursHeaderText;
    public string PercentOfTrackedHeaderText => _leaderboardController.PercentOfTrackedHeaderText;
    public string PercentOfDayHeaderText => _leaderboardController.PercentOfDayHeaderText;
    public string PointsHeaderText => _leaderboardController.PointsHeaderText;

    public string SummaryText => _leaderboardController.SummaryText;

    public Task RefreshAsync() => _leaderboardController.RefreshAsync();

    private TimeSpan GetClippedActiveTime(
        IActiveCardModel card,
        DateTime start,
        DateTime end,
        DateTime now)
    {
        var startUtc = ToUtcInstant(start);
        var endUtc = ToUtcInstant(end);
        var nowUtc = ToUtcInstant(now);

        if (endUtc <= startUtc) return TimeSpan.Zero;

        var effectiveEndUtc = Min(endUtc, nowUtc);
        if (effectiveEndUtc <= startUtc) return TimeSpan.Zero;

        var totalMinutes = 0d;

        foreach (var period in card.Activity)
        {
            var activityStartUtc = ToUtcInstant(period.StartDate);
            var activityEndUtc = period.EndDate.HasValue
                ? ToUtcInstant(period.EndDate.Value)
                : effectiveEndUtc;

            activityEndUtc = Min(activityEndUtc, effectiveEndUtc);

            var overlapStart = Max(activityStartUtc, startUtc);
            var overlapEnd = Min(activityEndUtc, endUtc);

            if (overlapEnd > overlapStart)
                totalMinutes += (overlapEnd - overlapStart).TotalMinutes;
        }

        return TimeSpan.FromMinutes(totalMinutes);
    }

    private DateTime LocalNow => ToLocalWallClock(_clock.LocalNow);

    private DateTime ToLocalWallClock(DateTime value)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        var local = value.Kind == DateTimeKind.Utc
            ? _timeZoneService.ToLocal(value)
            : value;

        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    private DateTime ToUtcInstant(DateTime value)
    {
        if (value == DateTime.MinValue || value == DateTime.MaxValue)
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return value.Kind == DateTimeKind.Utc
            ? value
            : _timeZoneService.ToUtcFromLocal(value);
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
