using Points.Global;
using Points.Models;
using Points.Services.Sqlite.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class BudgetDetailsViewModel : ObservableObject
    {
        private readonly BudgetCardModel _model;
        private readonly Action<BudgetCardModel> _onSaved;
        private Action<BudgetCardModel> _onDelete;
        private readonly IDbService _db;

        public List<string> AvailableTagList { get; }
        public Command CancelCommand { get; }
        public Command OpenTransactionLogCommand { get; }


        public ObservableCollection<BudgetTopUpEditItem> TopUps { get; } = new();

        public Command AddTopUpCommand { get; }
        public Command RemoveTopUpCommand { get; }
        public Command SaveCommand { get; }


        private readonly IDispatcherTimer _timer;
        public void StopTimer() => _timer?.Stop();


        public BudgetDetailsViewModel(BudgetCardModel model, Action<BudgetCardModel> onSaved, Action<BudgetCardModel> onDelete, List<string> availableTagsList, IDbService db)
        {
            _model = model;
            _onSaved = onSaved;
            _onDelete = onDelete;
            _db = db;
            AvailableTagList = availableTagsList;

            // Tick every second
            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, __) =>
            {

            };
            _timer.Start();

            AddTopUpCommand = new Command(AddTopUp);
            RemoveTopUpCommand = new Command<BudgetTopUpEditItem>(RemoveTopUp);
            SaveCommand = new Command(async () => await SaveAsync());
            CancelCommand = new Command(async () => await OnCancelAsync());
            OpenTransactionLogCommand = new Command(async () => await OpenTransactionLogAsync());


            // Editable fields from model
            Title = _model.Title;
            Tags = _model.Tags;
            Currency = _model.Currency;
            Description = _model.Description;

            ExchangeRateText = _model.ExchangeRate.ToString("0.###", CultureInfo.InvariantCulture);
            InitialBalanceText = _model.InitialBalance.ToString("0.##", CultureInfo.InvariantCulture);

            // Start DateTime split
            var start = _model.StartDate == default ? DateTime.Now : _model.StartDate;
            StartDate = start.Date;
            StartTime = start.TimeOfDay;

            // Status read-only
            // Load topups
            if (_model.TopUps.Count > 0)
            {
                foreach (var t in _model.TopUps.OrderBy(x => x.TimeOfDay))
                {
                    TopUps.Add(new BudgetTopUpEditItem
                    {
                        Id = t.Id,
                        AmountText = t.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                        TimeOfDay = t.TimeOfDay
                    });
                }
            }
            else
            {
                // sensible default
                TopUps.Add(new BudgetTopUpEditItem { AmountText = "500", TimeOfDay = new TimeSpan(7, 0, 0) });
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
                RaisePropertyChanged();
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
                RaisePropertyChanged();
            }
        }

        // Editable fields
        private string _title = "";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        public string Status => _model.Status;

        private string _tags = "";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        private string _currency = "Kcal";
        public string Currency { get => _currency; set => SetProperty(ref _currency, value); }

        private string _exchangeRateText = "0.010";
        public string ExchangeRateText
        {
            get => _exchangeRateText;
            set => SetProperty(ref _exchangeRateText, value);
        }

        private DateTime _startDate = DateTime.Now.Date;
        public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

        private TimeSpan _startTime = DateTime.Now.TimeOfDay;
        public TimeSpan StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }

        private string _initialBalanceText = "0";
        public string InitialBalanceText
        {
            get => _initialBalanceText;
            set => SetProperty(ref _initialBalanceText, value);
        }

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private void AddTopUp()
        {
            TopUps.Add(new BudgetTopUpEditItem
            {
                AmountText = "500",
                TimeOfDay = new TimeSpan(7, 0, 0)
            });
        }

        private void RemoveTopUp(BudgetTopUpEditItem? item)
        {
            if (item is null) return;
            TopUps.Remove(item);
        }

        private async Task SaveAsync()
        {
            // Parse exchange rate (3 dp max)
            if (!double.TryParse(ExchangeRateText, NumberStyles.Float, CultureInfo.InvariantCulture, out var exchangeRate))
                exchangeRate = 0;

            exchangeRate = Math.Round(exchangeRate, 3);

            // Parse initial balance
            if (!double.TryParse(InitialBalanceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var initialBalance))
                initialBalance = 0;

            // Compose start datetime
            var start = StartDate.Date + StartTime;

            // Build topups from editable list (ignore invalid rows)
            var parsedTopUps = new List<ScheduledTopUp>();
            foreach (var t in TopUps)
            {
                if (!double.TryParse(t.AmountText, NumberStyles.Float, CultureInfo.InvariantCulture, out var amt))
                    continue;

                parsedTopUps.Add(new ScheduledTopUp
                {
                    Id = t.Id,
                    Amount = amt,
                    TimeOfDay = t.TimeOfDay
                });
            }

            // Apply to model
            _model.Title = Title;
            _model.Tags = Tags;
            _model.Currency = Currency;
            _model.ExchangeRate = exchangeRate;

            _model.StartDate = start;
            _model.InitialBalance = initialBalance;

            _model.Description = Description;

            _model.TopUps.Clear();
            foreach (var t in parsedTopUps.OrderBy(x => x.TimeOfDay))
                _model.TopUps.Add(t);

            _onSaved(_model);

            await Shell.Current.Navigation.PopAsync();
        }

        private async Task OnCancelAsync()
        {
            var choice = await Shell.Current.DisplayActionSheet(
                _model.Title,
                "Cancel",
                null,
                "Delete"
            );

            if (choice == "Delete")
            {
                _onDelete?.Invoke(_model);
                await Shell.Current.Navigation.PopAsync();
            }
        }

        private async Task OpenTransactionLogAsync()
        {
            // Clone current transactions so the log page can edit/delete without mutating
            // until Save is pressed (same pattern as EditActiveTimePage).
            var working = _model.Transactions
                .Select(t => new BudgetTransaction
                {
                    Id = t.Id,
                    Timestamp = t.Timestamp,
                    Type = t.Type,
                    CurrencyAmount = t.CurrencyAmount,
                    GlobalValueAmount = t.GlobalValueAmount
                })
                .ToList();

            var tcs = new TaskCompletionSource<List<BudgetTransaction>>();

            await Shell.Current.Navigation.PushAsync(new Points.Views.Details.BudgetTransactionLogPage(
                transactions: working,
                tcs: tcs,
                exchangeRate: _model.ExchangeRate,
                db: _db
            ));

            var edited = await tcs.Task;
            if (edited is null) return;

            // Apply back to model (replace)
            _model.Transactions.Clear();
            foreach (var t in edited.OrderByDescending(x => x.Timestamp))
                _model.Transactions.Add(t);
        }

    }
}
