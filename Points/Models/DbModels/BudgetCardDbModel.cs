namespace Points.Models.DbModels
{
    public class BudgetCardDbModel
    {
        public int BudgetCardID { get; set; }
        public int CardID { get; set; }

        public string Status { get; set; } = "";
        public string Description { get; set; } = "";

        public double Value { get; set; }

        public string ResetPeriod { get; set; } = "";
        public DateTime ResetDate { get; set; }

        public double CurrentBalance { get; set; }
    }

}
