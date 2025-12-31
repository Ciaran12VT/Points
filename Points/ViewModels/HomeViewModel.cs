using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Points.Models;
using Points.Global;
using Points.Services;

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
        public Action<IActiveCardModel>? ScrollToCardRequested;
        public Command FilterPositiveCommand { get; }
        public Command FilterNegativeCommand { get; }
        public Command ClearFiltersCommand { get; }
        public Command SortByLastActiveCommand { get; }
        public Command OpenAchievementsCommand { get; }

        public Command OpenDateRangePickerViewCommand { get; }
        public Command OpenSettingsCommand { get; }
        public Command OpenReportsCommand { get; }

        //public Command<MissionCardModel> MissionCancelCommand { get; }
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

        public string HeaderDate
        {
            get
            {
                if(GlobalVariables.RangeStart.Date == GlobalVariables.RangeEnd.Date)
                {
                    return GlobalVariables.RangeStart.Date.ToString("MMM-dd-yyyy");
                }
                else
                {
                    return $"{GlobalVariables.RangeStart.Date.ToString("MMM-dd")} - {GlobalVariables.RangeEnd.Date.ToString("MMM-dd")}";
                }
            }
        }

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

                var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
                if (missionPage == null) return false;

                foreach (var m in missionPage.AllCards.OfType<MissionCardModel>())
                {
                    if (m.IsComplete) continue;
                    if (now < m.AvailableFromDate) continue;

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
            // Commands
            ActivateCardCommand = new Command<IActiveCardModel>(RequestActivate);
            AddCardCommand = new Command(async () => await AddCardAsync());
            FilterPositiveCommand = new Command(ApplyPositiveFilter);
            FilterNegativeCommand = new Command(ApplyNegativeFilter);
            ClearFiltersCommand = new Command(ClearFilters);
            ScrollToActiveCardCommand = new Command(RequestScrollToActiveCard);
            SortByLastActiveCommand = new Command(SortCardsByLastActive);
            FilterByTagCommand = new Command(async () => await FilterCardsByTag());
            OpenAchievementsCommand = new Command(async () => await OpenAchievementsAsync());
            OpenDateRangePickerViewCommand = new Command(async () => await OpenDateRangePickerViewAsync());
            OpenSettingsCommand = new Command(async () => await OpenSettingsAsync());
            OpenReportsCommand = new Command(async () => await OpenReportsAsync());
            //MissionCancelCommand = new Command<MissionCardModel>(async m => await OnMissionCancelAsync(m));

            // Pages + mock data moved out of constructor logic
            InitializePages();        // defines pages (empty)
            SeedMockCards();          // adds mocks via the SAME route as UI adds

            // Start on Main Quest
            Position = 0;

            // Ensure mission ordering after seeding
            SortMissionCards();
        }

        #region Single-route add/commit pipeline

        /// <summary>
        /// UI entry: Add button.
        /// Uses the same pipeline: (create model) -> (open details if needed) -> (commit card to page) -> (post-commit hooks)
        /// </summary>
        private async Task AddCardAsync()
        {
            await AddCardFlowAsync(model: null, targetPage: CurrentPage, openDetails: true);
        }

        /// <summary>
        /// Strong single-route guarantee:
        /// - If model is null: chooses a model based on the target page.
        /// - If openDetails is true: navigates to the relevant details page and commits from the save callback.
        /// - If openDetails is false: commits immediately (used for mock seeding).
        /// </summary>
        private async Task AddCardFlowAsync(ICardModel? model, HomePageModel targetPage, bool openDetails)
        {
            if (targetPage == null) return;

            // If the caller didn't supply a model, create one based on the page
            model ??= await CreateModelForPageAsync(targetPage);

            if (model == null) return;

            if (!openDetails)
            {
                CommitCardToPage(targetPage, model);
                return;
            }

            // Open details UI, and commit ONLY through the shared commit route
            await OpenDetailsForModelAsync(targetPage, model);
        }

        /// <summary>
        /// Page-based model creation (only place that decides "what model to use").
        /// For Main Quest, shows ActionSheet because there are multiple card types.
        /// </summary>
        private async Task<ICardModel?> CreateModelForPageAsync(HomePageModel page)
        {
            if (page.Name == "Main Quest")
            {
                var choice = await Shell.Current.DisplayActionSheet(
                    "Add Card",
                    "Cancel",
                    null,
                    "Time-At-Task",
                    "Step-Completion");

                if (choice == "Time-At-Task")
                {
                    return CreateDefaultTat();
                }
                else if (choice == "Step-Completion")
                {
                    return CreateDefaultSc();
                }

                return null;
            }

            if (page.Name == "Mission")
            {
                return CreateDefaultMission();
            }

            if (page.Name == "Budgets")
            {
                return CreateDefaultBudget();
            }

            return null;
        }

        /// <summary>
        /// Navigation to details page is here; commit only happens via CommitCardToPage.
        /// </summary>
        private async Task OpenDetailsForModelAsync(HomePageModel page, ICardModel model)
        {
            if (model is ScCardModel sc)
            {
                await Shell.Current.Navigation.PushAsync(
                    new Points.Views.Details.ScDetailsPage(
                        sc,
                        saved => CommitCardToPage(page, saved),
                        deleted => RemoveCardFromPage(page, deleted),
                        GetTags()
                    )
                );
                return;
            }

            if (model is TatCardModel tat)
            {
                await Shell.Current.Navigation.PushAsync(
                    new Points.Views.Details.TatDetailsPage(
                        tat,
                        saved => CommitCardToPage(page, saved),
                        deleted => RemoveCardFromPage(page, deleted),
                        GetTags()
                    )
                );
                return;
            }

            if (model is MissionCardModel mission)
            {
                await Shell.Current.Navigation.PushAsync(
                    new Points.Views.Details.MissionDetailsPage(
                        mission,
                        saved => CommitCardToPage(page, saved),
                        onDelete: m => DeleteMission(m),
                        onFail: m => FailMission(m),
                        GetTags()
                    )
                );
                return;
            }

            if (model is BudgetCardModel budget)
            {
                await Shell.Current.Navigation.PushAsync(
                    new Points.Views.Details.BudgetDetailsPage(
                        budget,
                        saved => CommitCardToPage(page, saved),
                        deleted => RemoveCardFromPage(page, deleted),
                        GetTags()
                    )
                );
                return;
            }
        }

        /// <summary>
        /// The ONE AND ONLY way a card gets added to a page.
        /// All callers (mock seeding + UI save callbacks) must use this.
        /// </summary>
        private void CommitCardToPage(HomePageModel page, ICardModel card)
        {
            if (page == null || card == null) return;

            // If card already exists (editing existing), don't duplicate
            // (Reference equality is the safest default here.)
            if (!page.AllCards.Contains(card))
            {
                page.AddCard(card);
            }

            // Post-commit hooks centralized here
            AfterCardCommitted(page, card);
        }

        private void AfterCardCommitted(HomePageModel page, ICardModel card)
        {
            // Any per-page logic that must always happen after add/update
            if (page.Name == "Mission" && card is MissionCardModel)
            {
                SortMissionCards();
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
            }

            // For value/colour refresh, etc.
            OnPropertyChanged(nameof(GlobalValueColor));
        }

        private void RemoveCardFromPage(HomePageModel page, ICardModel card)
        {
            if (page == null || card == null) return;
            page.RemoveCard(card);

            if (page.Name == "Mission")
            {
                SortMissionCards();
                OnPropertyChanged(nameof(HasNegativeAvailableMission));
            }
        }

        #endregion

        #region Default model factories (no side effects)

        private static TatCardModel CreateDefaultTat()
        {
            return new TatCardModel
            {
                Title = "New TAT",
                Status = "In-Progress",
                Tags = "",
                Description = "",
                ValuePerMinute = 1.0
            };
        }

        private static ScCardModel CreateDefaultSc()
        {
            return new ScCardModel
            {
                Title = "New SC",
                Status = "In-Progress",
                Tags = "",
                Description = "",
                ValuePerMinute = 1.0
            };
        }

        private static MissionCardModel CreateDefaultMission()
        {
            var now = DateTime.Now;

            return new MissionCardModel
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
        }

        private static BudgetCardModel CreateDefaultBudget()
        {
            return new BudgetCardModel
            {
                Title = "New Budget",
                Status = "In-Progress",
                Tags = "",
                Currency = "Kcal",
                ExchangeRate = 0.01,
                StartDate = DateTime.Now,
                InitialBalance = 0
            };
        }

        #endregion

        #region Page + mock seeding (moved out of ctor; uses single route)

        private void InitializePages()
        {
            Pages.Clear();

            Pages.Add(new HomePageModel("Main Quest", Enumerable.Empty<IActiveCardModel>()));
            Pages.Add(new HomePageModel("Mission", Enumerable.Empty<IActiveCardModel>()));
            Pages.Add(new HomePageModel("Budgets", Enumerable.Empty<IActiveCardModel>()));
        }

        private void SeedMockCards()
        {
            var mainQuest = Pages.First(p => p.Name == "Main Quest");
            var mission = Pages.First(p => p.Name == "Mission");
            var budgets = Pages.First(p => p.Name == "Budgets");

            // Main Quest mocks (commit through single route)
            var testValueRates = new List<ValueRateModel>
            {
                new ValueRateModel() { RateName = "Higher Rate", ValuePerMinute = 5 }
            };

            var mainQuestMocks = new IActiveCardModel[]
            {
                new TatCardModel { Title = "TAT 1", ValuePerMinute = 1.25, ValueRates = testValueRates },
                new ScCardModel  { Title = "SC 1",  ValuePerMinute = 1.00 },
                new TatCardModel { Title = "TAT 2", ValuePerMinute = 0.75 },
                new TatCardModel { Title = "TAT 3", ValuePerMinute = -1.00 },
            };

            foreach (var c in mainQuestMocks)
                CommitCardToPage(mainQuest, c);

            // Mission mocks
            var today = DateTime.Today;
            var now = DateTime.Now;
            DateTime AtToday(int hour, int minute = 0) => today.AddHours(hour).AddMinutes(minute);

            var missionCards = new IActiveCardModel[]
            {
                // ===== AVAILABLE + INCOMPLETE =====
                new MissionCardModel
                {
                    Title = "Stable - Available & Incomplete",
                    Tags = "#Stable #Available",
                    SubType = MissionSubType.Stable,
                    Value = 25,
                    CreatedDate = now.AddDays(-2),
                    AvailableFromDate = today.AddDays(-1),
                    DueDate = today.AddDays(+2),
                },

                new MissionCardModel
                {
                    Title = "Degrade - Available & Incomplete",
                    Tags = "#Degrade #Available",
                    SubType = MissionSubType.Degrade,
                    Value = 30,
                    CreatedDate = now.AddDays(-1),
                    AvailableFromDate = AtToday(8, 0),
                    DueDate = AtToday(18, 0),
                },

                new MissionCardModel
                {
                    Title = "Rot - Available, Overdue & Incomplete",
                    Tags = "#Rot #Overdue",
                    SubType = MissionSubType.Rot,
                    Value = 40,
                    CreatedDate = now.AddDays(-3),
                    AvailableFromDate = today.AddDays(-2),
                    DueDate = AtToday(10, 0),
                },

                // ===== NOT AVAILABLE YET (should be greyed + disabled) =====
                new MissionCardModel
                {
                    Title = "Stable - Not Available Yet",
                    Tags = "#Stable #Locked",
                    SubType = MissionSubType.Stable,
                    Value = 15,
                    CreatedDate = now,
                    AvailableFromDate = now.AddHours(+2),
                    DueDate = today.AddDays(+1),
                },

                new MissionCardModel
                {
                    Title = "Degrade - Not Available Yet",
                    Tags = "#Degrade #Locked",
                    SubType = MissionSubType.Degrade,
                    Value = 20,
                    CreatedDate = now,
                    AvailableFromDate = today.AddDays(+1),
                    DueDate = today.AddDays(+2),
                },

                new MissionCardModel
                {
                    Title = "Rot - Not Available Yet",
                    Tags = "#Rot #Locked",
                    SubType = MissionSubType.Rot,
                    Value = 10,
                    CreatedDate = now,
                    AvailableFromDate = today.AddDays(+1),
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

            // mark some completed
            ((MissionCardModel)missionCards[6]).Complete(AtToday(9, 15));
            ((MissionCardModel)missionCards[7]).Complete(AtToday(11, 30));
            ((MissionCardModel)missionCards[8]).Complete(AtToday(14, 10));

            foreach (var c in missionCards)
                CommitCardToPage(mission, c);

            // Budget mocks
            var budget = new BudgetCardModel
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
            };

            CommitCardToPage(budgets, budget);
        }

        #endregion

        #region Methods (existing behavior preserved)

        public async void RequestActivate(IActiveCardModel card)
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

            if(card is TatCardModel tat && tat.ValueRates.Count > 0)
            {
                List<string> rateNames = ["Base Rate", .. tat.ValueRates.Select(x => x.RateName)];

                var choice = await Shell.Current.DisplayActionSheet(
                    "Choose Rate",
                    "Cancel",
                    null,
                    rateNames.ToArray()
                );

                if (!string.IsNullOrEmpty(choice))
                {
                    tat.SelectedValueRateModel = choice == "Base Rate" ? null : tat.ValueRates.FirstOrDefault(x => x.RateName == choice);
                }
                else
                {
                    return;
                }
            }

            // Deactivate previous
            _activeCard?.StopActivity();

            // Activate new
            if (!card.IsActive && card.ToggleActivityCommand.CanExecute(null))
                card.ToggleActivityCommand.Execute(null);

            _activeCard = card;
            OnPropertyChanged(nameof(HasActiveCard));
        }

        private void ApplyPositiveFilter()
        {
            var page = CurrentPage;
            if (page.Name != "Main Quest") return;

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
            if (page.Name != "Main Quest" && page.Name != "Mission") return;

            page.ResetVisible();
            SortMissionCards();
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

            if (!string.IsNullOrEmpty(choice))
            {
                foreach (var page in Pages)
                {
                    page.FilterCardsByTag(choice);
                }
            }
        }

        private List<string> GetTags()
        {
            var result = new List<string>();

            foreach (var page in Pages)
            {
                foreach (var card in page.AllCards)
                {
                    var cardTags = card.Tags.Replace('#', ' ').Replace(',', ' ')
                        .Split(' ')
                        .Select(x => x.Trim());

                    foreach (var cardTag in cardTags)
                    {
                        if (string.IsNullOrWhiteSpace(cardTag)) continue;

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

            ScrollToCardRequested?.Invoke((IActiveCardModel)_activeCard);
        }

        private void SortMissionCards()
        {
            var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
            if (missionPage == null) return;

            var missionCards = missionPage.AllCards.OfType<MissionCardModel>().ToList();
            if (missionCards.Count == 0) return;

            // Completed at top, ordered by CompletedDate (most recent first)
            // Others by AvailableFromDate (oldest -> newest)
            var sorted = missionCards
                .OrderByDescending(m => m.IsComplete) // true first
                .ThenBy(m => m.IsComplete ? m.CompletedDate : DateTime.MinValue)
                .ThenBy(m => m.IsComplete ? DateTime.MaxValue : m.AvailableFromDate)
                .ToList();

            // Check if there is any difference between missionPage.AllCards and sorted. If not, do not reset
            if (missionCards.Count == sorted.Count)
            {
                bool sameOrder = true;
                bool hasDateHeaderCards = missionPage.AllCards.OfType<DateHeaderCardModel>().Count() > 0;

                for (int i = 0; i < missionCards.Count; i++)
                {
                    if (!ReferenceEquals(missionCards[i], sorted[i]))
                    {
                        sameOrder = false;
                        break;
                    }
                }

                if (sameOrder && hasDateHeaderCards) return;
            }

            missionPage.AllCards.Clear();

            foreach (var m in sorted)
            {
                if (m == sorted[0] || sorted[sorted.IndexOf(m) - 1].AvailableFromDate.Date != m.AvailableFromDate.Date)
                {
                    var dateheadermodel = new DateHeaderCardModel()
                    {
                        Title = $"{m.AvailableFromDate.Date.ToString("MMM-dd yyyy")} ({GetRelativeDateString(m.AvailableFromDate)})",
                    };
                    missionPage.AllCards.Add(dateheadermodel);
                }

                missionPage.AllCards.Add(m);
            }

            missionPage.ResetVisible();
        }

        private string GetRelativeDateString(DateTime dt)
        {
            if(dt.Date < DateTime.Today)
            {
                if(dt.Date == DateTime.Today.AddDays(-1))
                {
                    return "Yesterday";
                }
                else
                {
                    return $"{(dt.Date - DateTime.Today).TotalDays * -1} Days Ago";
                }
            }

            if(dt.Date == DateTime.Today)
            {
                return "Today";
            }
            else if(dt.Date == DateTime.Today.AddDays(1))
            {
                return "Tomorrow";
            }
            else
            {
                return $"In {(dt.Date - DateTime.Today).TotalDays} Days"; 
            }
        }

        public void ScrollMainQuestIntoView()
        {
            Position = 0;
        }

        public void ScrollCardPageIntoView(ICardModel card)
        {
            var pg = Pages.FirstOrDefault(x => x.AllCards.Contains(card));
            int pos = Pages.IndexOf(pg);
            if (pos == -1) return;

            Position = pos;
        }

        private async Task OpenAchievementsAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Achievements.AchievementsPage(GetTags()));
        }

        private async Task OpenDateRangePickerViewAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Shared.DateRangePickerPage());
        }

        private async Task OpenSettingsAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Settings.SettingsPage(new SettingsViewModel(new MockDbService())));
        }

        private async Task OpenReportsAsync()
        {
            await Shell.Current.Navigation.PushAsync(new Points.Views.Reports.ReportPage());
        }

        public void FailMission(MissionCardModel model)
        {
            model.Fail(DateTime.Now);
            SortMissionCards();
        }

        public void DeleteMission(MissionCardModel model)
        {
            var missionPage = Pages.FirstOrDefault(p => p.Name == "Mission");
            if (missionPage == null)
                return;

            missionPage.AllCards.Remove(model);
            missionPage.VisibleCards.Remove(model);

            SortMissionCards();
            OnPropertyChanged(nameof(HasNegativeAvailableMission));
        }

        public async Task OpenExistingCardAsync(ICardModel model)
        {
            if (model == null) return;

            var page = Pages.FirstOrDefault(p => p.AllCards.Contains(model));
            if (page == null) return;

            await OpenDetailsForModelAsync(page, model);
        }


        #endregion

        public void Tick()
        {
            Now = DateTime.Now;

            foreach (var page in Pages)
                foreach (var card in page.AllCards.OfType<MissionCardModel>())
                    card.NotifyTimeChanged();

            SortMissionCards();

            double total = 0;
            foreach (var page in Pages)
                foreach (var card in page.AllCards)
                    total += card.GetValue(RangeStart, RangeEnd);

            TopRightValue = total;

            OnPropertyChanged(nameof(RangeEnd));
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
            var sorted = VisibleCards
                .OrderByDescending(x => ((IActiveCardModel)x).GetLastActiveTime())
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in sorted)
            {
                VisibleCards.Add(item);
            }
        }

        public void FilterCardsByTag(string choice)
        {
            var filtered = VisibleCards
                .Where(x => x.Tags.ToLower().Contains(choice.ToLower()))
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in filtered)
            {
                VisibleCards.Add(item);
            }
        }

        public void FilterCardsBySearchTerm(string choice)
        {
            var filtered = VisibleCards
                .Where(x =>
                    x.Tags.ToLower().Contains(choice.ToLower()) ||
                    x.Title.ToLower().Contains(choice.ToLower()))
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in filtered)
            {
                VisibleCards.Add(item);
            }
        }

        internal void RemoveCard(ICardModel card)
        {
            AllCards.Remove(card);
            VisibleCards.Remove(card);
        }



        #endregion
    }
}
