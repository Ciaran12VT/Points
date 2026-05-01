using Points.Models;
using Points.Services.Persistence;

namespace Points.ViewModels.Leaderboard;

internal static class LeaderboardRowsBuilder
{
    public static LeaderboardRowsBuildResult Build(
        HomeSeedData seed,
        DateTime start,
        DateTime end,
        DateTime now,
        Func<IActiveCardModel, DateTime, DateTime, DateTime, TimeSpan> getClippedActiveTime)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(getClippedActiveTime);

        var activeCards = seed.MainQuestCards
            .Concat(seed.MissionCards)
            .ToList();

        var rawRows = activeCards
            .Select(card =>
            {
                var hours = getClippedActiveTime(card, start, end, now).TotalHours;
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

        return new LeaderboardRowsBuildResult(rows, totalTrackedHours, deadAirRow);
    }
}

internal sealed record LeaderboardRowsBuildResult(
    List<LeaderboardRowModel> Rows,
    double TotalTrackedHours,
    LeaderboardRowModel DeadAirRow);
