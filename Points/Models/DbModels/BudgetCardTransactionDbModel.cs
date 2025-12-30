namespace Points.Models.DbModels
{
    public class BudgetCardTransactionDbModel
    {
        public int BudgetCardTransactionID { get; set; }
        public int BudgetCardID { get; set; }

        public double Amount { get; set; }
        public string Type { get; set; } = "";
        public DateTime TimeStamp { get; set; }
        public string Description { get; set; } = "";
    }

}
