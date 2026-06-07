using Points.Evaluators;
using Points.Models;
using Points.Services.Diagnostics;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Home
{
    internal sealed class HomeRuntimeTickCoordinator
    {
        private static readonly TimeSpan PageNavigationSuppressionWindow = TimeSpan.FromMilliseconds(650);
        private static readonly TimeSpan ShortcutNavigationSuppressionWindow = TimeSpan.FromMilliseconds(1200);

        private readonly IAchievementService _achievements;
        private readonly IClock _clock;
        private readonly IReadOnlyList<HomePageModel> _pages;
        private readonly Func<int> _getPosition;
        private readonly Func<DateTime> _getRangeStart;
        private readonly Func<DateTime> _getRangeEnd;
        private readonly Action<DateTime> _setNow;
        private readonly Action<double> _setTopRightValue;
        private readonly Action _sortMissionCards;
        private readonly Action<string> _notifyPropertyChanged;
        private readonly Action _notifyTickHappened;
        private readonly IHardModePenaltyService _hardModePenalties;
        private readonly SemaphoreSlim _runtimeTickGate = new(1, 1);
        private readonly SemaphoreSlim _achievementTickGate = new(1, 1);

        private int _tickSuppressionCount;
        private int _suppressedTickPending;
        private CancellationTokenSource? _pageNavigationSuppressionCts;
        private CancellationTokenSource? _shortcutNavigationSuppressionCts;

        public HomeRuntimeTickCoordinator(
            IAchievementService achievements,
            IClock clock,
            IReadOnlyList<HomePageModel> pages,
            Func<int> getPosition,
            Func<DateTime> getRangeStart,
            Func<DateTime> getRangeEnd,
            Action<DateTime> setNow,
            Action<double> setTopRightValue,
            Action sortMissionCards,
            Action<string> notifyPropertyChanged,
            Action notifyTickHappened,
            IHardModePenaltyService hardModePenalties)
        {
            _achievements = achievements;
            _clock = clock;
            _pages = pages;
            _getPosition = getPosition;
            _getRangeStart = getRangeStart;
            _getRangeEnd = getRangeEnd;
            _setNow = setNow;
            _setTopRightValue = setTopRightValue;
            _sortMissionCards = sortMissionCards;
            _notifyPropertyChanged = notifyPropertyChanged;
            _notifyTickHappened = notifyTickHappened;
            _hardModePenalties = hardModePenalties ?? throw new ArgumentNullException(nameof(hardModePenalties));
        }

        public void Tick()
        {
            TaskSupervisor.Forget(TickAsync(), "Home runtime tick");
        }

        public async Task TickAsync()
        {
            if (AreTicksSuppressed)
            {
                Interlocked.Exchange(ref _suppressedTickPending, 1);
                return;
            }

            await RunTickCoreAsync(_clock.LocalNow, _clock.UtcNow);
        }

        public void RunImmediate()
        {
            TaskSupervisor.Forget(RunImmediateAsync(), "Immediate home runtime tick");
        }

        public Task RunImmediateAsync()
        {
            return RunTickCoreAsync(_clock.LocalNow, _clock.UtcNow);
        }

        public void RefreshBudgetCards(DateTime now)
        {
            foreach (var page in _pages)
                foreach (var budget in page.AllCards.OfType<BudgetCardModel>())
                    budget.NotifyTimeChanged(now);
        }

        public void SuppressTicksForPageNavigation()
        {
            _pageNavigationSuppressionCts?.Cancel();
            _pageNavigationSuppressionCts?.Dispose();

            var cts = new CancellationTokenSource();
            _pageNavigationSuppressionCts = cts;

            var suppression = BeginInteractionSuppression();
            _ = ReleasePageNavigationSuppressionAsync(suppression, cts);
        }

        public void SuppressTicksForShortcutNavigation()
        {
            _shortcutNavigationSuppressionCts?.Cancel();
            _shortcutNavigationSuppressionCts?.Dispose();

            var cts = new CancellationTokenSource();
            _shortcutNavigationSuppressionCts = cts;

            var suppression = BeginInteractionSuppression();
            _ = ReleaseShortcutNavigationSuppressionAsync(suppression, cts);
        }

        public IDisposable BeginInteractionSuppression()
        {
            Interlocked.Increment(ref _tickSuppressionCount);
            return new InteractionSuppressionHandle(this);
        }

        private async Task RunTickCoreAsync(DateTime now, DateTime utcNow)
        {
            if (!await _runtimeTickGate.WaitAsync(0))
                return;

            try
            {
                _setNow(now);

                foreach (var page in _pages)
                    foreach (var card in page.AllCards.OfType<MissionCardModel>())
                        card.NotifyTimeChanged();

                RefreshBudgetCards(now);
                _sortMissionCards();
                _ = UpdateAchievementsPerTickAsync();

                await _hardModePenalties.ReconcileAsync(utcNow);

                var rangeStart = _getRangeStart();
                var rangeEnd = _getRangeEnd();
                double total = 0;
                foreach (var page in _pages)
                    foreach (var card in page.AllCards)
                        total += MultiplierValueCalculator.GetValue(card, rangeStart, rangeEnd);

                total += await _hardModePenalties.GetValueAsync(rangeStart, rangeEnd, utcNow);

                _setTopRightValue(total);

                _notifyPropertyChanged(nameof(HomeViewModel.RangeEnd));
                _notifyPropertyChanged(nameof(HomeViewModel.ActiveMultiplierCode));
                _notifyPropertyChanged(nameof(HomeViewModel.HasActiveMultiplier));

                _notifyTickHappened();
            }
            finally
            {
                _runtimeTickGate.Release();
            }
        }

        private bool AreTicksSuppressed => Volatile.Read(ref _tickSuppressionCount) > 0;

        private async Task ReleasePageNavigationSuppressionAsync(IDisposable suppression, CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(PageNavigationSuppressionWindow, cts.Token);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_pageNavigationSuppressionCts, cts))
                    _pageNavigationSuppressionCts = null;

                cts.Dispose();
                suppression.Dispose();
            }
        }

        private async Task ReleaseShortcutNavigationSuppressionAsync(IDisposable suppression, CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(ShortcutNavigationSuppressionWindow, cts.Token);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_shortcutNavigationSuppressionCts, cts))
                    _shortcutNavigationSuppressionCts = null;

                cts.Dispose();
                suppression.Dispose();
            }
        }

        private void EndInteractionSuppression()
        {
            var remaining = Interlocked.Decrement(ref _tickSuppressionCount);
            if (remaining > 0)
                return;

            if (remaining < 0)
            {
                Interlocked.Exchange(ref _tickSuppressionCount, 0);
            }

            if (Interlocked.Exchange(ref _suppressedTickPending, 0) == 0)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (AreTicksSuppressed)
                {
                    Interlocked.Exchange(ref _suppressedTickPending, 1);
                    return;
                }

                TaskSupervisor.Forget(
                    RunTickCoreAsync(_clock.LocalNow, _clock.UtcNow),
                    "Home runtime tick after suppression");
            });
        }

        private async Task UpdateAchievementsPerTickAsync()
        {
            if (!await _achievementTickGate.WaitAsync(0))
                return;

            try
            {
                var position = _getPosition();
                if (_pages.Count == 0 || position < 0 || position >= _pages.Count)
                    return;

                var achievementsPage = _pages[position];
                if (achievementsPage == null) return;

                if (achievementsPage.Name != "Challenges & Pinned Achievements") return;

                var achievementCards = achievementsPage.AllCards.OfType<AchievementCardModel>().ToList();
                if (achievementCards.Count == 0) return;

                var activeCards = _pages
                    .SelectMany(x => x.AllCards.Where(y => y is IActiveCardModel))
                    .Cast<IActiveCardModel>()
                    .ToList();

                foreach (var card in achievementCards)
                {
                    // Finalized deadline achievements are inert and frozen.
                    if (IsFinalizedDeadlineAchievement(card))
                    {
                        if (card.FrozenCurrentValue.HasValue)
                            card.CurrentValue = card.FrozenCurrentValue.Value;

                        card.NotifyTimeChanged();
                        continue;
                    }

                    var cardsForThisAchievement = activeCards
                        .Where(x => CardMatchesAchievementByTag(x, card))
                        .ToList();

                    var evaluations = cardsForThisAchievement
                        .SelectMany(x => x.TimeValueAchievementEvaluators ?? Enumerable.Empty<TimeValueAchievementEvaluator>())
                        .SelectMany(x => x.Evaluations ?? Enumerable.Empty<TimeValueAchievementEvaluation>())
                        .ToList();

                    // Live achievements still update dynamically.
                    card.UpdatePerTick(evaluations);

                    // Deadline achievements may transition during a tick.
                    if (card.CompletionType == AchievementCompletionType.Deadline)
                    {
                        var reevaluated = await _achievements.ReevaluateDeadlineAchievementAsync(card);

                        if (reevaluated != null)
                        {
                            ApplyAchievementRuntimeState(card, reevaluated);
                        }
                    }
                }
            }
            finally
            {
                _achievementTickGate.Release();
            }
        }

        private static bool IsFinalizedDeadlineAchievement(AchievementCardModel card)
        {
            return card != null
                && card.CompletionType == AchievementCompletionType.Deadline
                && card.FinalizedAt.HasValue;
        }

        private static bool CardMatchesAchievementByTag(IActiveCardModel activeCard, AchievementCardModel achievement)
        {
            if (activeCard == null || achievement == null)
                return false;

            if (string.IsNullOrWhiteSpace(achievement.Tags))
                return false;

            var achievementTag = achievement.Tags.Trim();

            return (activeCard.Tags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(achievementTag, StringComparer.OrdinalIgnoreCase);
        }

        private static void ApplyAchievementRuntimeState(AchievementCardModel target, AchievementCardModel source)
        {
            if (target == null || source == null)
                return;

            // Core persisted fields that may change during runtime / finalization
            target.Status = source.Status;
            target.LastEarnedAt = source.LastEarnedAt;
            target.FinalizedAt = source.FinalizedAt;
            target.FrozenCurrentValue = source.FrozenCurrentValue;

            // These should remain consistent too in case they were changed/reloaded
            target.CreatedDate = source.CreatedDate;
            target.DeadlineStart = source.DeadlineStart;
            target.Deadline = source.Deadline;

            // Keep trophies in sync in case completion awarded one
            SyncTrophies(target, source);

            // Finalized deadline achievements must freeze at their frozen value
            if (source.CompletionType == AchievementCompletionType.Deadline &&
                source.FinalizedAt.HasValue)
            {
                target.CurrentValue = source.FrozenCurrentValue ?? source.CurrentValue;
            }
            else
            {
                target.CurrentValue = source.CurrentValue;
            }

            target.NotifyTimeChanged();
        }

        private static void SyncTrophies(AchievementCardModel target, AchievementCardModel source)
        {
            if (target == null || source == null)
                return;

            target.Trophies.Clear();

            foreach (var trophy in source.Trophies)
            {
                target.Trophies.Add(trophy);
            }
        }

        private sealed class InteractionSuppressionHandle : IDisposable
        {
            private HomeRuntimeTickCoordinator? _owner;

            public InteractionSuppressionHandle(HomeRuntimeTickCoordinator owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                owner?.EndInteractionSuppression();
            }
        }
    }
}
