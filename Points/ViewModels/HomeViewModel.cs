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
using Points.Global;

namespace Points.ViewModels
{
    public class HomeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        #region Commands
        public Command<IActiveCardModel> ActivateCardCommand { get; }
        public Command AddCardCommand { get; }
        public Command ScrollToActiveCardCommand { get; }
        public Command FilterByTagCommand { get; }
        public Action<ICardModel>? ScrollToCardRequested;
        public Command FilterPositiveCommand { get; }
        public Command FilterNegativeCommand { get; }
        public Command ClearFiltersCommand { get; }
        public Command SortByLastActiveCommand { get; }
        public Command OpenAchievementsCommand { get; }


        #endregion

        #region Fields

        public bool HasActiveCard => _activeCard != null;
        public ObservableCollection<HomePageModel> Pages { get; } = new();
        private HomePageModel CurrentPage => Pages[Math.Clamp(Position, 0, Pages.Count - 1)];

        public DateTime _now = DateTime.Now;
        public DateTime Now
        {
            get => _now;
            set
            {
                if (_now == value) return;
                _now = value;
                OnPropertyChanged();
            }
        }

        private enum MainQuestFilterMode
        {
            None,
            PositiveOnly,
            NegativeOnly
        }

        private MainQuestFilterMode _mainQuestFilterMode = MainQuestFilterMode.None;

        public Color GlobalValueColor
        {
            get
            {
                if (TopRightValue < 0) return Colors.Red;
                if (TopRightValue < 100) return Colors.Orange;
                return Colors.Green;
            }
        }

        public bool HasNegativeAvailableMission
        {
            get
            {
                var now = DateTime.Now;

                // Adjust this to match how your pages are stored.
                var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
                if (missionPage == null) return false;

                foreach (var m in missionPage.AllCards.OfType<MissionCardModel>())
                {
                    // Available = not complete AND now >= AvailableFromDate
                    if (m.IsComplete) continue;
                    if (now < m.AvailableFromDate) continue;

                    // "current value" means "what would it be worth if frozen now"
                    var current = m.GetCurrentValue(now);

                    if (current < 0)
                        return true;
                }

                return false;
            }
        }

