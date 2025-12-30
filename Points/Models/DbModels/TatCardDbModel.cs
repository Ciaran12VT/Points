namespace Points.Models.DbModels
{
    public class TatCardDbModel
    {
        public int TatCardID { get; set; }
        public int CardID { get; set; }
        public double ValuePerMinute { get; set; }
        public string Status { get; set; } = "";
        public string Description { get; set; } = "";
    }

}
