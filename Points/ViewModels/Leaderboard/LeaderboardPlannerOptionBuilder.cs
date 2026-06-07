using Points.Models;

namespace Points.ViewModels.Leaderboard;

internal static class LeaderboardPlannerOptionBuilder
{
    public static LeaderboardPlannerOptions Build(PlannerDayData? dayData)
    {
        if (dayData == null)
            return LeaderboardPlannerOptions.Empty;

        var taskOptions = dayData.TaskCards
            .GroupBy(c => c.CardID)
            .Select(g =>
            {
                var card = g.First();
                var kind = GetCardKind(card);
                return new PlannerTaskCardOption(card.CardID, kind, card.Title);
            })
            .OrderBy(o => o.Title)
            .ToList();

        var stepOptions = dayData.ScCards
            .SelectMany(card => card.Steps.Select(step => new PlannerStepOption(
                card.CardID,
                step.Id,
                card.Title,
                step.Title)))
            .OrderBy(o => o.DisplayTitle)
            .ToList();

        var missionOptions = dayData.MissionCards
            .GroupBy(m => m.CardID)
            .Select(g =>
            {
                var mission = g.First();
                return new PlannerMissionOption(mission.CardID, mission.Title);
            })
            .OrderBy(o => o.Title)
            .ToList();

        return new LeaderboardPlannerOptions(taskOptions, stepOptions, missionOptions);
    }

    private static PlannerTaskCardKind GetCardKind(IActiveCardModel card)
    {
        return card switch
        {
            MissionCardModel => PlannerTaskCardKind.Mission,
            ScCardModel => PlannerTaskCardKind.ScCard,
            _ => PlannerTaskCardKind.TatCard
        };
    }
}

internal sealed record LeaderboardPlannerOptions(
    IReadOnlyList<PlannerTaskCardOption> TaskOptions,
    IReadOnlyList<PlannerStepOption> StepOptions,
    IReadOnlyList<PlannerMissionOption> MissionOptions)
{
    public static LeaderboardPlannerOptions Empty { get; } = new(
        Array.Empty<PlannerTaskCardOption>(),
        Array.Empty<PlannerStepOption>(),
        Array.Empty<PlannerMissionOption>());
}
