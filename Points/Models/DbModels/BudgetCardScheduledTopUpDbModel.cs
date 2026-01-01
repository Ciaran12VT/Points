namespace Points.Models.DbModels
{
    public class BudgetCardScheduledTopUpDbModel
    {
        public int BudgetCardScheduledTopUpID { get; set; }
        public int BudgetCardID { get; set; }

        public double Amount { get; set; }
        public TimeSpan TimeOfDay { get; set; }
    }

}
