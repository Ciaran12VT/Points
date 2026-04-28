using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using Points.Models;
using Points.Services.Sqlite.Interfaces;

namespace Points.ViewModels;

public sealed class LeaderboardViewModel : INotifyPropertyChanged
{
    private readonly IDbService _db;
    private List<LeaderboardRowModel> _allRows = new();

    private bool _isBusy;
    private string _errorMessage = "";
    private bool _isLeaderboardSelected = true;
    private LeaderboardSortColumn _sortColumn = LeaderboardSortColumn.TotalHours;
    private bool _sortDescending = true;
    private DateTime _refreshedAt = DateTime.Now;
    private double _totalTrackedHours;
    private LeaderboardRowModel? _deadAirRow;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LeaderboardRowModel> Rows { get; } = new();

    public LeaderboardRowModel? DeadAirRow
    {
        get => _deadAirRow;
        private set
        {
            if (_deadAirRow == value) return;
            _deadAirRow = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDeadAirVisible));
            OnPropertyChanged(nameof(HasNoRows));
        }
    }

    public ICommand SelectLeaderboardTabCommand { get; }
    public ICommand SelectPlannerTabCommand { get; }
    public ICommand SortByHoursCommand { get; }
    public ICommand SortByPercentOfTrackedCommand { get; }
    public ICommand SortByPercentOfDayCommand { get; }
    public ICommand SortByPointsCommand { get; }

    public LeaderboardViewModel(IDbService db)
    {
        _db = db;

        SelectLeaderboardTabCommand = new Command(() => IsLeaderboardSelected = true);
        SelectPlannerTabCommand = new Command(() => IsLeaderboardSelected = false);

        SortByHoursCommand = new Command(() => SortBy(LeaderboardSortColumn.TotalHours));
        SortByPercentOfTrackedCommand = new Command(() => SortBy(LeaderboardSortColumn.PercentOfTrackedTime));
        SortByPercentOfDayCommand = new Command(() => SortBy(LeaderboardSortColumn.PercentOfDay));
        SortByPointsCommand = new Command(() => SortBy(LeaderboardSortColumn.Points));
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNoRows));
            OnPropertyChanged(nameof(IsDeadAirVisible));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(HasNoRows));
            OnPropertyChanged(nameof(IsDeadAirVisible));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNoRows => !IsBusy && !HasError && Rows.Count == 0 && DeadAirRow == null;

    public bool IsDeadAirVisible => !IsBusy && !HasError && DeadAirRow != null;

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

    public string HoursHeaderText => GetHeaderText("Hours", LeaderboardSortColumn.TotalHours);
    public string PercentOfTrackedHeaderText => GetHeaderText("% Cards", LeaderboardSortColumn.PercentOfTrackedTime);
    public string PercentOfDayHeaderText => GetHeaderText("% Day", LeaderboardSortColumn.PercentOfDay);
    public string PointsHeaderText => GetHeaderText("Points", LeaderboardSortColumn.Points);

    public string SummaryText =>
        $"{_refreshedAt:MMM-dd HH:mm} | {Rows.Count} cards | {_totalTrackedHours:0.00} hrs tracked";

    public async Task RefreshAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ErrorMessage = "";
            IsBusy = true;
        });

        try
        {
            var now = DateTime.Now;
            var start = now.Date;
            var end = start.AddDays(1);

            var seed = await _db.GetHomeSeedDataAsync(start, end);

            var activeCards = seed.MainQuestCards
                .Cast<IActiveCardModel>()
                .Concat(seed.MissionCards.Cast<IActiveCardModel>())
                .ToList();

            var rawRows = activeCards
                .Select(card =>
                {
                    var hours = GetClippedActiveTime(card, start, end, now).TotalHours;
                    var points = card.GetValue(start, end);

                    return new
                    {
                        Card = card,
                        Hours = hours,
                        Points = points
                    };
                })
                .Where(x => x.Hours > 0.0001 || Math.Abs(x.Points) > 0.0001)
                .ToList();

            var totalTrackedHours = rawRows.Sum(x => x.Hours);
            var elapsedHoursToday = Math.Max(0, (now - start).TotalHours);
            var deadAirHours = Math.Max(0, elapsedHoursToday - totalTrackedHours);

            var rows = rawRows
                .Select(x => new LeaderboardRowModel(
                    x.Card.Title,
                    x.Hours,
                    totalTrackedHours <= 0 ? 0 : x.Hours / totalTrackedHours * 100,
                    x.Hours / 24d * 100,
                    x.Points))
                .ToList();

            var deadAirRow = new LeaderboardRowModel(
                "Dead Air",
                deadAirHours,
                elapsedHoursToday <= 0 ? 0 : deadAirHours / elapsedHoursToday * 100,
                deadAirHours / 24d * 100,
                0);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _allRows = rows;
                _totalTrackedHours = totalTrackedHours;
                _refreshedAt = now;
                DeadAirRow = deadAirRow;
                ApplySort();
            });
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _allRows.Clear();
                Rows.Clear();
                DeadAirRow = null;
                ErrorMessage = ex.Message;
                RaiseRowsChanged();
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    private static TimeSpan GetClippedActiveTime(
        IActiveCardModel card,
        DateTime start,
        DateTime end,
        DateTime now)
    {
        if (end <= start) return TimeSpan.Zero;

        var effectiveEnd = Min(end, now);
        if (effectiveEnd <= start) return TimeSpan.Zero;

        var totalMinutes = 0d;

        foreach (var period in card.Activity)
        {
            var activityStart = period.StartDate;
            var activityEnd = period.EndDate ?? effectiveEnd;
            activityEnd = Min(activityEnd, effectiveEnd);

            var overlapStart = Max(activityStart, start);
            var overlapEnd = Min(activityEnd, end);

            if (overlapEnd > overlapStart)
                totalMinutes += (overlapEnd - overlapStart).TotalMinutes;
        }

        return TimeSpan.FromMinutes(totalMinutes);
    }

    private void SortBy(LeaderboardSortColumn column)
    {
        if (_sortColumn == column)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortColumn = column;
            _sortDescending = true;
        }

        ApplySort();
    }

    private void ApplySort()
    {
        IOrderedEnumerable<LeaderboardRowModel> sorted = _sortColumn switch
        {
            LeaderboardSortColumn.PercentOfTrackedTime => _sortDescending
                ? _allRows.OrderByDescending(x => x.PercentOfTrackedTime)
                : _allRows.OrderBy(x => x.PercentOfTrackedTime),

            LeaderboardSortColumn.PercentOfDay => _sortDescending
                ? _allRows.OrderByDescending(x => x.PercentOfDay)
                : _allRows.OrderBy(x => x.PercentOfDay),

            LeaderboardSortColumn.Points => _sortDescending
                ? _allRows.OrderByDescending(x => x.PointsToday)
                : _allRows.OrderBy(x => x.PointsToday),

            _ => _sortDescending
                ? _allRows.OrderByDescending(x => x.HoursToday)
                : _allRows.OrderBy(x => x.HoursToday)
        };

        Rows.Clear();
        foreach (var row in sorted.ThenBy(x => x.Title))
            Rows.Add(row);

        RaiseSortHeaderChanged();
        RaiseRowsChanged();
    }

    private string GetHeaderText(string label, LeaderboardSortColumn column)
    {
        if (_sortColumn != column)
            return label;

        return _sortDescending ? $"{label} v" : $"{label} ^";
    }

    private void RaiseSortHeaderChanged()
    {
        OnPropertyChanged(nameof(HoursHeaderText));
        OnPropertyChanged(nameof(PercentOfTrackedHeaderText));
        OnPropertyChanged(nameof(PercentOfDayHeaderText));
        OnPropertyChanged(nameof(PointsHeaderText));
    }

    private void RaiseRowsChanged()
    {
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(IsDeadAirVisible));
        OnPropertyChanged(nameof(SummaryText));
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class LeaderboardRowModel
{
    public LeaderboardRowModel(
        string title,
        double hoursToday,
        double percentOfTrackedTime,
        double percentOfDay,
        double pointsToday)
    {
        Title = title;
        HoursToday = hoursToday;
        PercentOfTrackedTime = percentOfTrackedTime;
        PercentOfDay = percentOfDay;
        PointsToday = pointsToday;
    }

    public string Title { get; }
    public double HoursToday { get; }
    public double PercentOfTrackedTime { get; }
    public double PercentOfDay { get; }
    public double PointsToday { get; }

    public string HoursTodayText => HoursToday.ToString("0.00", CultureInfo.CurrentCulture);
    public string PercentOfTrackedTimeText => PercentOfTrackedTime.ToString("0.0", CultureInfo.CurrentCulture) + "%";
    public string PercentOfDayText => PercentOfDay.ToString("0.0", CultureInfo.CurrentCulture) + "%";
    public string PointsTodayText => PointsToday.ToString("0.##", CultureInfo.CurrentCulture);
    public Color PointsColor => PointsToday < 0 ? Colors.Red : Colors.Green;
}

public enum LeaderboardSortColumn
{
    TotalHours,
    PercentOfTrackedTime,
    PercentOfDay,
    Points
}
