using Points.Global;
using Points.Helpers;
using Points.Models;
using Points.Services.Diagnostics;
using Points.Services.Sqlite.Interfaces;
using Points.Services.Time;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
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
        private readonly IClock _clock;

        public Command AddAchievementCommand { get; }
        public Command OpenTrophyRoomCommand { get; }

        private AchievementsPageModel CurrentPage => Pages[Math.Clamp(Position, 0, Pages.Count - 1)];

        private async Task OpenTrophyRoomAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Achievements.TrophyRoomPage(_achievements));
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

            await Shell.Current.Navigation.PushAsync(
                new Points.Views.Details.AchievementDetailsPage(
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
                    }
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
            IClock? clock = null)
        {
            _cardWriter = cardWriter;
            _achievements = achievements;
            _clock = clock ?? ServiceHelper.GetService<IClock>();

            Pages.Add(CreateAchievementsPage());
            Pages.Add(CreateMetaAchievementsPage());

            AvailableTagsList = availableTagsList;

            AddAchievementCommand = new Command(async () => await AddAchievementAsync());
            OpenTrophyRoomCommand = new Command(async () => await OpenTrophyRoomAsync());

            _deadlineRefreshTimer = Application.Current.Dispatcher.CreateTimer();
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
            Cards.Add(card);
        }

        public void RemoveCard(AchievementCardModel card)
        {
            Cards.Remove(card);
        }
    }
}
