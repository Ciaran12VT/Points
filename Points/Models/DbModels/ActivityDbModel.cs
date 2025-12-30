namespace Points.Models.DbModels
{
    public class ActivityDbModel
    {
        public int ActivityID { get; set; }
        public int CardID { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

}
