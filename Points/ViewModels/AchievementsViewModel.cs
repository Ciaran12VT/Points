using Points.Models;
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
                Deadline = DateTime.Now
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
                        // Add to whichever carousel page the user is currently on
                        page.Cards.Add(saved);
                    }
                )
            );
        }

        private IEnumerable<string> GetAllTags()
        {
            // Very simple parsing: split by comma from all cards’ Tags fields
            return Pages
                .SelectMany(p => p.Cards)
                .SelectMany(c => (c.Tags ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct()
                .OrderBy(x => x);
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


        public AchievementsViewModel()
        {
            Pages.Add(CreateAchievementsPage());
            Pages.Add(CreateMetaAchievementsPage());

            AddAchievementCommand = new Command(async () => await AddAchievementAsync());
            OpenTrophyRoomCommand = new Command(async () => await OpenTrophyRoomAsync());
        }

        private AchievementsPageModel CreateAchievementsPage()
        {
            return new AchievementsPageModel(
                "Achievements",
                new ObservableCollection<AchievementCardModel>
                {
                    new AchievementCardModel
                    {
                        Title = "Super Nerd",
                        Status = "In-Progress",
                        Tags = "#Study, #Consistency",
                        GoalType = AchievementGoalType.ActiveTime,
                        Target = 600, // minutes
                        CurrentValue = 245
                    },
                    new AchievementCardModel
                    {
                        Title = "Gym Rat",
                        Status = "Completed",
                        Tags = "#Fitness",
                        GoalType = AchievementGoalType.Value,
                        Target = 1000,
                        CurrentValue = 1000,
                        CompletedAt = DateTime.Now.AddDays(-2),
                        CompletionType = AchievementCompletionType.Range,
                        RangeAmount = 6,
                        RangeUnit  = AchievementRangeUnit.Months,
                        LastEarnedAt = DateTime.Now.AddDays(-2),
                        ActiveTimeTargetText = "200:00:00"
                    }
                });
        }

        private AchievementsPageModel CreateMetaAchievementsPage()
        {
            return new AchievementsPageModel(
                "Meta-Achievements",
                new ObservableCollection<AchievementCardModel>
                {
                    new AchievementCardModel
                    {
                        Title = "Achievement Hunter",
                        Status = "In-Progress",
                        Tags = "#Meta",
                        GoalType = AchievementGoalType.Steps,
                        Target = 10,
                        CurrentValue = 3
                    }
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
    }
}
