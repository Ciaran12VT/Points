using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Models
{
    public enum BudgetTransactionType
    {
        Spend,   // subtract currency from balance
        CashIn   // convert currency to global value AND subtract currency from balance
    }

    public class BudgetTransaction
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public BudgetTransactionType Type { get; set; }

        // amount in budget currency
        public double CurrencyAmount { get; set; }

        // only meaningful for CashIn (CurrencyAmount * ExchangeRate at that moment)
        public double GlobalValueAmount { get; set; }
    }
}
