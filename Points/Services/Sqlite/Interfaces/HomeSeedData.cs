using Points.Models;

namespace Points.Services.Sqlite.Interfaces
{
    public sealed class HomeSeedData
    {
        public IReadOnlyList<IActiveCardModel> MainQuestCards { get; init; } = new List<IActiveCardModel>();
        public IReadOnlyList<IActiveCardModel> MissionCards { get; init; } = new List<IActiveCardModel>();
        public IReadOnlyList<ICardModel> BudgetCards { get; init; } = new List<ICardModel>();
        public IReadOnlyList<ICardModel> Achievements { get; init; } = new List<ICardModel>();
        public IReadOnlyList<ICardModel> ValueTrackers { get; init; } = new List<ICardModel>();
        public IReadOnlyList<ICardModel> EventTrackers { get; init; } = new List<ICardModel>();
    }
}
