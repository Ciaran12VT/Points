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

        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

        private double _initialBalance = 0;
        public double InitialBalance { get => _initialBalance; set => SetProperty(ref _initialBalance, value); }

        private string _description = "";
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        public ObservableCollection<ScheduledTopUp> TopUps { get; set; } = new();
        public ObservableCollection<BudgetTransaction> Transactions { get; set; } = new();
        public int Id { get; set; }

        // ---- Core calculations ----

        public double GetBalance(DateTime now)
        {
            // Balance = initial + all scheduled top-ups up to now - spends/cash-ins up to now
            var totalTopUps = GetTotalTopUpsApplied(now);
            var spent = Transactions
                .Where(t => t.Timestamp <= now)
                .Sum(t => t.CurrencyAmount); // both Spend and CashIn subtract from balance

            return InitialBalance + totalTopUps - spent;
        }

        public double GetTotalTopUpsApplied(DateTime now)
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

            return Transactions
                .Where(t => t.Type == BudgetTransactionType.CashIn
                            && t.Timestamp >= start
                            && t.Timestamp <= end)
                .Sum(t => t.GlobalValueAmount);
        }

        // This is what contributes to the global top-right total
        public double GetValue(DateTime start, DateTime end)
        {
            double cashedInValue = GetCashedInValue(start, end);

            double currentValue = GetGlobalValueRemaining(end > DateTime.Now ? DateTime.Now : end);

            if(currentValue < 0)
            {
                cashedInValue += currentValue;
            }

            return cashedInValue;
        }

        // ---- Commands/helpers you'll hook up later via forms/buttons ----

        public void AddSpend(double currencyAmount)
        {
            Transactions.Add(new BudgetTransaction
            {
                Timestamp = DateTime.Now,
                Type = BudgetTransactionType.Spend,
                CurrencyAmount = currencyAmount,
                GlobalValueAmount = 0
            });
        }

        public void AddCashIn(double currencyAmount)
        {
            Transactions.Add(new BudgetTransaction
            {
                Timestamp = DateTime.Now,
                Type = BudgetTransactionType.CashIn,
                CurrencyAmount = currencyAmount,
                GlobalValueAmount = currencyAmount * ExchangeRate
            });
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

    }
}
