using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Points.Models;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Leaderboard;

internal sealed class LeaderboardController
{
    private readonly ICardReadService _cardReader;
    private readonly Func<DateTime> _localNow;
    private readonly Func<IActiveCardModel, DateTime, DateTime, DateTime, TimeSpan> _getClippedActiveTime;
    private readonly Action<string?> _notify;

    private List<LeaderboardRowModel> _allRows = new();
    private bool _isBusy;
    private string _errorMessage = "";
    private LeaderboardSortColumn _sortColumn = LeaderboardSortColumn.TotalHours;
    private bool _sortDescending = true;
    private DateTime _refreshedAt;
    private double _totalTrackedHours;
    private LeaderboardRowModel? _deadAirRow;

    public LeaderboardController(
        ICardReadService cardReader,
        Func<DateTime> localNow,
        Func<IActiveCardModel, DateTime, DateTime, DateTime, TimeSpan> getClippedActiveTime,
        Action<string?> notify)
    {
        _cardReader = cardReader ?? throw new ArgumentNullException(nameof(cardReader));
        _localNow = localNow ?? throw new ArgumentNullException(nameof(localNow));
        _getClippedActiveTime = getClippedActiveTime ?? throw new ArgumentNullException(nameof(getClippedActiveTime));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _refreshedAt = _localNow();
    }

    public ObservableCollection<LeaderboardRowModel> Rows { get; } = new();

    public LeaderboardRowModel? DeadAirRow
    {
        get => _deadAirRow;
        private set
        {
            if (_deadAirRow == value) return;
            _deadAirRow = value;
            Notify(nameof(LeaderboardViewModel.DeadAirRow));
            Notify(nameof(LeaderboardViewModel.IsDeadAirVisible));
            Notify(nameof(LeaderboardViewModel.HasNoRows));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            Notify(nameof(LeaderboardViewModel.IsBusy));
            Notify(nameof(LeaderboardViewModel.HasNoRows));
            Notify(nameof(LeaderboardViewModel.IsDeadAirVisible));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            Notify(nameof(LeaderboardViewModel.ErrorMessage));
            Notify(nameof(LeaderboardViewModel.HasError));
            Notify(nameof(LeaderboardViewModel.HasNoRows));
            Notify(nameof(LeaderboardViewModel.IsDeadAirVisible));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasNoRows => !IsBusy && !HasError && Rows.Count == 0 && DeadAirRow == null;

    public bool IsDeadAirVisible => !IsBusy && !HasError && DeadAirRow != null;

    public string HoursHeaderText => GetHeaderText("Hours", LeaderboardSortColumn.TotalHours);
    public string PercentOfTrackedHeaderText => GetHeaderText("% Cards", LeaderboardSortColumn.PercentOfTrackedTime);
    public string PercentOfDayHeaderText => GetHeaderText("% Day", LeaderboardSortColumn.PercentOfDay);
    public string PointsHeaderText => GetHeaderText("Points", LeaderboardSortColumn.Points);

    public string SummaryText =>
        $"{TimeDisplayFormatter.FormatLocal(_refreshedAt, "MMM-dd HH:mm")} | {Rows.Count} cards | {_totalTrackedHours:0.00} hrs tracked";

    public async Task RefreshAsync()
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ErrorMessage = "";
            IsBusy = true;
        });

        try
        {
            var now = _localNow();
            var start = now.Date;
            var end = start.AddDays(1);

            var seed = await _cardReader.GetHomeSeedDataAsync(start, end);
            var result = LeaderboardRowsBuilder.Build(seed, start, end, now, _getClippedActiveTime);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _allRows = result.Rows;
                _totalTrackedHours = result.TotalTrackedHours;
                _refreshedAt = now;
                DeadAirRow = result.DeadAirRow;
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

    public void SortBy(LeaderboardSortColumn column)
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
        Notify(nameof(LeaderboardViewModel.HoursHeaderText));
        Notify(nameof(LeaderboardViewModel.PercentOfTrackedHeaderText));
        Notify(nameof(LeaderboardViewModel.PercentOfDayHeaderText));
        Notify(nameof(LeaderboardViewModel.PointsHeaderText));
    }

    private void RaiseRowsChanged()
    {
        Notify(nameof(LeaderboardViewModel.HasNoRows));
        Notify(nameof(LeaderboardViewModel.IsDeadAirVisible));
        Notify(nameof(LeaderboardViewModel.SummaryText));
    }

    private void Notify(string propertyName) => _notify(propertyName);
}
