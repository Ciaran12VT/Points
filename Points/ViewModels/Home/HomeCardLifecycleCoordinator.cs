using Points.Models;
using Points.Services.Locks;
using Points.Services.MissionSharing;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Home
{
    internal sealed class HomeCardLifecycleCoordinator
    {
        private readonly IReadOnlyList<HomePageModel> _pages;
        private readonly ICardWriteService _cardWriter;
        private readonly IBudgetService _budgets;
        private readonly ITrackerService _trackers;
        private readonly ITatCardService _tats;
        private readonly IClock _clock;
        private readonly IAppDialogService _dialogs;
        private readonly IMissionShareService _missionShares;
        private readonly Func<List<IActiveCardModel>> _getActiveCardModels;
        private readonly Action<ICardModel> _wireLongPress;
        private readonly Action _sortMissionCards;
        private readonly Action<string> _notifyPropertyChanged;
        private readonly Func<Task> _reloadDashboardAsync;

        public HomeCardLifecycleCoordinator(
            IReadOnlyList<HomePageModel> pages,
            ICardWriteService cardWriter,
            IBudgetService budgets,
            ITrackerService trackers,
            ITatCardService tats,
            IClock clock,
            IAppDialogService dialogs,
            Func<List<IActiveCardModel>> getActiveCardModels,
            Action<ICardModel> wireLongPress,
            Action sortMissionCards,
            Action<string> notifyPropertyChanged,
            Func<Task> reloadDashboardAsync,
            IMissionShareService missionShares)
        {
            _pages = pages;
            _cardWriter = cardWriter;
            _budgets = budgets;
            _trackers = trackers;
            _tats = tats;
            _clock = clock;
            _dialogs = dialogs;
            _missionShares = missionShares;
            _getActiveCardModels = getActiveCardModels;
            _wireLongPress = wireLongPress;
            _sortMissionCards = sortMissionCards;
            _notifyPropertyChanged = notifyPropertyChanged;
            _reloadDashboardAsync = reloadDashboardAsync;
        }

        public void CommitCardToPage(HomePageModel? page, ICardModel? card, bool noDb = false)
        {
            if (page == null || card == null)
                return;

            if (!page.AllCards.Contains(card))
                page.AddCard(card);

            _wireLongPress(card);

            if (!noDb)
                CommitCardToDb(card);

            AfterCardCommitted(page, card);
        }

        public void ReplaceCardsForLoad(HomePageModel? page, IEnumerable<ICardModel> cards)
        {
            if (page == null)
                return;

            ArgumentNullException.ThrowIfNull(cards);

            var replacement = cards.ToList();
            page.ReplaceCards(replacement);

            foreach (var card in replacement)
                _wireLongPress(card);

            if (page.Name == "Mission")
                RefreshMissionState();

            _notifyPropertyChanged(nameof(HomeViewModel.GlobalValueColor));
        }

        public void RemoveCardFromPage(HomePageModel? page, ICardModel? card)
        {
            if (page == null || card == null)
                return;

            page.RemoveCard(card);

            if (page.Name == "Mission")
                RefreshMissionState();
        }

        public async Task DeleteCardFromPageAndDbAsync(HomePageModel? page, ICardModel? card)
        {
            if (page == null || card == null)
                return;

            await _cardWriter.DeleteCardModelAsync(card);
            RemoveCardFromPage(page, card);
            await _reloadDashboardAsync();
        }

        public Task<bool> WouldArchiveOnDeleteAsync(ICardModel? card)
        {
            return card == null
                ? Task.FromResult(false)
                : _cardWriter.WouldArchiveCardModelOnDeleteAsync(card);
        }

        public async Task FailMissionAsync(MissionCardModel model)
        {
            model.Fail(_clock.UtcNow);
            _sortMissionCards();
            await SaveMissionAsync(model);
            await PromptShareUpdateIfNeededAsync(model, "failed status");
        }

        public async Task RestoreMissionAsync(MissionCardModel model)
        {
            model.Restore();
            _sortMissionCards();
            await SaveMissionAsync(model);
            await PromptShareUpdateIfNeededAsync(model, "restore");
        }

        public async Task SaveMissionAsync(MissionCardModel model)
        {
            await _cardWriter.SaveCardModelAsync(model);
        }

        public async Task CompleteMissionAsync(MissionCardModel? model)
        {
            if (model == null || model.IsComplete)
                return;

            var now = _clock.LocalNow;
            if (LockEvaluator.IsLockedNow(model, now, _getActiveCardModels(), out var availableAt))
            {
                var remaining = LockEvaluator.FormatRemaining(now, availableAt);
                await _dialogs.DisplayAlertAsync("Locked", $"This mission is locked. Available in {remaining}.", "OK");
                return;
            }

            var confirm = await _dialogs.DisplayAlertAsync(
                "Complete mission?",
                "Mark as complete?",
                "Complete",
                "Cancel");

            if (!confirm)
                return;

            if (model.CompleteCommand.CanExecute(null))
                model.CompleteCommand.Execute(null);

            await Task.Yield();
            await SaveMissionAsync(model);
            await PromptShareUpdateIfNeededAsync(model, "completion");
        }

        private void CommitCardToDb(ICardModel card)
        {
            _ = HomeCardSaveRouter.SaveAsync(
                card,
                _cardWriter,
                _budgets,
                _trackers,
                _tats);
        }

        private void AfterCardCommitted(HomePageModel page, ICardModel card)
        {
            if (page.Name == "Mission" && card is MissionCardModel)
                RefreshMissionState();

            _notifyPropertyChanged(nameof(HomeViewModel.GlobalValueColor));
        }

        private void RefreshMissionState()
        {
            _sortMissionCards();
            _notifyPropertyChanged(nameof(HomeViewModel.HasNegativeAvailableMission));
        }

        private async Task PromptShareUpdateIfNeededAsync(MissionCardModel model, string updateDescription)
        {
            if (string.IsNullOrWhiteSpace(model.SharedWith))
                return;

            var send = await _dialogs.DisplayAlertAsync(
                "Share update?",
                $"This mission is shared with {model.SharedWith}. Send the {updateDescription} update now?",
                "Share",
                "Not now");

            if (!send)
                return;

            try
            {
                await _missionShares.ShareMissionAsync(model);
            }
            catch (Exception ex)
            {
                await _dialogs.DisplayAlertAsync("Share failed", ex.Message, "OK");
            }
        }
    }
}
