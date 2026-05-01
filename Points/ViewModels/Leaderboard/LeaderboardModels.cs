using System.Globalization;
using Microsoft.Maui.Graphics;

namespace Points.ViewModels.Leaderboard;

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
