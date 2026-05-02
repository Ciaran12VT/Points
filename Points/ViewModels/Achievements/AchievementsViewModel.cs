using Points.Global;
using Points.Models;
using Points.Services.Diagnostics;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels.Achievements
{
    public class AchievementsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<AchievementsPageModel> Pages { get; } = new();

        private int _position;
        public int Position
        {
            get => _position;
            set
            {
                if (_position == value) return;
                _position = value;
                OnPropertyChanged();
            }
        }

        public List<string> AvailableTagsList;
        private readonly ICardWriteService _cardWriter;
        private readonly IAchievementService _achievements;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly IClock _clock;

        public Command AddAchievementCommand { get; }
        public Command OpenTrophyRoomCommand { get; }
        public Command<AchievementCardModel> OpenAchievementDetailsCommand { get; }
        public Command ToggleOrderModeCommand { get; }
        public Command<AchievementCardModel> MoveAchievementUpCommand { get; }
        public Command<AchievementCardModel> MoveAchievementDownCommand { get; }

        private bool _isOrderMode;
        public bool IsOrderMode
        {
            get => _isOrderMode;
            set
            {
                if (_isOrderMode == value)
                    return;

                _isOrderMode = value;
                OnPropertyChanged(nameof(IsOrderMode));
            }
        }

        private AchievementsPageModel CurrentPage => Pages[Math.Clamp(Position, 0, Pages.Count - 1)];

        private async Task OpenTrophyRoomAsync()
        {
            await _navigation.PushAsync(new Points.Views.Achievements.TrophyRoomPage(_achievements, _navigation, _dialogs, _clock));
        }

        private async Task AddAchievementAsync()
        {
            var page = CurrentPage;

            var now = _clock.LocalNow;

            var model = new AchievementCardModel
            {
                Title = "New Achievement",
                Status = "In-Progress",
                Tags = "",
                TargetType = AchievementTargetType.ActiveTime,
                TargetValue = 0,
                CompletionType = AchievementCompletionType.Range,
                RangeUnit = AchievementRangeUnit.Days,
                RangeAmount = 7,
                CreatedDate = now,
                DeadlineStart = now,
                Deadline = now,
                IsPinned = true,
            };

            var allTags = GetAllTags();
            var stepNames = GetAllStepNames();
            var achievementTitles = GetAllAchievementTitles();

            await _navigation.PushAsync(
                new Points.Views.Achievements.AchievementDetailsPage(
                    model,
                    allTags,
                    stepNames,
                    achievementTitles,
                    async saved =>
                    {
                        var achievementsPage = Pages.First(p => p.Name == "Achievements");
                        var metaAchievementsPage = Pages.First(p => p.Name == "Meta-Achievements");
                        var page = saved.TargetType == AchievementTargetType.Achievements ? metaAchievementsPage : achievementsPage;
                        await CommitCardToPage(page, saved, false);
                    },
                    deleted =>
                    {
                        var achievementsPage = Pages.First(p => p.Name == "Achievements");
                        var metaAchievementsPage = Pages.First(p => p.Name == "Meta-Achievements");
                        var page = deleted.TargetType == AchievementTargetType.Achievements ? metaAchievementsPage : achievementsPage;
                        RemoveCardFromPage(page, deleted);
                        DeleteCardFromDb(deleted);
                    },
                    _clock,
                    _navigation,
                    _dialogs
                )
            );
        }

        private async Task OpenAchievementDetailsAsync(AchievementCardModel? model)
        {
            if (model == null)
                return;

            await _navigation.PushAsync(
                new Points.Views.Achievements.AchievementDetailsPage(
                    model,
                    GetAllTags(),
                    GetAllStepNames(),
                    GetAllAchievementTitles(),
                    async saved =>
                    {
                        var achievementsPage = Pages.First(p => p.Name == "Achievements");
                        var metaAchievementsPage = Pages.First(p => p.Name == "Meta-Achievements");
                        var page = saved.TargetType == AchievementTargetType.Achievements ? metaAchievementsPage : achievementsPage;
                        await CommitCardToPage(page, saved, false);
                    },
                    deleted =>
                    {
                        var achievementsPage = Pages.First(p => p.Name == "Achievements");
                        var metaAchievementsPage = Pages.First(p => p.Name == "Meta-Achievements");
                        var page = deleted.TargetType == AchievementTargetType.Achievements ? metaAchievementsPage : achievementsPage;
                        RemoveCardFromPage(page, deleted);
                        DeleteCardFromDb(deleted);
                    },
                    _clock,
                    _navigation,
                    _dialogs
                )
            );
        }

        public IEnumerable<string> GetAllTags()
        {
            return AvailableTagsList;
        }

        private IEnumerable<string> GetAllAchievementTitles()
        {
            return Pages
                .SelectMany(p => p.Cards)
                .Select(c => c.Title)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .OrderBy(x => x);
        }

        public Task? Initialization { get; private set; }

        private readonly SemaphoreSlim _deadlineRefreshGate = new(1, 1);
        private readonly IDispatcherTimer _deadlineRefreshTimer;

        public AchievementsViewModel(
            List<string> availableTagsList,
            ICardWriteService cardWriter,
            IAchievementService achievements,
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            IClock clock)
        {
            _cardWriter = cardWriter;
            _achievements = achievements;
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            Pages.Add(CreateAchievementsPage());
            Pages.Add(CreateMetaAchievementsPage());

            AvailableTagsList = availableTagsList;

            AddAchievementCommand = new Command(async () => await AddAchievementAsync());
            OpenTrophyRoomCommand = new Command(async () => await OpenTrophyRoomAsync());
            OpenAchievementDetailsCommand = new Command<AchievementCardModel>(async model => await OpenAchievementDetailsAsync(model));
            ToggleOrderModeCommand = new Command(() =>
            {
                IsOrderMode = !IsOrderMode;
            });
            MoveAchievementUpCommand = new Command<AchievementCardModel>(async card => await MoveCardByOffsetAsync(card, -1));
            MoveAchievementDownCommand = new Command<AchievementCardModel>(async card => await MoveCardByOffsetAsync(card, 1));

            var dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("Application dispatcher is not available.");

            _deadlineRefreshTimer = dispatcher.CreateTimer();
            _deadlineRefreshTimer.Interval = TimeSpan.FromSeconds(1);
            _deadlineRefreshTimer.Tick += async (_, __) => await RefreshDeadlineAchievementsAsync();
            _deadlineRefreshTimer.Start();

            Initialization = LoadAsync();
        }

        public void StopTimer()
        {
            _deadlineRefreshTimer?.Stop();
        }

        private async Task LoadAsync()
        {
            var achievements = await _achievements.GetAchievementCardModelsDataAsync();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var regularAchievements = achievements.Where(x => x.TargetType != AchievementTargetType.Achievements).ToList();
                var metaAchievements = achievements.Where(x => x.TargetType == AchievementTargetType.Achievements).ToList();

                var achievementsPage = Pages.First(p => p.Name == "Achievements");
                var metaAchievementsPage = Pages.First(p => p.Name == "Meta-Achievements");

                foreach (var c in regularAchievements)
                    await CommitCardToPage(achievementsPage, c, true);

                foreach (var c in metaAchievements)
                    await CommitCardToPage(metaAchievementsPage, c, true);

            });
        }

        private async Task RefreshDeadlineAchievementsAsync()
        {
            if (!await _deadlineRefreshGate.WaitAsync(0))
                return;

            try
            {
                var allCards = Pages
                    .SelectMany(p => p.Cards)
                    .ToList();

                if (allCards.Count == 0)
                    return;

                foreach (var card in allCards)
                {
                    // We only care about deadline achievements here.
                    if (card.CompletionType != AchievementCompletionType.Deadline)
                        continue;

                    // Finalized deadline achievements remain visible but inert.
                    if (IsFinalizedDeadlineAchievement(card))
                    {
                        if (card.FrozenCurrentValue.HasValue)
                            card.CurrentValue = card.FrozenCurrentValue.Value;

                        card.NotifyTimeChanged();
                        continue;
                    }

                    var refreshed = await _achievements.ReevaluateDeadlineAchievementAsync(card);

                    if (refreshed != null)
                    {
                        ApplyAchievementRuntimeState(card, refreshed);
                    }
                }
            }
            finally
            {
                _deadlineRefreshGate.Release();
            }
        }

        private static bool IsFinalizedDeadlineAchievement(AchievementCardModel card)
        {
            return card != null
                && card.CompletionType == AchievementCompletionType.Deadline
                && card.FinalizedAt.HasValue;
        }

        private static void ApplyAchievementRuntimeState(AchievementCardModel target, AchievementCardModel source)
        {
            if (target == null || source == null)
                return;

            target.Status = source.Status;
            target.LastEarnedAt = source.LastEarnedAt;
            target.FinalizedAt = source.FinalizedAt;
            target.FrozenCurrentValue = source.FrozenCurrentValue;

            target.CreatedDate = source.CreatedDate;
            target.DeadlineStart = source.DeadlineStart;
            target.Deadline = source.Deadline;

            SyncTrophies(target, source);

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

        /// <summary>
        /// The ONE AND ONLY way a card gets added to a page.
        /// All callers (mock seeding + UI save callbacks) must use this.
        /// </summary>
        public async Task CommitCardToPage(AchievementsPageModel page, AchievementCardModel card, bool noDb = false)
        {
            if (page == null || card == null) return;

            // If card already exists (editing existing), don't duplicate
            // (Reference equality is the safest default here.)
            if (!page.Cards.Contains(card))
            {
                page.AddCard(card);
            }

            if (!noDb)
            {
                await CommitCardToDb(card);
                RefreshSingleDeadlineAchievementAsync(card).Forget("Refresh saved deadline achievement");
            }
        }

        public async Task ReorderCardsAsync(AchievementCardModel? dragged, AchievementCardModel? target)
        {
            if (dragged == null || target == null)
                return;

            var page = Pages.FirstOrDefault(p => p.Cards.Contains(dragged));
            if (page == null || !ReferenceEquals(page, Pages.FirstOrDefault(p => p.Cards.Contains(target))))
                return;

            if (!page.MoveCard(dragged, target))
                return;

            var persistedCards = page.Cards
                .Where(c => c.CardID > 0)
                .Cast<ICardModel>()
                .ToList();

            if (persistedCards.Count > 0)
                await _cardWriter.SaveCardDisplayOrderAsync(persistedCards);
        }

        private async Task MoveCardByOffsetAsync(AchievementCardModel? card, int offset)
        {
            if (card == null)
                return;

            var page = Pages.FirstOrDefault(p => p.Cards.Contains(card));
            if (page == null || !page.MoveCardByOffset(card, offset))
                return;

            var persistedCards = page.Cards
                .Where(c => c.CardID > 0)
                .Cast<ICardModel>()
                .ToList();

            if (persistedCards.Count > 0)
                await _cardWriter.SaveCardDisplayOrderAsync(persistedCards);
        }

        private async Task CommitCardToDb(ICardModel card)
        {
            await _cardWriter.SaveCardModelAsync(card);
        }

        public void DeleteCardFromDb(AchievementCardModel deleted)
        {
            var files = Directory.GetFiles(AppPaths.GetAchievementTrophiesPath(deleted.Id));
            foreach (var file in files)
            {
                File.Delete(file);
            }

            Directory.Delete(AppPaths.GetAchievementTrophiesPath(deleted.Id));

            _achievements.DeleteAchievementCardModelAsync(deleted).Forget("Delete achievement card");
        }

        public void RemoveCardFromPage(AchievementsPageModel page, AchievementCardModel card)
        {
            if (page == null || card == null) return;
            page.RemoveCard(card);
        }

        private async Task RefreshSingleDeadlineAchievementAsync(AchievementCardModel card)
        {
            if (card == null)
                return;

            if (card.CompletionType != AchievementCompletionType.Deadline)
                return;

            var refreshed = await _achievements.ReevaluateDeadlineAchievementAsync(card);
            if (refreshed != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ApplyAchievementRuntimeState(card, refreshed);
                });
            }
        }

        private AchievementsPageModel CreateAchievementsPage()
        {
            return new AchievementsPageModel(
                "Achievements",
                new ObservableCollection<AchievementCardModel>
                {
                });
        }

        private AchievementsPageModel CreateMetaAchievementsPage()
        {
            return new AchievementsPageModel(
                "Meta-Achievements",
                new ObservableCollection<AchievementCardModel>
                {
                });
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        internal IEnumerable<string> GetAllStepNames()
        {
            // Placeholder for now until you wire “steps from cards with selected tags”
            return Array.Empty<string>();
        }

    }

    public class AchievementsPageModel
    {
        public string Name { get; }
        public ObservableCollection<AchievementCardModel> Cards { get; }

        public AchievementsPageModel(string name, ObservableCollection<AchievementCardModel> cards)
        {
            Name = name;
            Cards = cards;
        }
        public void AddCard(AchievementCardModel card)
        {
            if (card.CardID == 0 && card.DisplayOrder == 0 && Cards.Count > 0)
                card.DisplayOrder = Cards.Max(c => c.DisplayOrder) + 1;

            Cards.Add(card);
        }

        public void RemoveCard(AchievementCardModel card)
        {
            Cards.Remove(card);
        }

        public bool MoveCard(AchievementCardModel dragged, AchievementCardModel target)
        {
            if (dragged == null || target == null || ReferenceEquals(dragged, target))
                return false;

            var oldIndex = Cards.IndexOf(dragged);
            var newIndex = Cards.IndexOf(target);

            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
                return false;

            Cards.Move(oldIndex, newIndex);
            NormalizeDisplayOrder();
            return true;
        }

        public bool MoveCardByOffset(AchievementCardModel card, int offset)
        {
            if (card == null || offset == 0)
                return false;

            var index = Cards.IndexOf(card);
            var targetIndex = index + offset;
            if (index < 0 || targetIndex < 0 || targetIndex >= Cards.Count)
                return false;

            return MoveCard(card, Cards[targetIndex]);
        }

        private void NormalizeDisplayOrder()
        {
            for (var i = 0; i < Cards.Count; i++)
                Cards[i].DisplayOrder = i;
        }
    }
}
