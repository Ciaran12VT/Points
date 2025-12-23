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

        public AchievementsViewModel()
        {
            Pages.Add(CreateAchievementsPage());
            Pages.Add(CreateMetaAchievementsPage());
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
                        CompletedAt = DateTime.Now.AddDays(-2)
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
            return new string[] { "" };
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
