using Points.Models;
using Points.ViewModels.Goals;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Home
{
    internal sealed class HomeGoalsPageCoordinator
    {
        private static readonly TimeScope[] GoalScopes =
        [
            TimeScope.Daily,
            TimeScope.Weekly,
            TimeScope.Monthly
        ];

        private readonly ICardReadService _cardReader;
        private readonly IGoalService _goals;
        private readonly IClock _clock;
        private readonly IReadOnlyList<HomePageModel> _pages;
        private long _reloadVersion;

        public HomeGoalsPageCoordinator(
            ICardReadService cardReader,
            IGoalService goals,
            IClock clock,
            IReadOnlyList<HomePageModel> pages)
        {
            _cardReader = cardReader;
            _goals = goals;
            _clock = clock;
            _pages = pages;
        }

        public async Task<IReadOnlyList<ICardModel>> BuildGoalProgressCardsAsync(
            IReadOnlyList<IActiveCardModel> allCards)
        {
            var allGoalModels = await _goals.GetGoalModelsDataAsync();
            return BuildGoalProgressCards(allCards, allGoalModels);
        }

        public void ReplaceGoalProgressCards(
            HomePageModel? goalsPage,
            IEnumerable<ICardModel> goalProgressCards)
        {
            if (goalsPage == null)
                return;

            Interlocked.Increment(ref _reloadVersion);
            goalsPage.ReplaceCards(goalProgressCards);
        }

        public async Task ReloadGoalsAsync()
        {
            var goalsPage = _pages.FirstOrDefault(p => p.Name == "Goals");
            if (goalsPage == null)
                return;

            var reloadVersion = Interlocked.Increment(ref _reloadVersion);

            var now = _clock.LocalNow;
            var ranges = GoalScopes
                .Select(scope => new TimeScopeRange(scope, now))
                .ToList();

            var allCards = await _cardReader.GetMainQuestModelsDataAsync(
                ranges.Min(range => range.Start),
                ranges.Max(range => range.End));

            var goalProgressCards = await BuildGoalProgressCardsAsync(allCards);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (reloadVersion == Volatile.Read(ref _reloadVersion))
                    goalsPage.ReplaceCards(goalProgressCards);
            });
        }

        private IReadOnlyList<ICardModel> BuildGoalProgressCards(
            IReadOnlyList<IActiveCardModel> allCards,
            IEnumerable<GoalDetailsModel> allGoalModels)
        {
            var enabledGoalModels = allGoalModels
                .Where(p => p.Enabled)
                .ToList();

            var cards = new List<ICardModel>();

            foreach (var scope in GoalScopes)
            {
                var rowVms = BuildRowsForScope(allCards, enabledGoalModels, scope);
                if (rowVms.Count == 0)
                    continue;

                cards.Add(new DateHeaderCardModel { Title = scope.ToString() });
                cards.AddRange(rowVms);
            }

            return cards;
        }

        private List<GoalProgressRowVm> BuildRowsForScope(
            IReadOnlyList<IActiveCardModel> allCards,
            IEnumerable<GoalDetailsModel> enabledGoalModels,
            TimeScope scope)
        {
            var rowVms = new List<GoalProgressRowVm>();
            var modelsForScope = enabledGoalModels
                .Where(p => p.TimeScope == scope)
                .ToList();

            foreach (var goalModel in modelsForScope)
            {
                var card = allCards.FirstOrDefault(c => c.CardID == goalModel.CardId);
                if (card is null)
                    continue;

                rowVms.Add(new GoalProgressRowVm(card, goalModel, () => _clock.LocalNow)
                {
                    EnableCheckbox = false
                });
            }

            return rowVms;
        }
    }
}