        private int _position;
        public int Position
        {
            get => _position;
            set
            {
                if (_position == value) return;
                _position = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPage));
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
                OnPropertyChanged(nameof(GlobalValueColor));
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
                OnPropertyChanged(nameof(TopRightValue));
            }
        }

        private DateTime _rangeStart = GlobalVariables.RangeStart;
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

        private DateTime _rangeEnd = GlobalVariables.RangeEnd;
        public DateTime RangeEnd
        {
            get => _rangeEnd;
            set
            {
                if (_rangeEnd == value) return;
                _rangeEnd = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GlobalValueColor));
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
            }
        }

        private IActiveCardModel? _activeCard;

        #endregion

        public HomeViewModel()
        {
            #region Commands

            ActivateCardCommand = new Command<IActiveCardModel>(RequestActivate);
            AddCardCommand = new Command(async () => await AddCardAsync());
            FilterPositiveCommand = new Command(ApplyPositiveFilter);
            FilterNegativeCommand = new Command(ApplyNegativeFilter);
            ClearFiltersCommand = new Command(ClearFilters);
            ScrollToActiveCardCommand = new Command(RequestScrollToActiveCard);
            SortByLastActiveCommand = new Command(SortCardsByLastActive);
            FilterByTagCommand = new Command(async () => await FilterCardsByTag());
            OpenAchievementsCommand = new Command(async () => await OpenAchievementsAsync());

            #endregion

            #region Add Cards

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

            #endregion
        }

        #region Methods

        public void RequestActivate(IActiveCardModel card)
        {
            if (card == null) return;

            // If tapping the same active card: allow it to toggle off
            if (ReferenceEquals(_activeCard, card))
            {
                card.StopActivity();
                _activeCard = null;
                OnPropertyChanged(nameof(HasActiveCard));
                return;
            }

            // Deactivate previous
            _activeCard?.StopActivity();

            // Activate new
            // We only have ToggleActivityCommand publicly, so call it to start.
            if (!card.IsActive && card.ToggleActivityCommand.CanExecute(null))
                card.ToggleActivityCommand.Execute(null);

            _activeCard = card;
            OnPropertyChanged(nameof(HasActiveCard));
        }

        private async Task AddCardAsync()
        {
            var page = CurrentPage;

            if (page.Name == "Main Quest")
            {
                var choice = await Shell.Current.DisplayActionSheet(
                    "Add Card",
                    "Cancel",
                    null,
                    "Time-At-Task",
                    "Step-Completion");

                if (choice == "Time-At-Task")
                    await CreateTatAsync(page);
                else if (choice == "Step-Completion")
                    await CreateScAsync(page);

                return;
            }

            if (page.Name == "Mission")
            {
                await CreateMissionAsync(page);
                return;
            }

            if (page.Name == "Budgets")
            {
                await CreateBudgetAsync(page);
                return;
            }
        }

        private async Task CreateTatAsync(HomePageModel page)
        {
            var model = new TatCardModel
            {
                Title = "New TAT",
                Status = "In-Progress",
                Tags = "",
                Description = "",
                ValuePerMinute = 1.0
            };

            await Shell.Current.Navigation.PushAsync(
                new Points.Views.Details.TatDetailsPage(model, saved =>
                {
                    page.AddCard(saved);
                })
            );
        }

        private async Task CreateScAsync(HomePageModel page)
        {
            var model = new ScCardModel
            {
                Title = "New SC",
                Status = "In-Progress",
                Tags = "",
                Description = "",
                ValuePerMinute = 1.0
            };

            await Shell.Current.Navigation.PushAsync(
                new Points.Views.Details.ScDetailsPage(model, saved => { page.AddCard(saved); })
            );
        }

        private async Task CreateMissionAsync(HomePageModel page)
        {
            var now = DateTime.Now;

            var model = new MissionCardModel
            {
                Title = "New Mission",
                Status = "In-Progress",                 // non-editable in form
                Tags = "",
                SubType = MissionSubType.Stable,
                Value = 0,
                CreatedDate = now,                      // auto-set to now
                AvailableFromDate = now,                // default now
                DueDate = now.AddDays(1),               // default now + 1 day
                Description = ""
            };

            await Shell.Current.Navigation.PushAsync(
                new Points.Views.Details.MissionDetailsPage(model, saved =>
                {
                    page.AddCard(saved);
                    // If you have mission sorting, call it here if needed
                    // SortMissionCards();
                })
            );
        }

        private async Task CreateBudgetAsync(HomePageModel page)
        {
            var model = new BudgetCardModel
            {
                Title = "New Budget",
                Status = "In-Progress",
                Tags = "",
                Currency = "Kcal",
                ExchangeRate = 0.01,
                StartDate = DateTime.Now,
                InitialBalance = 0
            };

            await Shell.Current.Navigation.PushAsync(
                new Points.Views.Details.BudgetDetailsPage(model, saved =>
                {
                    page.AllCards.Add(saved);
                })
            );
        }

        private void ApplyPositiveFilter()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest") return;

            // Toggle behavior: pressing + again turns filter off
            _mainQuestFilterMode =
                _mainQuestFilterMode == MainQuestFilterMode.PositiveOnly
                    ? MainQuestFilterMode.None
                    : MainQuestFilterMode.PositiveOnly;

            ApplyMainQuestFilter(page);
        }

        private void ApplyNegativeFilter()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest") return;

            // Toggle behavior: pressing - again turns filter off
            _mainQuestFilterMode =
                _mainQuestFilterMode == MainQuestFilterMode.NegativeOnly
                    ? MainQuestFilterMode.None
                    : MainQuestFilterMode.NegativeOnly;

            ApplyMainQuestFilter(page);
        }

        private void ApplyMainQuestFilter(HomePageModel page)
        {
            switch (_mainQuestFilterMode)
            {
                case MainQuestFilterMode.None:
                    page.ResetVisible();
                    break;

                case MainQuestFilterMode.PositiveOnly:
                    page.ApplyFilter(IsPositiveMainQuestCard);
                    break;

                case MainQuestFilterMode.NegativeOnly:
                    page.ApplyFilter(IsNegativeMainQuestCard);
                    break;
            }
        }

        private void ClearFilters()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest") return;

            page.ResetVisible();
        }

        private void SortCardsByLastActive()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest") return;

            page.SortCardsByLastActive();
        }

        private async Task FilterCardsByTag()
        {
            var choice = await Shell.Current.DisplayActionSheet(
                "Add Card",
                "Cancel",
                null,
                GetTags().ToArray()
                );

            if(!string.IsNullOrEmpty(choice))
            {
                foreach (var page in Pages)
                {
                    page.FilterCardsByTag(choice);
                }
            }

            return;
        }

        private List<string> GetTags()
        {
            var result = new List<string>();

            foreach (var page in Pages)
            {
                foreach (var card in page.AllCards)
                {
                    var cardTags = card.Tags.Replace('#', ' ').Replace(',', ' ').Split(' ').Select(x => x.Trim());
                    foreach (var cardTag in cardTags)
                    {
                        if (!result.Contains(cardTag))
                        {
                            result.Add(cardTag);
                        }
                    }
                }
            }

            return result;
        }

        internal void FilterCardsBySearchTerm(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                foreach (var page in Pages)
                {
                    page.FilterCardsBySearchTerm(input);
                }
            }
        }

        private bool IsPositiveMainQuestCard(ICardModel card)
        {
            return card is IActiveCardModel active
                && active.ValuePerMinute > 0;
        }

        private bool IsNegativeMainQuestCard(ICardModel card)
        {
            return card is IActiveCardModel active
                && active.ValuePerMinute < 0;
        }

        private void RequestScrollToActiveCard()
        {
            if (_activeCard == null)
                return;

            ScrollToCardRequested?.Invoke((ICardModel)_activeCard);
        }
        private void SortMissionCards()
        {
            var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
            if (missionPage == null) return;

            var missionCards = missionPage.AllCards.OfType<Points.Models.MissionCardModel>().ToList();
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
            missionPage.AllCards.Clear();
            foreach (var m in sorted)
                missionPage.AllCards.Add(m);

            missionPage.ResetVisible();
        }

        public void ScrollMainQuestIntoView()
        {
            Position = 0;
        }

        private async Task OpenAchievementsAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Achievements.AchievementsPage());
        }


        #endregion

        public void Tick()
        {
            Now = DateTime.Now;

            foreach (var page in Pages)
                foreach (var card in page.AllCards.OfType<Points.Models.MissionCardModel>())
                    card.NotifyTimeChanged();

            SortMissionCards();

            double total = 0;
            foreach (var page in Pages)
                foreach (var card in page.AllCards)
                    total += card.GetValue(RangeStart, RangeEnd);
                             
            TopRightValue = total;

            OnPropertyChanged(nameof(RangeEnd));
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


    }


    public class HomePageModel
    {
        public string Name { get; }
        public ObservableCollection<ICardModel> AllCards { get; } = new();
        public ObservableCollection<ICardModel> VisibleCards { get; } = new();

        public HomePageModel(string name, IEnumerable<ICardModel> cards)
        {
            Name = name;
            foreach (var c in cards)
            {
                AllCards.Add(c);
                VisibleCards.Add(c);
            }
        }

        #region Methods

        public void ResetVisible()
        {
            VisibleCards.Clear();
            foreach (var c in AllCards)
                VisibleCards.Add(c);
        }

        public void ApplyFilter(Func<ICardModel, bool> predicate)
        {
            VisibleCards.Clear();
            foreach (var c in AllCards)
            {
                if (predicate(c))
                    VisibleCards.Add(c);
            }
        }

        public void AddCard(ICardModel card)
        {
            AllCards.Add(card);
            VisibleCards.Add(card);
        }

        public void SortCardsByLastActive()
        {
            var sorted = VisibleCards.OrderByDescending(x => ((IActiveCardModel)x).GetLastActiveTime()).ToArray();
            VisibleCards.Clear();
            foreach (var item in sorted)
            {
                VisibleCards.Add(item);
            }
        }

        public void FilterCardsByTag(string choice)
        {
            var filtered = VisibleCards.Where(x => x.Tags.ToLower().Contains(choice.ToLower())).ToArray();
            VisibleCards.Clear();
            foreach (var item in filtered)
            {
                VisibleCards.Add(item);
            }
        }

        public void FilterCardsBySearchTerm(string choice)
        {
            var filtered = VisibleCards.Where(x => x.Tags.ToLower().Contains(choice.ToLower()) || x.Title.ToLower().Contains(choice.ToLower())).ToArray();
            VisibleCards.Clear();
            foreach (var item in filtered)
            {
                VisibleCards.Add(item);
            }
        }

        #endregion

    }
}
