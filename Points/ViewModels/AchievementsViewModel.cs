using Points.Models;
using Points.Services;
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
        private IDbService _db;

        public Command AddAchievementCommand { get; }
        public Command OpenTrophyRoomCommand { get; }

        private AchievementsPageModel CurrentPage => Pages[Math.Clamp(Position, 0, Pages.Count - 1)];

        private async Task OpenTrophyRoomAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Achievements.TrophyRoomPage());
        }

        private async Task AddAchievementAsync()
        {
            var page = CurrentPage;

            var model = new AchievementCardModel
            {
                Title = "New Achievement",
                Status = "In-Progress",
                Tags = "",
                GoalType = AchievementGoalType.ActiveTime,
                TargetValue = 0,
                CompletionType = AchievementCompletionType.Range,
                RangeUnit = AchievementRangeUnit.Days,
                RangeAmount = 7,
                Deadline = DateTime.Now,
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
                    saved =>
                    {
                        var achievementsPage = Pages.First(p => p.Name == "Achievements");
                        var metaAchievementsPage = Pages.First(p => p.Name == "Meta-Achievements");
                        var page = saved.GoalType == AchievementGoalType.Achievements ? metaAchievementsPage : achievementsPage;
                        CommitCardToPage(page, saved, false);
                    },
                    deleted =>
                    {
                        var achievementsPage = Pages.First(p => p.Name == "Achievements");
                        var metaAchievementsPage = Pages.First(p => p.Name == "Meta-Achievements");
                        var page = deleted.GoalType == AchievementGoalType.Achievements ? metaAchievementsPage : achievementsPage;
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

        public AchievementsViewModel(List<string> availableTagsList, Services.IDbService db)
        {
            _db = db;

            Pages.Add(CreateAchievementsPage());
            Pages.Add(CreateMetaAchievementsPage());

            AvailableTagsList = availableTagsList;

            Initialization = LoadAsync();

            AddAchievementCommand = new Command(async () => await AddAchievementAsync());
            OpenTrophyRoomCommand = new Command(async () => await OpenTrophyRoomAsync());
        }

        private async Task LoadAsync()
        {
            var achievements = await _db.GetAchievementCardModelsDataAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var regularAchievements = achievements.Where(x => x.GoalType != AchievementGoalType.Achievements).ToList();
                var metaAchievements = achievements.Where(x => x.GoalType == AchievementGoalType.Achievements).ToList();

                var achievementsPage = Pages.First(p => p.Name == "Achievements");
                var metaAchievementsPage = Pages.First(p => p.Name == "Meta-Achievements");

                foreach (var c in regularAchievements) CommitCardToPage(achievementsPage, c, true);

                foreach (var c in metaAchievements) CommitCardToPage(metaAchievementsPage, c, true);

            });
        }

        /// <summary>
        /// The ONE AND ONLY way a card gets added to a page.
        /// All callers (mock seeding + UI save callbacks) must use this.
        /// </summary>
        private void CommitCardToPage(AchievementsPageModel page, AchievementCardModel card, bool noDb = false)
        {
            if (page == null || card == null) return;

            // If card already exists (editing existing), don't duplicate
            // (Reference equality is the safest default here.)
            if (!page.Cards.Contains(card))
            {
                page.AddCard(card);
            }

            if (!noDb) CommitCardToDb(card);
        }

        private void CommitCardToDb(ICardModel card)
        {
            _db.SaveCardModelAsync(card);
        }

        public void DeleteCardFromDb(AchievementCardModel deleted)
        {
            _db.DeleteAchievementCardModelAsync(deleted);
        }

        public void RemoveCardFromPage(AchievementsPageModel page, AchievementCardModel card)
        {
            if (page == null || card == null) return;
            page.RemoveCard(card);
        }

        private AchievementsPageModel CreateAchievementsPage()
        {
            return new AchievementsPageModel(
                "Achievements",
                new ObservableCollection<AchievementCardModel>
                {
                    //new AchievementCardModel
                    //{
                    //    Title = "Super Nerd",
                    //    Status = "In-Progress",
                    //    Tags = "#Study, #Consistency",
                    //    GoalType = AchievementGoalType.ActiveTime,
                    //    Target = 600, // minutes
                    //    CurrentValue = 245
                    //},
                    //new AchievementCardModel
                    //{
                    //    Title = "Gym Rat",
                    //    Status = "Completed",
                    //    Tags = "#Fitness",
                    //    GoalType = AchievementGoalType.Value,
                    //    Target = 1000,
                    //    CurrentValue = 1000,
                    //    CompletedAt = DateTime.Now.AddDays(-2),
                    //    CompletionType = AchievementCompletionType.Range,
                    //    RangeAmount = 6,
                    //    RangeUnit  = AchievementRangeUnit.Months,
                    //    LastEarnedAt = DateTime.Now.AddDays(-2),
                    //    ActiveTimeTargetText = "200:00:00"
                    //}
                });
        }

        private AchievementsPageModel CreateMetaAchievementsPage()
        {
            return new AchievementsPageModel(
                "Meta-Achievements",
                new ObservableCollection<AchievementCardModel>
                {
                    //new AchievementCardModel
                    //{
                    //    Title = "Achievement Hunter",
                    //    Status = "In-Progress",
                    //    Tags = "#Meta",
                    //    GoalType = AchievementGoalType.Steps,
                    //    Target = 10,
                    //    CurrentValue = 3
                    //}
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
