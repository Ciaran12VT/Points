namespace Points.Models.DbModels
{
    public class BudgetCardDbModel
    {
        public int BudgetCardID { get; set; }
        public int CardID { get; set; }

        public string Status { get; set; } = "";
        public string Description { get; set; } = "";

        public string Currency { get; set; } = "";

        public double ExchangeRate { get; set; }

        public DateTime StartDate { get; set; }

        public double InitialBalance { get; set; }
    }

}
