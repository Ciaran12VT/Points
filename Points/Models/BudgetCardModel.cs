using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public class BudgetCardModel : ObservableObject, ICardModel
    {
        public long CardID { get; set; }
        public int DisplayOrder { get; set; }

        private string _title = "Daily Calories";
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _status = "In-Progress";
        public string Status { get => _status; set => SetProperty(ref _status, value); }

        private string _tags = "PRO TAT Other";
        public string Tags { get => _tags; set => SetProperty(ref _tags, value); }

        private string _currency = "Kcal";
        public string Currency { get => _currency; set => SetProperty(ref _currency, value); }

        // Convert budget currency -> global value
        private double _exchangeRate = 0.01;
        public double ExchangeRate { get => _exchangeRate; set => SetProperty(ref _exchangeRate, value); }

        private DateTime _startDate = ActivityTimeMath.LocalNow.Date;
        public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

        private double _initialBalance = 0;
        public double InitialBalance { get => _initialBalance; set => SetProperty(ref _initialBalance, value); }

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        public ObservableCollection<ScheduledTopUp> TopUps { get; set; } = new();
        public ObservableCollection<BudgetTransaction> Transactions { get; set; } = new();
        public int Id { get; set; }

        private bool _isCashInEnabled;
        public bool IsCashInEnabled
        {
            get => _isCashInEnabled;
            set
            {
                if (SetProperty(ref _isCashInEnabled, value))
                {
                    RaisePropertyChanged(nameof(IsCashInEnabled));
                }
            }
        }

        private string _nextTopUpCountdownText = "Next Top-Up In: --:--:--";
        public string NextTopUpCountdownText
        {
            get => _nextTopUpCountdownText;
            private set => SetProperty(ref _nextTopUpCountdownText, value);
        }

        private string _nextTopUpAmountText = "Next Top-Up Value: --";
        public string NextTopUpAmountText
        {
            get => _nextTopUpAmountText;
            private set => SetProperty(ref _nextTopUpAmountText, value);
        }

        // ---- Core calculations ----

        public double GetBalance(DateTime now)
        {
            var nowUtc = ActivityTimeMath.ToUtcAssumingLocal(now);
            var startDateUtc = ActivityTimeMath.ToUtcAssumingLocal(StartDate);

            // Balance = initial + all scheduled top-ups up to now - spends/cash-ins up to now
            var totalTopUps = GetTotalTopUpsApplied(now);
            var spent = Transactions
                .Where(t =>
                {
                    var timestampUtc = ActivityTimeMath.ToUtcAssumingLocal(t.Timestamp);
                    return timestampUtc <= nowUtc && timestampUtc >= startDateUtc;
                })
                .Sum(t => t.CurrencyAmount); // both Spend and CashIn subtract from balance

            return InitialBalance + totalTopUps - spent;
        }

        private double GetTotalTopUpsApplied(DateTime now)
        {
            if (now < StartDate) return 0;

            double sum = 0;
            for (var day = StartDate.Date; day <= now.Date; day = day.AddDays(1))
            {
                foreach (var tu in TopUps)
                {
                    var topUpTime = day + tu.TimeOfDay;
                    if (topUpTime <= now)
                        sum += tu.Amount;
                }
            }
            return sum;
        }

        public (DateTime When, double Amount)? GetNextTopUp(DateTime now)
        {
            // Find next scheduled top-up after 'now'
            // Check today (remaining) then future days
            for (int dayOffset = 0; dayOffset <= 365; dayOffset++)
            {
                var day = now.Date.AddDays(dayOffset);
                if (day < StartDate.Date) continue;

                foreach (var tu in TopUps.OrderBy(t => t.TimeOfDay))
                {
                    var dt = day + tu.TimeOfDay;
                    if (dt > now)
                        return (dt, tu.Amount);
                }
            }
            return null;
        }

        public double GetCashedInValue(DateTime start, DateTime end)
        {
            if (end <= start) return 0;

            var startUtc = ActivityTimeMath.ToUtcAssumingLocal(start);
            var endUtc = ActivityTimeMath.ToUtcAssumingLocal(end);

            return Transactions
                .Where(t =>
                {
                    var timestampUtc = ActivityTimeMath.ToUtcAssumingLocal(t.Timestamp);
                    return t.Type == BudgetTransactionType.CashIn
                           && timestampUtc >= startUtc
                           && timestampUtc <= endUtc;
                })
                .Sum(t => t.GlobalValueAmount);
        }

        // This is what contributes to the global top-right total
        public double GetValue(DateTime start, DateTime end)
        {
            double cashedInValue = GetCashedInValue(start, end);

            var now = ActivityTimeMath.LocalNow;
            double currentValue = GetGlobalValueRemaining(end > now ? now : end);

            if(currentValue < 0)
            {
                cashedInValue += currentValue;
            }

            return cashedInValue;
        }

        // ---- Commands/helpers you'll hook up later via forms/buttons ----

        public void AddSpend(double currencyAmount)
        {
            var now = ActivityTimeMath.LocalNow;
            Transactions.Add(new BudgetTransaction
            {
                Timestamp = ActivityTimeMath.UtcNow,
                Type = BudgetTransactionType.Spend,
                CurrencyAmount = currencyAmount,
                GlobalValueAmount = 0
            });

            NotifyTimeChanged(now);
        }

        public void AddCashIn(double currencyAmount)
        {
            var now = ActivityTimeMath.LocalNow;
            Transactions.Add(new BudgetTransaction
            {
                Timestamp = ActivityTimeMath.UtcNow,
                Type = BudgetTransactionType.CashIn,
                CurrencyAmount = currencyAmount,
                GlobalValueAmount = currencyAmount * ExchangeRate
            });

            NotifyTimeChanged(now);
        }

        public double GetDailyTopUpTotal(DateTime day)
        {
            // sum of scheduled top-ups for a given day (independent of "now")
            return TopUps.Sum(t => t.Amount);
        }

        public double GetGlobalValueRemaining(DateTime now)
        {
            return GetBalance(now) * ExchangeRate;
        }

        public void NotifyTimeChanged(DateTime now)
        {
            var next = GetNextTopUp(now);
            if (next is null)
            {
                NextTopUpCountdownText = "Next Top-Up In: --:--:--";
                NextTopUpAmountText = "Next Top-Up Value: --";
                return;
            }

            var remaining = next.Value.When - now;
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            var totalHours = (int)remaining.TotalHours;
            NextTopUpCountdownText = $"Next Top-Up In: {totalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            NextTopUpAmountText = $"Next Top-Up Value: {next.Value.Amount:0}";
        }

    }
}
