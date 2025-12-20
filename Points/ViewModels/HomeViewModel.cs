using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Points.Models;
using Microsoft.VisualBasic;

namespace Points.ViewModels
{
    public class HomeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public Command<IActiveCardModel> ActivateCardCommand { get; }
        public ObservableCollection<HomePageModel> Pages { get; } = new();

        private HomePageModel CurrentPage => Pages[Math.Clamp(Position, 0, Pages.Count - 1)];

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

        private double _topRightValue;
        public double TopRightValue
        {
            get => _topRightValue;
            set
            {
                if (Math.Abs(_topRightValue - value) < 0.0000001) return;
                _topRightValue = value;
                OnPropertyChanged();
            }
        }

        private DateTime _rangeStart = DateTime.Today;
        public DateTime RangeStart
        {
            get => _rangeStart;
            set
            {
                if (_rangeStart == value) return;
                _rangeStart = value;
                OnPropertyChanged();
            }
        }

        private DateTime _rangeEnd = DateTime.Now;
        public DateTime RangeEnd
        {
            get => _rangeEnd;
            set
            {
                if (_rangeEnd == value) return;
                _rangeEnd = value;
                OnPropertyChanged();
            }
        }



        private IActiveCardModel? _activeCard;

        public void RequestActivate(IActiveCardModel card)
        {
            if (card == null) return;

            // If tapping the same active card: allow it to toggle off
            if (ReferenceEquals(_activeCard, card))
            {
                card.StopActivity();
                _activeCard = null;
                return;
            }

            // Deactivate previous
            _activeCard?.StopActivity();

            // Activate new
            // We only have ToggleActivityCommand publicly, so call it to start.
            if (!card.IsActive && card.ToggleActivityCommand.CanExecute(null))
                card.ToggleActivityCommand.Execute(null);

            _activeCard = card;
        }

        public HomeViewModel()
        {
            ActivateCardCommand = new Command<IActiveCardModel>(RequestActivate);

            Pages.Add(new HomePageModel("Main Quest", new ICardModel[]
                   {
                        new TatCardModel { Title = "TAT 1", ValuePerMinute = 1.25 },
                        new ScCardModel  { Title = "SC 1",  ValuePerMinute = 1.00 },
                        new TatCardModel { Title = "TAT 2", ValuePerMinute = 0.75 },
                        new TatCardModel { Title = "TAT 3", ValuePerMinute = -1.00 },

                   }));

            var today = DateTime.Today;
            var now = DateTime.Now;

            // Helpers
            DateTime AtToday(int hour, int minute = 0) => today.AddHours(hour).AddMinutes(minute);

            var missionCards = new ICardModel[]
            {
                // ===== AVAILABLE + INCOMPLETE =====

                new MissionCardModel
                {
                    Title = "Stable - Available & Incomplete",
                    Tags = "#Stable #Available",
                    SubType = MissionSubType.Stable,
                    Value = 25,
                    CreatedDate = now.AddDays(-2),
                    AvailableFromDate = today.AddDays(-1),       // already available
                    DueDate = today.AddDays(+2),                 // not due soon
                },

                new MissionCardModel
                {
                    Title = "Degrade - Available & Incomplete",
                    Tags = "#Degrade #Available",
                    SubType = MissionSubType.Degrade,
                    Value = 30,
                    CreatedDate = now.AddDays(-1),
                    AvailableFromDate = AtToday(8, 0),           // available since 8am
                    DueDate = AtToday(18, 0),                    // due 6pm today
                },

                new MissionCardModel
                {
                    Title = "Rot - Available, Overdue & Incomplete",
                    Tags = "#Rot #Overdue",
                    SubType = MissionSubType.Rot,
                    Value = 40,
                    CreatedDate = now.AddDays(-3),
                    AvailableFromDate = today.AddDays(-2),       // was available 2 days ago
                    DueDate = AtToday(10, 0),                    // due 10am today -> penalty stream after 10am
                },

                // ===== NOT AVAILABLE YET (should be greyed + disabled) =====

                new MissionCardModel
                {
                    Title = "Stable - Not Available Yet",
                    Tags = "#Stable #Locked",
                    SubType = MissionSubType.Stable,
                    Value = 15,
                    CreatedDate = now,
                    AvailableFromDate = now.AddHours(+2),        // future -> locked
                    DueDate = today.AddDays(+1),
                },

                new MissionCardModel
                {
                    Title = "Degrade - Not Available Yet",
                    Tags = "#Degrade #Locked",
                    SubType = MissionSubType.Degrade,
                    Value = 20,
                    CreatedDate = now,
                    AvailableFromDate = today.AddDays(+1),       // tomorrow -> locked
                    DueDate = today.AddDays(+2),
                },

                new MissionCardModel
                {
                    Title = "Rot - Not Available Yet",
                    Tags = "#Rot #Locked",
                    SubType = MissionSubType.Rot,
                    Value = 10,
                    CreatedDate = now,
                    AvailableFromDate = today.AddDays(+1),       // tomorrow -> locked
                    DueDate = today.AddDays(+1).AddHours(6),
                },

                // ===== COMPLETED TODAY (should float to top, sorted by CompletedDate) =====

                new MissionCardModel
                {
                    Title = "Stable - Completed Today",
                    Tags = "#Stable #Done",
                    SubType = MissionSubType.Stable,
                    Value = 25,
                    CreatedDate = now.AddDays(-5),
                    AvailableFromDate = today.AddDays(-2),
                    DueDate = today.AddDays(+5),
                },

                new MissionCardModel
                {
                    Title = "Degrade - Completed Today",
                    Tags = "#Degrade #Done",
                    SubType = MissionSubType.Degrade,
                    Value = 30,
                    CreatedDate = now.AddDays(-2),
                    AvailableFromDate = AtToday(7, 0),
                    DueDate = AtToday(19, 0),
                },

                new MissionCardModel
                {
                    Title = "Rot - Completed Today (Freezes Damage)",
                    Tags = "#Rot #Done",
                    SubType = MissionSubType.Rot,
                    Value = 40,
                    CreatedDate = now.AddDays(-2),
                    AvailableFromDate = today.AddDays(-1),
                    DueDate = AtToday(9, 0),
                },
            };

            // Mark some as completed at specific times today
            ((MissionCardModel)missionCards[6]).Complete(AtToday(9, 15));   // Stable completed 09:15
            ((MissionCardModel)missionCards[7]).Complete(AtToday(11, 30));  // Degrade completed 11:30
            ((MissionCardModel)missionCards[8]).Complete(AtToday(14, 10));  // Rot completed 14:10 (after due -> freezes penalty)

            // Build the Mission page
            Pages.Add(new HomePageModel("Mission", missionCards));

            Pages.Add(new HomePageModel("Budgets", new ICardModel[]
            {
                new BudgetCardModel
                {
                    Title = "Calorie Budget",
                    Currency = "Kcal",
                    ExchangeRate = 0.01,
                    StartDate = DateTime.Today,
                    InitialBalance = 0,
                    Status = "In-Progress",
                    Tags = "PRO TAT Other",
                    TopUps =
                    {
                        new ScheduledTopUp { TimeOfDay = new TimeSpan(7,0,0), Amount = 500 },
                        new ScheduledTopUp { TimeOfDay = new TimeSpan(12,0,0), Amount = 500 },
                        new ScheduledTopUp { TimeOfDay = new TimeSpan(18,0,0), Amount = 500 },
                    }
                }
            }));

            // Start on Main Quest
            Position = 0;
        }

        public bool AddCardToCurrentPage()
        {
            var page = CurrentPage;

            // For now: allow TAT only on Main Quest (example rule)
            if (page.Name != "Main Quest")
                return false;

            page.Cards.Add(new TatCardModel { Title = "New TAT", ValuePerMinute = 1.00 });
            return true;
        }

        public void Tick()
        {
            // Test range: "today so far"
            RangeStart = DateTime.Today;   // stable, but fine to keep explicit
            RangeEnd = DateTime.Now;

            foreach (var page in Pages)
                foreach (var card in page.Cards.OfType<Points.Models.MissionCardModel>())
                    card.NotifyTimeChanged();

            SortMissionCards();

            double total = 0;
            foreach (var page in Pages)
                foreach (var card in page.Cards)
                    total += card.GetValue(RangeStart, RangeEnd);
                             
            TopRightValue = total;

            OnPropertyChanged(nameof(RangeEnd));
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void SortMissionCards()
        {
            var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
            if (missionPage == null) return;

            var missionCards = missionPage.Cards.OfType<Points.Models.MissionCardModel>().ToList();
            if (missionCards.Count == 0) return;

            // Completed at top, ordered by CompletedDate (most recent first)
            // Others by AvailableFromDate (oldest -> newest)
            var sorted = missionCards
                .OrderByDescending(m => m.IsComplete) // true first
                .ThenBy(m => m.IsComplete ? m.CompletedDate : DateTime.MinValue) // completed most-recent first
                .ThenBy(m => m.IsComplete ? DateTime.MaxValue : m.AvailableFromDate) // incomplete by AvailableFromDate asc
                .ToList();

            // Rebuild the observable collection in-place
            // (only for mission cards; preserves other card types if you ever mix them in)
            // If Mission page is mission-only, this is enough:
            missionPage.Cards.Clear();
            foreach (var m in sorted)
                missionPage.Cards.Add(m);
        }

    }


    public class HomePageModel
    {
        public string Name { get; }
        public ObservableCollection<ICardModel> Cards { get; } = new();

        public HomePageModel(string name, IEnumerable<ICardModel> cards)
        {
            Name = name;
            foreach (var c in cards) Cards.Add(c);
        }
    }
}
